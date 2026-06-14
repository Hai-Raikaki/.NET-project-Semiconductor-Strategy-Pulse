using System.Collections.Generic; // Import các thư viện cấu trúc danh sách
using System.Linq; // Import LINQ hỗ trợ tìm phần tử cuối cùng (.Last())
using Microsoft.AspNetCore.Mvc; // Import thư viện hỗ trợ xây dựng API (ControllerBase, HttpGet...)
using Microsoft.Extensions.Caching.Memory; // Import thư viện Memory Cache
using SemiconductorStrategyPulse.Models; // Import thực thể dữ liệu (Models)
using SemiconductorStrategyPulse.Services; // Import dịch vụ logic nghiệp vụ

namespace SemiconductorStrategyPulse.Controllers
{
    [ApiController] // Đánh dấu là một Web API Controller hỗ trợ xác thực dữ liệu gửi lên tự động
    [Route("api/[controller]")] // Route cơ sở: /api/market
    public class MarketController : ControllerBase
    {
        private readonly IMemoryCache _cache; // Bộ nhớ đệm RAM tạm thời lưu trữ lịch sử biến động thị trường

        // Constructor thực hiện tiêm (Inject) dịch vụ IMemoryCache
        public MarketController(IMemoryCache cache)
        {
            _cache = cache;
        }

        /// <summary>
        /// Lấy điểm dữ liệu giá cổ phiếu bán dẫn mới nhất.
        /// GET /api/market/pulse
        /// </summary>
        [HttpGet("pulse")]
        public IActionResult GetPulse()
        {
            // Thử lấy danh sách lịch sử giá được mô phỏng từ Memory Cache
            if (_cache.TryGetValue(MarketDataSimulator.CacheKeyHistory, out List<MarketDataPoint>? history) && history != null && history.Any())
            {
                var latest = history.Last(); // Lấy điểm dữ liệu mới nhất (nằm ở cuối danh sách cửa sổ trượt)
                return Ok(new // Trả về thông tin giá cổ phiếu mới nhất dạng JSON viết thường
                {
                    nvda = latest.Nvda,
                    tsm = latest.Tsm,
                    sox = latest.Sox
                });
            }

            // Phương án dự phòng (fallback) nếu cache chưa kịp tải
            return Ok(new { nvda = 450.0, tsm = 100.0, sox = 3500.0 });
        }

        /// <summary>
        /// Lấy lịch sử biến động của 20 điểm dữ liệu gần nhất (phục vụ vẽ biểu đồ).
        /// GET /api/market/history
        /// </summary>
        [HttpGet("history")]
        public IActionResult GetHistory()
        {
            // Đọc lịch sử 20 điểm dữ liệu biến động từ Memory Cache
            if (_cache.TryGetValue(MarketDataSimulator.CacheKeyHistory, out List<MarketDataPoint>? history) && history != null)
            {
                return Ok(history); // Trả về toàn bộ danh sách lịch sử dạng HTTP 200 OK
            }

            // Nếu không tìm thấy dữ liệu trong cache, trả về danh sách trống
            return Ok(new List<MarketDataPoint>());
        }
    }
}
