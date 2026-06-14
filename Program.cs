using System; // Import kiểu dữ liệu hệ thống cơ bản
using System.Text; // Import thư viện xử lý chuỗi và bảng mã ký tự (UTF-8...)
using System.Threading.Channels; // Import thư viện quản lý Thread-Safe Queue Channel
using System.Threading.Tasks; // Import hỗ trợ lập trình bất đồng bộ Task
using System.Threading.RateLimiting; // Import thư viện giới hạn tần suất yêu cầu (Rate Limiting)
using Microsoft.AspNetCore.Authentication.JwtBearer; // Import cấu hình bảo mật xác thực JWT
using Microsoft.AspNetCore.Identity; // Import thư viện quản lý người dùng và quyền hạn ASP.NET Core Identity
using Microsoft.AspNetCore.Builder; // Import thư viện khởi tạo ứng dụng Web Application
using Microsoft.AspNetCore.Http; // Import các giao diện HTTP context
using Microsoft.AspNetCore.RateLimiting; // Import Middleware Rate Limiting
using Microsoft.EntityFrameworkCore; // Import Entity Framework Core
using Microsoft.Extensions.Configuration; // Import cấu hình ứng dụng appsettings.json
using Microsoft.Extensions.DependencyInjection; // Import bộ quản lý Dependency Injection
using Microsoft.Extensions.Hosting; // Import quản lý môi trường chạy ứng dụng (Development, Production...)
using Microsoft.Extensions.Logging; // Import bộ ghi log chuẩn của Microsoft
using Microsoft.IdentityModel.Tokens; // Import các tham số xác thực Token
using Serilog; // Import thư viện logging nâng cao Serilog
using SemiconductorStrategyPulse.Caching; // Import dịch vụ Cache dự án
using SemiconductorStrategyPulse.Data; // Import Lớp Database Context
using SemiconductorStrategyPulse.Models; // Import thực thể dữ liệu (Models)
using SemiconductorStrategyPulse.Services; // Import dịch vụ logic nghiệp vụ (Services)
using SemiconductorStrategyPulse.Hubs; // Import Hub thời gian thực SignalR (Hubs)

// Khởi tạo đối tượng Builder của ứng dụng web
var builder = WebApplication.CreateBuilder(args);

// Khởi tạo và cấu hình thư viện ghi log Serilog đọc cấu hình từ appsettings.json
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration) // Đọc cấu hình từ appsettings.json
    .Enrich.FromLogContext() // Bổ sung thông tin ngữ cảnh vào log
    .CreateLogger(); // Tạo logger thực tế

builder.Host.UseSerilog(); // Cấu hình ứng dụng sử dụng Serilog thay thế trình ghi log mặc định

try
{
    Log.Information("Starting Semiconductor Strategy Pulse Web API..."); // Log thông tin khởi chạy ứng dụng

    // Thêm các dịch vụ API Controller vào DI Container
    builder.Services.AddControllers();

    // 1. Cấu hình Connection Pooling với PostgreSQL nhằm tái sử dụng các kết nối tối ưu hiệu năng
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
        ?? "Host=127.0.0.1;Port=5432;Database=postgres;Username=postgres;Password=0123;Pooling=true;Minimum Pool Size=5;Maximum Pool Size=100;";
    Log.Information("Loaded ConnectionString: {ConnectionString}", connectionString); // Ghi log chuỗi kết nối
    
    // Đăng ký DbContext với cơ chế Connection Pool
    builder.Services.AddDbContextPool<PulseDbContext>(options =>
        options.UseNpgsql(connectionString)); // Sử dụng hệ quản trị CSDL PostgreSQL

    // 2. Cấu hình Bộ nhớ đệm phân tán Redis với chế độ tự động chuyển sang bộ nhớ RAM của máy nếu Redis sập (Fallback)
    var redisConnString = builder.Configuration.GetValue<string>("Redis:ConnectionString") ?? "localhost:6379";
    bool isRedisAvailable = false;
    try
    {
        var parts = redisConnString.Split(':'); // Cắt chuỗi kết nối để lấy host và port
        var host = parts[0];
        var port = parts.Length > 1 ? int.Parse(parts[1]) : 6379;
        using (var tcpClient = new System.Net.Sockets.TcpClient()) // Sử dụng TcpClient để kiểm tra cổng kết nối Redis
        {
            var connectTask = tcpClient.ConnectAsync(host, port);
            isRedisAvailable = connectTask.Wait(500); // Thử kết nối trong vòng 500ms (timeout)
        }
    }
    catch
    {
        isRedisAvailable = false; // Báo lỗi nếu không kết nối được Redis
    }

    if (isRedisAvailable) // Nếu Redis đang hoạt động bình thường
    {
        // Đăng ký dịch vụ Distributed Cache sử dụng Redis
        builder.Services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnString; // Địa chỉ kết nối Redis
            options.InstanceName = "SemiconductorPulse_"; // Tiền tố (prefix) phân biệt dữ liệu trong Redis
        });
    }
    else // Nếu Redis không kết nối được
    {
        Log.Warning("Redis is not available at {RedisConnString}. Falling back to In-Memory Distributed Cache.", redisConnString);
        // Đăng ký lưu cache tạm thời trên bộ nhớ RAM của Web Server (In-Memory Distributed Cache)
        builder.Services.AddDistributedMemoryCache();
    }
    // Đăng ký dịch vụ bọc ICacheService sử dụng lớp RedisCacheService
    builder.Services.AddSingleton<ICacheService, RedisCacheService>();

    // 3. Khởi tạo Kênh truyền hàng đợi có giới hạn (Bounded Channel) an toàn đa luồng cho dữ liệu Telemetry thô
    builder.Services.AddSingleton(provider =>
    {
        // Tạo hàng đợi chứa tối đa 10,000 phần tử thô chờ xử lý
        return Channel.CreateBounded<RawMarketData>(new BoundedChannelOptions(10000)
        {
            FullMode = BoundedChannelFullMode.DropOldest // Nếu hàng đợi bị đầy, tự động loại bỏ phần tử cũ nhất để tránh nghẽn ứng dụng
        });
    });

    // 4. Đăng ký các dịch vụ logic nghiệp vụ của dự án
    builder.Services.AddSingleton<IIngestionService, IngestionService>(); // Dịch vụ nạp dữ liệu (vòng đời Singleton)
    builder.Services.AddScoped<IMetricService, MetricService>(); // Dịch vụ tính toán chỉ số (vòng đời Scoped)
    builder.Services.AddSingleton<ITokenService, TokenService>(); // Dịch vụ tạo JWT Token (vòng đời Singleton)

    // 5. Đăng ký các Tiến trình Chạy ngầm (Background Hosted Services)
    builder.Services.AddHostedService<BackgroundProcessor>(); // Tiến trình lấy dữ liệu từ hàng đợi lưu vào DB
    builder.Services.AddMemoryCache(); // Đăng ký dịch vụ cache bộ nhớ cơ bản
    builder.Services.AddSignalR(); // Đăng ký SignalR để giao tiếp thời gian thực qua WebSockets
    builder.Services.AddHostedService<MarketDataSimulator>(); // Tiến trình giả lập biến động giá cổ phiếu mỗi 2 giây

    // 6. Cấu hình Dịch vụ Xác thực người dùng (ASP.NET Core Identity)
    builder.Services.AddIdentity<AppUser, IdentityRole<Guid>>(options =>
    {
        options.Password.RequiredLength = 6; // Yêu cầu mật khẩu tối thiểu 6 ký tự
        options.Password.RequireDigit = true; // Yêu cầu có ít nhất 1 chữ số
        options.Password.RequireUppercase = true; // Yêu cầu có ít nhất 1 chữ hoa
        options.Password.RequireLowercase = false; // Không bắt buộc chữ thường
        options.Password.RequireNonAlphanumeric = false; // Không bắt buộc ký tự đặc biệt
        options.User.RequireUniqueEmail = true; // Yêu cầu Email phải là duy nhất
    })
    .AddEntityFrameworkStores<PulseDbContext>() // Sử dụng Database làm nơi lưu trữ thông tin Identity
    .AddDefaultTokenProviders(); // Đăng ký bộ sinh token mặc định (dùng cho reset pass, confirm email...)

    // Cấu hình các thông số Token bảo mật JWT từ appsettings.json
    var jwtKey = builder.Configuration["Jwt:Key"] ?? "super_secret_semiconductor_pulse_system_key_2026_long_key_required";
    var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "SemiconductorStrategyPulse";
    var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "SemiconductorPulseClients";

    // Thiết lập dịch vụ xác thực tài khoản sử dụng JWT Bearer
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        // Thiết lập các tham số kiểm tra tính hợp lệ của Token
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, // Kiểm tra bên phát hành token (Issuer)
            ValidateAudience = true, // Kiểm tra đối tượng sử dụng token (Audience)
            ValidateLifetime = true, // Kiểm tra thời hạn sử dụng của token
            ValidateIssuerSigningKey = true, // Kiểm tra chữ ký bảo mật của token
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)), // Khóa ký mã hóa token
            ClockSkew = TimeSpan.Zero // Loại bỏ độ lệch thời gian cho phép khi kiểm tra hết hạn
        };

        // Bắt sự kiện của JwtBearer để lấy Token từ Cookie HttpOnly thay vì Header Authorization mặc định
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                // Thử tìm đọc token từ Cookie "access_token" được thiết lập bởi Server trước đó
                if (context.Request.Cookies.TryGetValue("access_token", out var token))
                {
                    context.Token = token; // Gán token tìm được vào ngữ cảnh xác thực
                }
                return Task.CompletedTask;
            }
        };
    });

    // 7. Cấu hình Dịch vụ Giới hạn tần suất gửi yêu cầu (Rate Limiting Middleware)
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests; // Trả về mã lỗi 429 nếu bị giới hạn

        // Đăng ký chính sách giới hạn cho việc đọc dữ liệu (yêu cầu GET)
        options.AddFixedWindowLimiter("read-policy", opt =>
        {
            opt.Window = TimeSpan.FromSeconds(10); // Chu kỳ cửa sổ là 10 giây
            opt.PermitLimit = 50; // Cho phép tối đa 50 yêu cầu trong 10 giây
            opt.QueueLimit = 5; // Hàng chờ chứa tối đa 5 yêu cầu nếu bị nghẽn
            opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst; // Xử lý hàng chờ theo thứ tự cũ nhất trước
        });

        // Đăng ký chính sách giới hạn cho việc ghi dữ liệu (yêu cầu POST ingest)
        options.AddFixedWindowLimiter("write-policy", opt =>
        {
            opt.Window = TimeSpan.FromSeconds(10); // Chu kỳ cửa sổ là 10 giây
            opt.PermitLimit = 100; // Cho phép tối đa 100 lượt ghi trong 10 giây
            opt.QueueLimit = 10; // Hàng chờ tối đa 10 yêu cầu
            opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        });
    });

    // Cấu hình chia sẻ tài nguyên nguồn gốc chéo (CORS) cho phép Client frontend gọi API
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAll", policy =>
        {
            policy.WithOrigins("http://localhost:5173") // Chỉ cho phép URL client cụ thể kết nối
                  .AllowAnyMethod() // Cho phép tất cả phương thức GET, POST, PUT, DELETE...
                  .AllowAnyHeader() // Cho phép gửi mọi Header HTTP
                  .AllowCredentials(); // Cho phép gửi kèm cookie và thông tin xác thực
        });
    });

    // Cấu hình Swagger / OpenAPI phục vụ tài liệu API
    builder.Services.AddOpenApi(options =>
    {
        options.AddDocumentTransformer((document, context, cancellationToken) =>
        {
            document.Info.Title = "Semiconductor Strategy Pulse RESTful API"; // Tiêu đề trang tài liệu API
            document.Info.Version = "v1.0.0"; // Phiên bản API
            document.Info.Description = "Production-grade, high-performance data analytics engine for monitoring supply, yields, and capex in real time."; // Mô tả dự án
            return Task.CompletedTask;
        });
    });

    // Tiến hành xây dựng ứng dụng (Build) từ các dịch vụ đã khai báo ở trên
    var app = builder.Build();

    // Cấu hình chạy các file tĩnh trong wwwroot (index.html, app.js, style.css...)
    app.UseDefaultFiles();
    app.UseStaticFiles();

    // Kích hoạt Middleware CORS, Logging và Định tuyến
    app.UseCors("AllowAll"); // Áp dụng chính sách CORS
    app.UseSerilogRequestLogging(); // Ghi nhận chi tiết nhật ký request của client thông qua Serilog

    if (app.Environment.IsDevelopment()) // Nếu chạy trên môi trường phát triển (Development)
    {
        app.MapOpenApi(); // Ánh xạ đường dẫn xuất dữ liệu OpenAPI tài liệu hóa API
    }

    app.UseRateLimiter(); // Kích hoạt bộ giới hạn tần suất yêu cầu Rate Limiting
    app.UseAuthentication(); // Kích hoạt Middleware xác thực (nhận diện danh tính qua JWT)
    app.UseAuthorization(); // Kích hoạt Middleware phân quyền (kiểm tra quyền truy cập API)

    app.MapControllers(); // Định tuyến đến các API Controller
    app.MapHub<MarketHub>("/api/market/hub"); // Định tuyến WebSocket SignalR Hub tới đường dẫn tương ứng

    // 8. Tự động kiểm tra, dọn sạch và nạp dữ liệu mẫu (Seed Data) mỗi lần ứng dụng khởi chạy
    using (var scope = app.Services.CreateScope()) // Tạo Scope tạm thời để lấy DbContext
    {
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILogger<Program>>();
        try
        {
            var dbContext = services.GetRequiredService<PulseDbContext>();
            
            logger.LogInformation("Initializing and verifying database state...");
            // Xóa cơ sở dữ liệu cũ để reset toàn bộ trạng thái (Clean state trên mỗi lần chạy)
            await dbContext.Database.EnsureDeletedAsync();
            // Tạo mới lại cơ sở dữ liệu trắng
            await dbContext.Database.EnsureCreatedAsync();

            // Lấy bộ quản lý tài khoản người dùng và phân quyền
            var userManager = services.GetRequiredService<UserManager<AppUser>>();
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            // Chạy hàm nạp dữ liệu mẫu bao gồm tài khoản Admin, IoTDevice và các chỉ số mặc định
            await SeedData.InitializeAsync(dbContext, userManager, roleManager);
            logger.LogInformation("Database initialized and seeded successfully."); // Log thành công
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Critical database initialization failure."); // Log lỗi nếu khởi tạo DB thất bại
        }
    }

    app.Run(); // Khởi chạy ứng dụng lắng nghe các yêu cầu kết nối
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application startup terminated unexpectedly!"); // Log lỗi nghiêm trọng khiến app không khởi động được
}
finally
{
    Log.CloseAndFlush(); // Giải phóng tài nguyên và đẩy hết các dòng log cuối cùng trước khi đóng ứng dụng
}
