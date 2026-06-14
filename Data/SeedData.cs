using System; // Import kiểu dữ liệu cơ bản hệ thống
using System.Linq; // Import thư viện LINQ kiểm tra tồn tại phần tử
using System.Threading.Tasks; // Import hỗ trợ lập trình bất đồng bộ Task
using Microsoft.AspNetCore.Identity; // Import các dịch vụ quản lý User/Role của Identity
using Microsoft.EntityFrameworkCore; // Import Entity Framework Core
using SemiconductorStrategyPulse.Models; // Import các thực thể thực bảng cơ sở dữ liệu

namespace SemiconductorStrategyPulse.Data
{
    // Lớp SeedData cung cấp phương thức tĩnh để nạp dữ liệu mẫu ban đầu khi ứng dụng khởi chạy
    public static class SeedData
    {
        // Khởi tạo và nạp dữ liệu mẫu ban đầu cho CSDL (Roles, Users, Strategy Pulse Metrics)
        public static async Task InitializeAsync(
            PulseDbContext context, 
            UserManager<AppUser> userManager, 
            RoleManager<IdentityRole<Guid>> roleManager)
        {
            // 1. Khởi tạo và nạp các Vai trò (Roles) mặc định của hệ thống
            var roles = new[] { "Admin", "IoTDevice", "User" };
            foreach (var role in roles)
            {
                // Nếu vai trò chưa tồn tại trong CSDL
                if (!await roleManager.RoleExistsAsync(role))
                {
                    // Tiến hành tạo vai trò mới với kiểu định danh duy nhất là Guid
                    await roleManager.CreateAsync(new IdentityRole<Guid> { Name = role, NormalizedName = role.ToUpper() });
                }
            }

            // 2. Khởi tạo và nạp Tài khoản Quản trị viên mẫu (Administrator)
            if (await userManager.FindByEmailAsync("admin@pulse.com") == null)
            {
                var adminUser = new AppUser
                {
                    UserName = "admin@pulse.com",
                    Email = "admin@pulse.com",
                    FullName = "System Administrator",
                    EmailConfirmed = true, // Xác nhận email thành công luôn
                    IsActive = true // Kích hoạt tài khoản hoạt động
                };

                // Tạo tài khoản admin với mật khẩu mặc định là 'Admin123'
                var result = await userManager.CreateAsync(adminUser, "Admin123");
                if (result.Succeeded)
                {
                    // Gán vai trò 'Admin' cho tài khoản vừa tạo
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                }
            }

            // 3. Khởi tạo và nạp Tài khoản Thiết bị IoT gửi telemetry mẫu (APAC Node)
            if (await userManager.FindByEmailAsync("device@pulse.com") == null)
            {
                var deviceUser = new AppUser
                {
                    UserName = "device@pulse.com",
                    Email = "device@pulse.com",
                    FullName = "APAC Ingestion Node 01",
                    EmailConfirmed = true,
                    IsActive = true
                };

                // Tạo tài khoản thiết bị với mật khẩu mặc định là 'Device123'
                var result = await userManager.CreateAsync(deviceUser, "Device123");
                if (result.Succeeded)
                {
                    // Gán vai trò thiết bị nhận dữ liệu 'IoTDevice'
                    await userManager.AddToRoleAsync(deviceUser, "IoTDevice");
                }
            }

            // 4. Khởi tạo và nạp các chỉ số chiến lược mặc định nếu bảng StrategyMetricPulses trống trơn
            if (!await context.StrategyMetricPulses.AnyAsync())
            {
                var initialPulses = new[]
                {
                    // Chỉ số 1: Tỷ lệ cung cầu bán dẫn toàn cầu
                    new StrategyMetricPulse
                    {
                        Id = Guid.NewGuid(),
                        MetricName = "Global Supply-Demand Balance (Book-to-Bill Ratio)",
                        MetricKey = "BOOK_TO_BILL",
                        Category = "SUPPLY_CHAIN",
                        Value = 1.12, // Tỷ lệ > 1.0 nghĩa là nhu cầu đang vượt xa nguồn cung
                        StandardDeviation = 0.05,
                        SampleCount = 100,
                        ChangeRate = 2.4, // Biến động tăng 2.4% so với kỳ trước
                        PulseStatus = "GROWING",
                        LastUpdated = DateTimeOffset.UtcNow
                    },
                    // Chỉ số 2: Thời gian chờ giao hàng trung bình
                    new StrategyMetricPulse
                    {
                        Id = Guid.NewGuid(),
                        MetricName = "Average Lead Time (Weeks)",
                        MetricKey = "LEAD_TIME",
                        Category = "SUPPLY_CHAIN",
                        Value = 18.5, // 18.5 tuần từ lúc đặt hàng đến khi nhận chip
                        StandardDeviation = 1.2,
                        SampleCount = 100,
                        ChangeRate = -1.5, // Giảm 1.5% (dấu hiệu chuỗi cung ứng đang cải thiện tốt lên)
                        PulseStatus = "STABLE",
                        LastUpdated = DateTimeOffset.UtcNow
                    },
                    // Chỉ số 3: Tỷ lệ tấm bán dẫn wafer đạt chuẩn
                    new StrategyMetricPulse
                    {
                        Id = Guid.NewGuid(),
                        MetricName = "Advanced Node Yield Rate (%)",
                        MetricKey = "YIELD_RATE",
                        Category = "CAPACITY",
                        Value = 74.2, // Tỷ lệ wafer đạt chuẩn trung bình ở các bóng bán dẫn dưới 3nm
                        StandardDeviation = 3.5,
                        SampleCount = 100,
                        ChangeRate = 0.8, // Tăng nhẹ 0.8% sản lượng
                        PulseStatus = "STABLE",
                        LastUpdated = DateTimeOffset.UtcNow
                    },
                    // Chỉ số 4: Chi phí đầu tư tài sản cố định
                    new StrategyMetricPulse
                    {
                        Id = Guid.NewGuid(),
                        MetricName = "CapEx Momentum (Billion USD)",
                        MetricKey = "CAPEX",
                        Category = "FINANCIAL",
                        Value = 38.6, // Đầu tư tài chính đạt 38.6 tỷ USD
                        StandardDeviation = 4.1,
                        SampleCount = 100,
                        ChangeRate = 5.2, // Tăng trưởng đầu tư đạt 5.2%
                        PulseStatus = "GROWING",
                        LastUpdated = DateTimeOffset.UtcNow
                    }
                };

                // Thêm danh sách chỉ số mẫu vào DbSet tương ứng
                await context.StrategyMetricPulses.AddRangeAsync(initialPulses);
            }

            // Lưu toàn bộ thay đổi dữ liệu nạp mẫu xuống CSDL thực tế
            await context.SaveChangesAsync();
        }
    }
}

