using System; // Import thư viện cơ bản hệ thống
using System.Threading.Tasks; // Import hỗ trợ lập trình bất đồng bộ Task

namespace SemiconductorStrategyPulse.Caching
{
    // Giao diện (Interface) định nghĩa các thao tác lưu trữ và truy vấn trên bộ nhớ đệm Cache
    public interface ICacheService
    {
        // Lấy dữ liệu kiểu T được lưu trữ trong cache tương ứng với khóa Key
        Task<T?> GetAsync<T>(string key);
        
        // Lưu trữ giá trị kiểu T vào cache tương ứng với khóa Key, hỗ trợ cấu hình thời gian hết hạn tuyệt đối
        Task SetAsync<T>(string key, T value, TimeSpan? absoluteExpiration = null);
        
        // Xóa sạch bản ghi trong cache tương ứng với khóa Key
        Task RemoveAsync(string key);
    }
}
