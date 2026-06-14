using System; // Import các kiểu dữ liệu cơ bản của hệ thống .NET
using Microsoft.AspNetCore.Identity; // Import thư viện IdentityUser để quản lý người dùng mặc định

namespace SemiconductorStrategyPulse.Models
{
    // Thực thể AppUser đại diện cho thông tin tài khoản người dùng đăng nhập hệ thống
    // Kế thừa từ IdentityUser<Guid> sử dụng kiểu định danh khóa chính Guid thay cho chuỗi string mặc định
    public class AppUser : IdentityUser<Guid>
    {
        public string FullName { get; set; } = string.Empty; // Họ tên đầy đủ của người dùng
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow; // Thời điểm tạo tài khoản mẫu mặc định hiện tại
        public DateTime? LastLoginAt { get; set; } // Thời điểm đăng nhập thành công gần nhất (có thể null nếu chưa từng đăng nhập)
        public bool IsActive { get; set; } = true; // Cờ hoạt động của tài khoản (mặc định hoạt động, chuyển false để khóa tài khoản)
    }
}
