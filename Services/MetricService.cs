using System; // Import các kiểu dữ liệu hệ thống cơ bản của .NET
using System.Collections.Generic; // Import các cấu trúc tập hợp danh sách dữ liệu
using System.Linq; // Import thư viện LINQ để tính trung bình, phương sai, độ lệch chuẩn
using System.Threading.Tasks; // Import hỗ trợ lập trình bất đồng bộ Task
using Microsoft.EntityFrameworkCore; // Import Entity Framework Core để tương tác với cơ sở dữ liệu
using Microsoft.Extensions.Logging; // Import thư viện ghi log hệ thống
using SemiconductorStrategyPulse.Caching; // Import dịch vụ quản lý Cache (Redis/Memory Cache)
using SemiconductorStrategyPulse.Data; // Import Context kết nối Database
using SemiconductorStrategyPulse.Models; // Import các thực thể dữ liệu (Model)

namespace SemiconductorStrategyPulse.Services
{
    // MetricService triển khai IMetricService, đảm nhận nhiệm vụ truy vấn, cập nhật và tính toán lại các chỉ số bán dẫn
    public class MetricService : IMetricService
    {
        private readonly PulseDbContext _dbContext; // Khai báo Context kết nối Database
        private readonly ICacheService _cacheService; // Khai báo dịch vụ Cache L2
        private readonly ILogger<MetricService> _logger; // Khai báo Logger để ghi log tiến trình tính toán

        // Định nghĩa Key dùng chung để cache toàn bộ danh sách chỉ số
        private const string CacheKeyAll = "metrics:all";
        
        // Sự kiện tĩnh lưu trữ danh sách các hàm xử lý khi một chỉ số được cập nhật thành công (Event Backing Field)
        private static event Action<StrategyMetricPulse>? _onMetricUpdated;

        // Định nghĩa sự kiện công khai (public event) để các Controller có thể đăng ký lắng nghe (Subscribe)
        public event Action<StrategyMetricPulse>? OnMetricUpdated
        {
            add => _onMetricUpdated += value; // Đăng ký sự kiện
            remove => _onMetricUpdated -= value; // Hủy đăng ký sự kiện
        }

        // Constructor thực hiện tiêm các Dependency cần thiết vào lớp dịch vụ
        public MetricService(
            PulseDbContext dbContext,
            ICacheService cacheService,
            ILogger<MetricService> logger)
        {
            _dbContext = dbContext; // Gán DbContext
            _cacheService = cacheService; // Gán Cache Service
            _logger = logger; // Gán Logger
        }

        // Lấy tất cả chỉ số chiến lược. Ưu tiên lấy từ bộ nhớ Cache, nếu không có thì đọc từ Database
        public async Task<IEnumerable<StrategyMetricPulse>> GetAllMetricsAsync()
        {
            // Kiểm tra xem dữ liệu đã có trong Cache L2 (Redis hoặc In-Memory) chưa
            var cached = await _cacheService.GetAsync<List<StrategyMetricPulse>>(CacheKeyAll);
            if (cached != null) // Nếu có trong Cache (Cache Hit)
            {
                _logger.LogInformation("L2 Cache HIT for: {CacheKey}", CacheKeyAll); // Ghi log cache hit
                return cached; // Trả về kết quả từ Cache luôn
            }

            // Nếu không có trong Cache (Cache Miss)
            _logger.LogInformation("L2 Cache MISS for: {CacheKey}. Querying DB.", CacheKeyAll); // Ghi log cache miss
            // Truy vấn lấy toàn bộ chỉ số từ Database
            var metrics = await _dbContext.StrategyMetricPulses.ToListAsync();
            
            // Lưu kết quả vừa lấy được vào Cache với thời gian hết hạn là 10 phút
            await _cacheService.SetAsync(CacheKeyAll, metrics, TimeSpan.FromMinutes(10));
            return metrics; // Trả về danh sách chỉ số
        }

        // Lấy thông tin một chỉ số cụ thể theo Key (ví dụ: YIELD_RATE)
        public async Task<StrategyMetricPulse?> GetMetricByKeyAsync(string key)
        {
            var cacheKey = $"metrics:key:{key}"; // Thiết lập cache key riêng cho từng Key chỉ số
            // Kiểm tra xem chỉ số này đã được cache chưa
            var cached = await _cacheService.GetAsync<StrategyMetricPulse>(cacheKey);
            if (cached != null) // Cache Hit
            {
                _logger.LogInformation("L2 Cache HIT for: {CacheKey}", cacheKey);
                return cached; // Trả về dữ liệu cache
            }

            // Cache Miss
            _logger.LogInformation("L2 Cache MISS for: {CacheKey}. Querying DB.", cacheKey);
            // Tìm bản ghi đầu tiên khớp với Key chỉ số trong Database
            var metric = await _dbContext.StrategyMetricPulses.FirstOrDefaultAsync(m => m.MetricKey == key);
            if (metric != null) // Nếu tìm thấy bản ghi trong DB
            {
                // Cất vào Cache để lần sau đọc nhanh hơn (hạn dùng 10 phút)
                await _cacheService.SetAsync(cacheKey, metric, TimeSpan.FromMinutes(10));
            }
            return metric; // Trả về kết quả
        }

        // Tính toán lại giá trị thống kê cho một chỉ số cụ thể dựa trên lịch sử dữ liệu thô
        public async Task RecalculateMetricAsync(string key)
        {
            _logger.LogInformation("Recalculating strategy metric for key: {Key}", key); // Log bắt đầu tính toán lại

            // 1. Truy vấn lịch sử dữ liệu thô: Lấy tối đa 500 bản ghi gần nhất để tính toán nhằm tối ưu hiệu năng DB
            var values = await _dbContext.RawMarketData
                .Where(r => r.MetricKey == key) // Lọc theo đúng mã Key chỉ số
                .OrderByDescending(r => r.Timestamp) // Sắp xếp thời gian mới nhất lên đầu
                .Take(500) // Chỉ lấy 500 điểm dữ liệu
                .Select(r => r.Value) // Chỉ lấy cột giá trị số
                .ToListAsync();

            if (!values.Any()) // Nếu không tìm thấy bất kỳ điểm dữ liệu thô nào
            {
                _logger.LogWarning("No raw telemetry data points found for key: {Key}", key); // Ghi log cảnh báo
                return; // Kết thúc hàm sớm
            }

            // 2. Tính toán các chỉ số thống kê toán học
            double mean = values.Average(); // Tính giá trị trung bình (Mean)
            // Tính phương sai (Variance) = Trung bình của tổng bình phương hiệu (giá trị - trung bình)
            double variance = values.Select(v => Math.Pow(v - mean, 2)).Average();
            double stdDev = Math.Sqrt(variance); // Tính độ lệch chuẩn (Standard Deviation) = Căn bậc hai của phương sai
            int sampleCount = values.Count; // Lấy tổng số lượng mẫu thu thập được

            // 3. Tìm bản ghi chỉ số tương ứng trong bảng StrategyMetricPulses
            var metric = await _dbContext.StrategyMetricPulses.FirstOrDefaultAsync(m => m.MetricKey == key);
            bool isNew = false; // Cờ đánh dấu bản ghi mới
            double oldVal = mean; // Giá trị cũ ban đầu (nếu là mới thì mặc định bằng mean để tỷ lệ thay đổi là 0%)

            if (metric == null) // Nếu chưa tồn tại chỉ số này trong bảng tổng hợp
            {
                isNew = true; // Đánh dấu là bản ghi mới
                metric = new StrategyMetricPulse // Khởi tạo thực thể chỉ số mới
                {
                    MetricKey = key,
                    MetricName = GetDefaultMetricName(key), // Lấy tên mặc định dựa vào mã Key
                    Category = GetDefaultCategory(key) // Lấy danh mục mặc định dựa vào mã Key
                };
            }
            else // Nếu đã có chỉ số này trong DB
            {
                oldVal = metric.Value; // Lưu lại giá trị cũ của chỉ số trước khi cập nhật
            }

            // 4. Tính toán phần trăm tỷ lệ thay đổi (Change Rate) so với giá trị cũ trước đó
            double changeRate = 0.0;
            if (oldVal != 0.0) // Tránh lỗi chia cho 0
            {
                changeRate = ((mean - oldVal) / oldVal) * 100; // Công thức tính tỷ lệ phần trăm thay đổi
            }

            // 5. Xác định trạng thái hệ thống (STABLE, GROWING, DECLINING, CRITICAL)
            string status = DeterminePulseStatus(key, mean, stdDev, changeRate);

            // Gán các giá trị thống kê mới đã làm tròn vào thực thể chỉ số
            metric.Value = Math.Round(mean, 4); // Làm tròn giá trị trung bình đến 4 chữ số thập phân
            metric.StandardDeviation = Math.Round(stdDev, 4); // Làm tròn độ lệch chuẩn đến 4 chữ số thập phân
            metric.SampleCount = sampleCount; // Lưu số lượng mẫu
            metric.ChangeRate = Math.Round(changeRate, 2); // Làm tròn phần trăm thay đổi đến 2 chữ số thập phân
            metric.PulseStatus = status; // Cập nhật trạng thái
            metric.LastUpdated = DateTimeOffset.UtcNow; // Ghi nhận thời điểm cập nhật mới nhất

            if (isNew) // Nếu là bản ghi chỉ số mới tinh
            {
                await _dbContext.StrategyMetricPulses.AddAsync(metric); // Thêm mới vào DbSet
            }
            else // Nếu là bản ghi cũ
            {
                _dbContext.StrategyMetricPulses.Update(metric); // Đánh dấu cập nhật bản ghi
            }

            await _dbContext.SaveChangesAsync(); // Lưu thay đổi xuống cơ sở dữ liệu thực tế

            // 6. Hủy Cache cũ (Cache Invalidation) để đảm bảo dữ liệu mới nhất được phản ánh
            await _cacheService.RemoveAsync($"metrics:key:{key}"); // Xóa cache của riêng chỉ số này
            await _cacheService.RemoveAsync(CacheKeyAll); // Xóa cache danh sách tất cả chỉ số

            // Ghi đè ngay lập tức dữ liệu chỉ số mới vào Cache (Mẫu thiết kế Write-Through Cache)
            await _cacheService.SetAsync($"metrics:key:{key}", metric, TimeSpan.FromMinutes(10));

            _logger.LogInformation("Strategy metric recalculated: {Key} = {Value} (StdDev: {StdDev}, Status: {Status})", 
                key, metric.Value, metric.StandardDeviation, status); // Log kết quả tính toán chi tiết

            // 7. Kích hoạt sự kiện tĩnh để phát sóng dữ liệu mới nhất tới các kết nối SSE đang mở
            _onMetricUpdated?.Invoke(metric);
        }

        // Phương thức trợ giúp lấy tên mô tả thân thiện của chỉ số từ mã Key
        private string GetDefaultMetricName(string key)
        {
            return key.ToUpper() switch
            {
                "BOOK_TO_BILL" => "Global Supply-Demand Balance (Book-to-Bill Ratio)", // Tỷ lệ đặt hàng trên giao hàng
                "LEAD_TIME" => "Average Lead Time (Weeks)", // Thời gian bàn giao trung bình (tuần)
                "YIELD_RATE" => "Advanced Node Yield Rate (%)", // Tỷ lệ sản xuất đạt chuẩn của tấm bán dẫn wafer (%)
                "CAPEX" => "CapEx Momentum (Billion USD)", // Tổng đầu tư tài sản cố định (Tỷ USD)
                _ => $"{key} Strategic Indicator" // Mặc định nếu là Key lạ
            };
        }

        // Phương thức trợ giúp phân loại danh mục của chỉ số dựa vào mã Key
        private string GetDefaultCategory(string key)
        {
            return key.ToUpper() switch
            {
                "BOOK_TO_BILL" or "LEAD_TIME" => "SUPPLY_CHAIN", // Thuộc chuỗi cung ứng
                "YIELD_RATE" => "CAPACITY", // Thuộc năng lực sản xuất
                "CAPEX" => "FINANCIAL", // Thuộc tài chính đầu tư
                _ => "GENERAL" // Danh mục chung khác
            };
        }

        // Thuật toán xác định trạng thái xung nhịp bán dẫn dựa trên giá trị trung bình, độ lệch chuẩn và tỷ lệ thay đổi
        private string DeterminePulseStatus(string key, double val, double stdDev, double changeRate)
        {
            // Tính Hệ số biến thiên (Coefficient of Variation - CV) = Độ lệch chuẩn / Trị tuyệt đối giá trị trung bình
            double cv = val != 0 ? stdDev / Math.Abs(val) : 0;
            
            // Nếu độ biến thiên quá lớn (vượt ngưỡng 20% đối với tỷ lệ sản lượng wafer YIELD_RATE)
            if (cv > 0.20 && key == "YIELD_RATE")
            {
                return "CRITICAL"; // Báo động khẩn cấp: Sản lượng chế tạo chip bị mất ổn định nghiêm trọng
            }
            // Đối với các chỉ số khác, nếu độ biến thiên vượt quá 30%
            if (cv > 0.30)
            {
                return "CRITICAL"; // Trạng thái khẩn cấp, biến động thị trường quá hỗn loạn
            }

            // Quy tắc logic riêng cho tỷ lệ đặt hàng/giao hàng (Book-to-Bill Ratio)
            if (key.ToUpper() == "BOOK_TO_BILL")
            {
                if (val > 1.15) return "GROWING"; // Boom: Đơn đặt hàng tăng mạnh, thị trường đang bùng nổ
                if (val < 0.90) return "DECLINING"; // Recession: Nhu cầu sụt giảm sâu, dấu hiệu suy thoái kinh tế
                return "STABLE"; // Ổn định quanh mức cân bằng ~1.0
            }

            // Quy tắc chung cho các chỉ số khác dựa vào biên độ thay đổi của đợt tính toán gần nhất
            if (Math.Abs(changeRate) > 3.0) // Nếu biến động tăng hoặc giảm mạnh hơn 3%
            {
                return changeRate > 0 ? "GROWING" : "DECLINING"; // Đang tăng mạnh hoặc đang giảm sút
            }

            return "STABLE"; // Trạng thái ổn định bình thường
        }
    }
}
