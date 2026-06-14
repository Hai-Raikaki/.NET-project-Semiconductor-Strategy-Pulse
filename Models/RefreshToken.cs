using System; // Import kiểu dữ liệu hệ thống cơ bản

namespace SemiconductorStrategyPulse.Models
{
    // Thực thể RefreshToken lưu trữ thông tin các mã token làm mới phiên làm việc trong CSDL
    public class RefreshToken
    {
        public Guid Id { get; set; } // Khóa chính định danh mã Refresh Token
        public string Token { get; set; } = string.Empty; // Chuỗi mã Refresh Token ngẫu nhiên (dạng mã Base64)
        public Guid UserId { get; set; } // ID người dùng sở hữu token này (Khóa ngoại liên kết tới AppUser)
        public AppUser User { get; set; } = null!; // Thuộc tính điều hướng liên kết lấy thông tin thực thể AppUser tương ứng
        public DateTime ExpiresAt { get; set; } // Thời điểm hết hạn của Refresh Token (mặc định là 7 ngày sau khi đăng nhập)
        public bool IsRevoked { get; set; } // Cờ trạng thái thu hồi (chuyển true khi người dùng logout hoặc khi token đã được dùng để đổi lấy cặp token mới)
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow; // Mốc thời gian phát hành Token
    }
}
