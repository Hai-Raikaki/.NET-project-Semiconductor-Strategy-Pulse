using System.Collections.Generic; // Import các thư viện tập hợp danh sách
using SemiconductorStrategyPulse.Models; // Import thực thể dữ liệu (Models)

namespace SemiconductorStrategyPulse.Services
{
    // Giao diện (Interface) định nghĩa dịch vụ tạo và làm mới Token xác thực người dùng
    public interface ITokenService
    {
        // Tạo chuỗi Access Token JWT từ đối tượng người dùng và danh sách vai trò
        string GenerateToken(AppUser user, IList<string> roles);
        
        // Sinh chuỗi Refresh Token ngẫu nhiên bảo mật cao để lưu cơ sở dữ liệu
        string GenerateRefreshToken();
    }
}
