using System; // Import thư viện cơ bản của hệ thống .NET
using System.Collections.Generic; // Import kiểu dữ liệu danh sách/tập hợp
using System.IdentityModel.Tokens.Jwt; // Import thư viện xử lý và phát hành JWT Token
using System.Security.Claims; // Import để thiết lập các tuyên bố danh tính (Claims)
using System.Security.Cryptography; // Import thư viện mã hóa bảo mật dùng để sinh token ngẫu nhiên
using System.Text; // Import xử lý bảng mã ký tự (mã hóa UTF-8)
using Microsoft.Extensions.Configuration; // Import cấu hình hệ thống (appsettings.json)
using Microsoft.IdentityModel.Tokens; // Import các lớp bảo mật Token (SymmetricSecurityKey...)
using SemiconductorStrategyPulse.Models; // Import thực thể dữ liệu (Models)

namespace SemiconductorStrategyPulse.Services
{
    // Dịch vụ quản lý mã Token (JWT & Refresh Token)
    public class TokenService : ITokenService
    {
        private readonly IConfiguration _config; // Khai báo đối tượng đọc file cấu hình appsettings.json

        // Constructor tiêm IConfiguration từ DI Container
        public TokenService(IConfiguration config)
        {
            _config = config;
        }

        // Tạo chuỗi mã Access Token (JWT) chứa thông tin người dùng và các vai trò (Roles)
        public string GenerateToken(AppUser user, IList<string> roles)
        {
            // Lấy khóa bí mật Jwt:Key từ file cấu hình, nếu không có thì dùng khóa dự phòng mặc định (yêu cầu khóa đủ độ dài)
            var secretKey = _config["Jwt:Key"] ?? "super_secret_semiconductor_pulse_system_key_2026_long_key_required";
            // Lấy thông tin bên phát hành (Issuer) từ file cấu hình
            var issuer = _config["Jwt:Issuer"] ?? "SemiconductorStrategyPulse";
            // Lấy thông tin đối tượng nhận token (Audience) từ file cấu hình
            var audience = _config["Jwt:Audience"] ?? "SemiconductorPulseClients";

            // Mã hóa khóa bí mật dạng UTF-8 thành mảng byte để làm khóa đối xứng
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            // Cấu hình thông số ký số sử dụng thuật toán mã hóa đối xứng HMAC-SHA256
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            // Thiết lập các thông tin Claims (tuyên bố danh tính) đính kèm bên trong Token
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()), // Subject - ID duy nhất của người dùng
                new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty), // Email của người dùng
                new Claim(ClaimTypes.Name, user.Email ?? string.Empty), // Tên định danh chuẩn cho hệ thống Authorize
                new Claim("FullName", user.FullName), // Họ và tên đầy đủ
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()) // JWT ID - Mã số ID duy nhất của token để chống tấn công Replay
            };

            // Thêm tất cả các vai trò (Roles) của người dùng vào danh sách Claims
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role)); // Mỗi vai trò được thêm dưới dạng Claim Role
            }

            // Khởi tạo đối tượng JWT Security Token với đầy đủ thông số cấu hình
            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(15), // Thời hạn sống ngắn của Access Token: 15 phút để đảm bảo an toàn
                signingCredentials: credentials); // Thông tin chữ ký điện tử xác thực

            // Sử dụng trình xử lý mã JWT để chuyển đổi đối tượng token thành chuỗi ký tự gửi về cho Client
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        // Sinh mã Refresh Token ngẫu nhiên có tính bảo mật cực cao để duy trì phiên đăng nhập lâu dài
        public string GenerateRefreshToken()
        {
            var randomNumber = new byte[32]; // Tạo mảng byte trống kích thước 32 byte (256 bit)
            using var rng = RandomNumberGenerator.Create(); // Tạo bộ sinh số ngẫu nhiên cấp độ mã hóa bảo mật mật mã học
            rng.GetBytes(randomNumber); // Điền các byte ngẫu nhiên an toàn vào mảng
            return Convert.ToBase64String(randomNumber); // Chuyển đổi mảng byte sang chuỗi Base64 thân thiện
        }
    }
}
