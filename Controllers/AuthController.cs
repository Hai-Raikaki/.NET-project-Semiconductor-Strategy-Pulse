using System; // Import các kiểu dữ liệu hệ thống cơ bản của .NET
using System.Collections.Generic; // Import kiểu dữ liệu danh sách/tập hợp
using System.Linq; // Import thư viện LINQ để lọc dữ liệu và ánh xạ danh sách
using System.Security.Claims; // Import để quản lý thông tin danh tính của người dùng (Claims)
using System.Threading.Tasks; // Import hỗ trợ lập trình bất đồng bộ Task
using Microsoft.AspNetCore.Authorization; // Import hỗ trợ kiểm tra quyền truy cập (Authorize)
using Microsoft.AspNetCore.Http; // Import quản lý Cookies và các lớp liên quan HTTP Context
using Microsoft.AspNetCore.Identity; // Import thư viện ASP.NET Core Identity quản lý đăng nhập/đăng ký
using Microsoft.AspNetCore.Mvc; // Import các thư viện MVC Web API
using Microsoft.EntityFrameworkCore; // Import thư viện tương tác CSDL Entity Framework Core
using SemiconductorStrategyPulse.Data; // Import lớp Database Context của dự án
using SemiconductorStrategyPulse.DTOs; // Import các lớp truyền dữ liệu (DTO)
using SemiconductorStrategyPulse.Models; // Import các thực thể dữ liệu (Models)
using SemiconductorStrategyPulse.Services; // Import dịch vụ logic nghiệp vụ (Services)

namespace SemiconductorStrategyPulse.Controllers
{
    [ApiController] // Tự động kiểm tra Model State và đánh dấu đây là API Controller
    [Route("api/[controller]")] // Định nghĩa route cho Controller là: /api/auth
    public class AuthController : ControllerBase
    {
        private readonly UserManager<AppUser> _userManager; // Trình quản lý người dùng của Identity
        private readonly SignInManager<AppUser> _signInManager; // Trình quản lý đăng nhập của Identity
        private readonly PulseDbContext _dbContext; // Database Context để tương tác bảng Token
        private readonly ITokenService _tokenService; // Dịch vụ tạo JWT và Refresh Token

        // Constructor thực hiện tiêm (Inject) các dịch vụ Identity và Token
        public AuthController(
            UserManager<AppUser> userManager,
            SignInManager<AppUser> signInManager,
            PulseDbContext dbContext,
            ITokenService tokenService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _dbContext = dbContext;
            _tokenService = tokenService;
        }

        // API Đăng ký tài khoản người dùng mới
        [HttpPost("register")] // Route: POST /api/auth/register
        [ProducesResponseType(StatusCodes.Status200OK)] // Phản hồi 200 khi đăng ký thành công
        [ProducesResponseType(StatusCodes.Status400BadRequest)] // Phản hồi 400 khi dữ liệu gửi lên lỗi hoặc email trùng
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            if (!ModelState.IsValid) // Nếu dữ liệu gửi lên không đúng định dạng yêu cầu
            {
                return BadRequest(ModelState); // Trả về lỗi định dạng của Model
            }

            // Kiểm tra xem Email đã tồn tại trong hệ thống chưa
            var existingUser = await _userManager.FindByEmailAsync(dto.Email);
            if (existingUser != null) // Nếu email đã được đăng ký trước đó
            {
                return BadRequest(new { success = false, message = "Email is already registered." });
            }

            // Khởi tạo thực thể người dùng mới từ DTO
            var user = new AppUser
            {
                UserName = dto.Email, // Username mặc định lấy theo Email
                Email = dto.Email,
                FullName = dto.FullName,
                EmailConfirmed = true // Mặc định xác nhận email để test nhanh
            };

            // Tạo tài khoản người dùng mới kèm mật khẩu được mã hóa tự động
            var result = await _userManager.CreateAsync(user, dto.Password);
            if (!result.Succeeded) // Nếu quá trình tạo tài khoản thất bại (ví dụ mật khẩu quá yếu)
            {
                // Ghép nối toàn bộ thông điệp lỗi thành một chuỗi
                var errors = string.Join("; ", result.Errors.Select(e => e.Description));
                return BadRequest(new { success = false, message = errors }); // Trả về lỗi cụ thể
            }

            // Gán vai trò (Role) mặc định cho người dùng mới đăng ký là "User"
            await _userManager.AddToRoleAsync(user, "User");

            // Trả về kết quả đăng ký thành công
            return Ok(new { success = true, message = "User registered successfully.", userId = user.Id });
        }

        // API Đăng nhập tài khoản người dùng
        [HttpPost("login")] // Route: POST /api/auth/login
        [ProducesResponseType(StatusCodes.Status200OK)] // Đăng nhập thành công trả về thông tin User
        [ProducesResponseType(StatusCodes.Status401Unauthorized)] // Sai tài khoản mật khẩu hoặc tài khoản bị khóa
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Tìm kiếm thông tin người dùng theo Email
            var user = await _userManager.FindByEmailAsync(dto.Email);
            // Nếu không tìm thấy người dùng hoặc tài khoản bị khóa kích hoạt (IsActive = false)
            if (user == null || !user.IsActive)
            {
                return Unauthorized(new { success = false, message = "Invalid email or password." });
            }

            // Kiểm tra mật khẩu người dùng gửi lên có khớp hay không
            var result = await _signInManager.CheckPasswordSignInAsync(user, dto.Password, lockoutOnFailure: false);
            if (!result.Succeeded) // Nếu mật khẩu không khớp
            {
                return Unauthorized(new { success = false, message = "Invalid email or password." });
            }

            // Lấy danh sách các vai trò (Roles) của người dùng này
            var roles = await _userManager.GetRolesAsync(user);
            var mainRole = roles.FirstOrDefault() ?? "User"; // Lấy vai trò đầu tiên làm mặc định

            // Tạo mã Access Token (JWT) và mã Refresh Token
            var accessToken = _tokenService.GenerateToken(user, roles);
            var refreshTokenValue = _tokenService.GenerateRefreshToken();

            // Cập nhật mốc thời gian đăng nhập cuối cùng của người dùng
            user.LastLoginAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);

            // Khởi tạo thực thể Refresh Token để lưu vào CSDL giúp kiểm soát phiên làm việc
            var refreshTokenEntity = new RefreshToken
            {
                Id = Guid.NewGuid(),
                Token = refreshTokenValue, // Lưu giá trị chuỗi Refresh Token ngẫu nhiên
                UserId = user.Id, // Liên kết tới User vừa đăng nhập
                ExpiresAt = DateTime.UtcNow.AddDays(7), // Hạn dùng Refresh Token là 7 ngày
                IsRevoked = false, // Chưa bị thu hồi
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.RefreshTokens.Add(refreshTokenEntity); // Thêm bản ghi mới vào DbSet
            await _dbContext.SaveChangesAsync(); // Lưu thay đổi xuống Database

            // Thiết lập lưu Access Token và Refresh Token vào Cookie bảo mật HttpOnly
            SetTokenCookies(accessToken, refreshTokenValue);

            // Trả về thông tin đăng nhập thành công dạng JSON cho client
            return Ok(new LoginResponseDto
            {
                Success = true,
                Message = "Logged in successfully.",
                ExpiresIn = 900, // Thời gian hết hạn của Access Token (15 phút tính bằng giây)
                User = new UserResponseDto
                {
                    Id = user.Id,
                    Email = user.Email ?? string.Empty,
                    FullName = user.FullName,
                    Role = mainRole,
                    CreatedAt = user.CreatedAt
                }
            });
        }

        // API làm mới Access Token khi đã hết hạn thông qua Refresh Token
        [HttpPost("refresh-token")] // Route: POST /api/auth/refresh-token
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> RefreshToken()
        {
            // Tìm đọc Refresh Token từ Cookie bảo mật "refresh_token" gửi kèm request
            if (!Request.Cookies.TryGetValue("refresh_token", out var token) || string.IsNullOrEmpty(token))
            {
                return Unauthorized(new { success = false, message = "Refresh token is missing." });
            }

            // Tìm bản ghi Refresh Token tương ứng trong DB kèm theo thông tin User sở hữu
            var storedToken = await _dbContext.RefreshTokens
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Token == token);

            // Kiểm tra tính hợp lệ: Token phải tồn tại, chưa bị thu hồi (IsRevoked = false) và chưa quá hạn sử dụng
            if (storedToken == null || storedToken.IsRevoked || storedToken.ExpiresAt < DateTime.UtcNow)
            {
                return Unauthorized(new { success = false, message = "Invalid or expired refresh token." });
            }

            var user = storedToken.User; // Lấy thông tin người dùng từ token
            // Nếu người dùng bị khóa tài khoản hoặc không tìm thấy
            if (user == null || !user.IsActive)
            {
                return Unauthorized(new { success = false, message = "User account is inactive or not found." });
            }

            // Thực hiện cơ chế xoay vòng Refresh Token (Token Rotation) để tăng tính bảo mật:
            // Thu hồi ngay lập tức Token hiện tại để tránh việc tái sử dụng trái phép
            storedToken.IsRevoked = true;
            _dbContext.RefreshTokens.Update(storedToken);

            // Phát hành các cặp Access Token và Refresh Token mới
            var roles = await _userManager.GetRolesAsync(user);
            var mainRole = roles.FirstOrDefault() ?? "User";
            
            var newAccessToken = _tokenService.GenerateToken(user, roles); // Tạo Access Token mới
            var newRefreshTokenValue = _tokenService.GenerateRefreshToken(); // Tạo Refresh Token mới

            // Lưu bản ghi Refresh Token mới phát hành vào Database
            var newRefreshTokenEntity = new RefreshToken
            {
                Id = Guid.NewGuid(),
                Token = newRefreshTokenValue,
                UserId = user.Id,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                IsRevoked = false,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.RefreshTokens.Add(newRefreshTokenEntity);
            await _dbContext.SaveChangesAsync(); // Lưu thay đổi

            // Thiết lập lại các Cookie mới đè lên Cookie cũ
            SetTokenCookies(newAccessToken, newRefreshTokenValue);

            // Trả về thông tin cập nhật token thành công
            return Ok(new LoginResponseDto
            {
                Success = true,
                Message = "Token refreshed successfully.",
                ExpiresIn = 900,
                User = new UserResponseDto
                {
                    Id = user.Id,
                    Email = user.Email ?? string.Empty,
                    FullName = user.FullName,
                    Role = mainRole,
                    CreatedAt = user.CreatedAt
                }
            });
        }

        // API Đăng xuất tài khoản
        [HttpPost("logout")] // Route: POST /api/auth/logout
        [Authorize] // Yêu cầu phải đang đăng nhập mới có thể gọi API đăng xuất
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Logout()
        {
            // Lấy ID người dùng hiện tại từ danh tính Claims của Token đăng nhập
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(userIdClaim, out var userId))
            {
                // Thu hồi tất cả các Refresh Token đang hoạt động của người dùng này trong Database
                var userTokens = await _dbContext.RefreshTokens
                    .Where(r => r.UserId == userId && !r.IsRevoked)
                    .ToListAsync();

                foreach (var token in userTokens)
                {
                    token.IsRevoked = true; // Thu hồi token
                }
                
                await _dbContext.SaveChangesAsync(); // Lưu thay đổi vào DB
            }

            // Xóa sạch các Cookie lưu token trên trình duyệt
            ClearTokenCookies();

            return Ok(new { success = true, message = "Logged out successfully." });
        }

        // API Lấy thông tin cá nhân của người dùng hiện tại
        [HttpGet("me")] // Route: GET /api/auth/me
        [Authorize] // Yêu cầu phải đăng nhập
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Me()
        {
            // Lấy ID người dùng từ Claims
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new { success = false, message = "Invalid user identity claims." });
            }

            // Tìm thông tin người dùng trong cơ sở dữ liệu
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                return Unauthorized(new { success = false, message = "User not found." });
            }

            // Lấy danh sách Roles
            var roles = await _userManager.GetRolesAsync(user);
            var mainRole = roles.FirstOrDefault() ?? "User";

            // Trả về thông tin DTO của User
            return Ok(new UserResponseDto
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                FullName = user.FullName,
                Role = mainRole,
                CreatedAt = user.CreatedAt
            });
        }

        // Hàm trợ giúp thiết lập Cookie chứa Token gửi về trình duyệt khách
        private void SetTokenCookies(string accessToken, string refreshToken)
        {
            // Cấu hình cookie chứa Access Token
            var accessCookieOptions = new CookieOptions
            {
                HttpOnly = true, // Cấm Javascript của trình duyệt đọc cookie để chống tấn công XSS
                Secure = Request.IsHttps, // Chỉ cho phép gửi qua HTTPS nếu có cấu hình
                SameSite = SameSiteMode.Lax, // Chế độ bảo mật chống CSRF cơ bản
                Expires = DateTime.UtcNow.AddMinutes(15), // Hết hạn sau 15 phút
                Path = "/" // Có hiệu lực trên toàn bộ trang web
            };
            Response.Cookies.Append("access_token", accessToken, accessCookieOptions);

            // Cấu hình cookie chứa Refresh Token
            var refreshCookieOptions = new CookieOptions
            {
                HttpOnly = true, // Cấm đọc bằng Javascript
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Expires = DateTime.UtcNow.AddDays(7), // Hết hạn sau 7 ngày
                Path = "/api/auth" // Chỉ gửi cookie này khi gửi yêu cầu tới các API có tiền tố /api/auth (tối ưu hiệu năng mạng)
            };
            Response.Cookies.Append("refresh_token", refreshToken, refreshCookieOptions);
        }

        // Hàm trợ giúp xóa sạch Cookie chứa Token bằng cách thiết lập mốc thời gian hết hạn trong quá khứ
        private void ClearTokenCookies()
        {
            // Hủy cookie Access Token
            var accessCookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Expires = DateTime.UtcNow.AddDays(-1), // Đặt hạn dùng là ngày hôm qua để trình duyệt xóa ngay lập tức
                Path = "/"
            };
            Response.Cookies.Append("access_token", "", accessCookieOptions);

            // Hủy cookie Refresh Token
            var refreshCookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Expires = DateTime.UtcNow.AddDays(-1), // Đặt ngày hết hạn trong quá khứ
                Path = "/api/auth"
            };
            Response.Cookies.Append("refresh_token", "", refreshCookieOptions);
        }
    }
}
