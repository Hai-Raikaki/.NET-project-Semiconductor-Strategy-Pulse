using System; // Import các kiểu dữ liệu cơ bản của hệ thống .NET
using System.ComponentModel.DataAnnotations; // Import thư viện hỗ trợ validate định dạng dữ liệu đầu vào (Data Annotations)

namespace SemiconductorStrategyPulse.DTOs
{
    // Đối tượng truyền dữ liệu đăng ký tài khoản (Register Request)
    public class RegisterDto
    {
        [Required] // Bắt buộc nhập trường này
        [EmailAddress] // Phải tuân thủ đúng định dạng của địa chỉ Email
        public string Email { get; set; } = string.Empty;
 
        [Required] // Bắt buộc nhập
        [MinLength(6)] // Chiều dài mật khẩu tối thiểu phải từ 6 ký tự trở lên
        public string Password { get; set; } = string.Empty;
 
        [Required] // Bắt buộc nhập
        [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")] // Kiểm tra xem ConfirmPassword có trùng khớp hoàn toàn với Password hay không
        public string ConfirmPassword { get; set; } = string.Empty;
 
        [Required] // Bắt buộc nhập
        public string FullName { get; set; } = string.Empty; // Họ và tên đầy đủ của người đăng ký
    }
 
    // Đối tượng truyền dữ liệu đăng nhập tài khoản (Login Request)
    public class LoginDto
    {
        [Required] // Bắt buộc nhập
        [EmailAddress] // Phải là địa chỉ Email hợp lệ
        public string Email { get; set; } = string.Empty;
 
        [Required] // Bắt buộc nhập
        public string Password { get; set; } = string.Empty;
    }
 
    // Đối tượng DTO chứa thông tin phản hồi của người dùng về Client sau khi xác thực thành công
    public class UserResponseDto
    {
        public Guid Id { get; set; } // Khóa chính Id duy nhất của User
        public string Email { get; set; } = string.Empty; // Email đăng ký
        public string FullName { get; set; } = string.Empty; // Họ tên đầy đủ hiển thị trên UI
        public string Role { get; set; } = string.Empty; // Quyền hạn chính (Admin, User...)
        public DateTime CreatedAt { get; set; } // Ngày tạo tài khoản
    }
 
    // Đối tượng DTO chứa toàn bộ kết quả phản hồi đăng nhập/làm mới Token gửi về Client
    public class LoginResponseDto
    {
        public bool Success { get; set; } // Trạng thái thành công hay thất bại (true/false)
        public string Message { get; set; } = string.Empty; // Chuỗi thông báo kết quả thân thiện
        public int ExpiresIn { get; set; } // Thời hạn sống của token tính bằng giây (ví dụ: 900 giây tương đương 15 phút)
        public UserResponseDto? User { get; set; } // Thông tin người dùng kèm theo (hoặc null nếu đăng nhập thất bại)
    }
}
