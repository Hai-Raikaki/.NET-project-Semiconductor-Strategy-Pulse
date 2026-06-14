using System; // Import thư viện cơ bản hệ thống
using System.Text.Json; // Import thư viện xử lý chuyển đổi JSON
using System.Threading.Tasks; // Import hỗ trợ lập trình bất đồng bộ Task
using Microsoft.Extensions.Caching.Distributed; // Import giao diện Cache phân tán mặc định của ASP.NET Core
using Microsoft.Extensions.Logging; // Import thư viện ghi log hệ thống

namespace SemiconductorStrategyPulse.Caching
{
    // Lớp triển khai ICacheService sử dụng bộ nhớ đệm phân tán Redis (hoặc Memory Cache fallback)
    public class RedisCacheService : ICacheService
    {
        private readonly IDistributedCache _distributedCache; // Bộ quản lý Distributed Cache
        private readonly ILogger<RedisCacheService> _logger; // Đối tượng ghi log lỗi cache

        // Constructor thực hiện tiêm (Inject) dịch vụ Distributed Cache và Logger
        public RedisCacheService(IDistributedCache distributedCache, ILogger<RedisCacheService> logger)
        {
            _distributedCache = distributedCache;
            _logger = logger;
        }

        // Lấy dữ liệu kiểu T được lưu trữ trong cache tương ứng với khóa Key
        public async Task<T?> GetAsync<T>(string key)
        {
            try
            {
                // Truy vấn lấy chuỗi JSON được lưu dưới dạng string từ Redis
                var cachedData = await _distributedCache.GetStringAsync(key);
                if (string.IsNullOrEmpty(cachedData)) // Nếu không có dữ liệu (Cache Miss)
                {
                    return default; // Trả về giá trị mặc định của kiểu T (null...)
                }
                // Giải tuần tự hóa (deserialize) chuỗi JSON lấy được về đối tượng kiểu T
                return JsonSerializer.Deserialize<T>(cachedData);
            }
            catch (Exception ex)
            {
                // Bắt ngoại lệ nếu Redis bị mất kết nối hoặc sập: Log lỗi và trả về mặc định để ứng dụng tự động truy vấn trực tiếp xuống DB (Graceful Fallback)
                _logger.LogError(ex, "Error reading from Redis cache for key: {Key}. Falling back to DB.", key);
                return default;
            }
        }

        // Lưu trữ giá trị kiểu T vào cache tương ứng với khóa Key, hỗ trợ cấu hình thời gian hết hạn tuyệt đối
        public async Task SetAsync<T>(string key, T value, TimeSpan? absoluteExpiration = null)
        {
            try
            {
                // Khởi tạo tùy chọn cấu hình hạn sử dụng của cache
                var options = new DistributedCacheEntryOptions();
                if (absoluteExpiration.HasValue) // Nếu có truyền vào thời gian hết hạn cụ thể
                {
                    options.AbsoluteExpirationRelativeToNow = absoluteExpiration.Value; // Áp dụng hạn dùng truyền vào
                }
                else
                {
                    options.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10); // Hạn dùng mặc định của L2 cache là 10 phút
                }

                // Tuần tự hóa (serialize) đối tượng kiểu T thành chuỗi JSON dạng string
                var json = JsonSerializer.Serialize(value);
                // Lưu chuỗi JSON vào cache phân tán theo Key kèm cấu hình hạn dùng
                await _distributedCache.SetStringAsync(key, json, options);
            }
            catch (Exception ex)
            {
                // Bắt lỗi ghi cache (ví dụ Redis sập): Log lỗi lại để theo dõi hệ thống
                _logger.LogError(ex, "Error writing to Redis cache for key: {Key}", key);
            }
        }

        // Xóa sạch bản ghi trong cache tương ứng với khóa Key
        public async Task RemoveAsync(string key)
        {
            try
            {
                // Gọi lệnh xóa phần tử theo Key trong bộ quản lý cache phân tán
                await _distributedCache.RemoveAsync(key);
            }
            catch (Exception ex)
            {
                // Log lỗi nếu thao tác xóa gặp lỗi kết nối Redis
                _logger.LogError(ex, "Error clearing Redis cache key: {Key}", key);
            }
        }
    }
}
