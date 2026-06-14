using System; // Import các kiểu dữ liệu cơ bản của .NET
using System.Collections.Generic; // Import kiểu dữ liệu tập hợp danh sách
using System.Linq; // Import thư viện LINQ để thực hiện các bộ lọc và ánh xạ DTO
using System.Threading; // Import thư viện hỗ trợ quản lý đa luồng và CancellationToken cho luồng SSE
using System.Threading.Tasks; // Import thư viện hỗ trợ lập trình bất đồng bộ Task
using Microsoft.AspNetCore.Authorization; // Import thư viện bảo mật và phân quyền (Roles/Policy)
using Microsoft.AspNetCore.Http; // Import thư viện quản lý HTTP context và HTTP status codes
using Microsoft.AspNetCore.Mvc; // Import các thư viện MVC Core như ControllerBase, Route, HttpGet/HttpPost
using Microsoft.AspNetCore.RateLimiting; // Import thư viện giới hạn tần suất yêu cầu (Rate Limiting)
using SemiconductorStrategyPulse.DTOs; // Import các lớp truyền dữ liệu (DTO)
using SemiconductorStrategyPulse.Models; // Import các lớp thực thể dữ liệu (Models)
using SemiconductorStrategyPulse.Services; // Import các Service nghiệp vụ

namespace SemiconductorStrategyPulse.Controllers
{
    [ApiController] // Đánh dấu đây là một API Controller có hỗ trợ tự động xác thực model (ModelState validation)
    [Route("api/[controller]")] // Định nghĩa đường dẫn API mặc định dạng /api/pulse
    public class PulseController : ControllerBase
    {
        // Khai báo dịch vụ quản lý chỉ số chiến lược
        private readonly IMetricService _metricService;
        // Khai báo dịch vụ nhận dữ liệu telemetry thô
        private readonly IIngestionService _ingestionService;

        // Constructor thực hiện tiêm (Inject) các dịch vụ cần thiết vào Controller
        public PulseController(IMetricService metricService, IIngestionService ingestionService)
        {
            _metricService = metricService; // Gán dịch vụ quản lý chỉ số
            _ingestionService = ingestionService; // Gán dịch vụ nhận telemetry
        }

        /// <summary>
        /// Lấy toàn bộ các chỉ số chiến lược đã tổng hợp. Mặc định lấy từ L2 Cache (Redis).
        /// </summary>
        [HttpGet("metrics")] // Định nghĩa API dạng GET: /api/pulse/metrics
        [EnableRateLimiting("read-policy")] // Áp dụng chính sách giới hạn lượt đọc cấu hình ở Program.cs
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<MetricPulseDto>))] // Định nghĩa kiểu dữ liệu phản hồi thành công
        public async Task<IActionResult> GetMetrics()
        {
            var metrics = await _metricService.GetAllMetricsAsync(); // Lấy danh sách chỉ số từ DB hoặc Cache
            
            // Ánh xạ danh sách thực thể DB sang danh sách DTO để gửi về cho Client
            var dtos = metrics.Select(m => new MetricPulseDto
            {
                MetricName = m.MetricName,
                MetricKey = m.MetricKey,
                Category = m.Category,
                Value = m.Value,
                StandardDeviation = m.StandardDeviation,
                SampleCount = m.SampleCount,
                ChangeRate = m.ChangeRate,
                PulseStatus = m.PulseStatus,
                LastUpdated = m.LastUpdated
            });

            return Ok(dtos); // Trả về HTTP 200 kèm danh sách chỉ số dạng JSON
        }

        /// <summary>
        /// Lấy thông tin một chỉ số chiến lược cụ thể theo Key (ví dụ: YIELD_RATE).
        /// </summary>
        [HttpGet("metrics/{key}")] // Định nghĩa API dạng GET: /api/pulse/metrics/{key}
        [EnableRateLimiting("read-policy")] // Áp dụng chính sách giới hạn lượt đọc
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(MetricPulseDto))] // Phản hồi 200 khi thành công
        [ProducesResponseType(StatusCodes.Status404NotFound)] // Phản hồi 404 khi không tìm thấy Key tương ứng
        public async Task<IActionResult> GetMetricByKey(string key)
        {
            var metric = await _metricService.GetMetricByKeyAsync(key.ToUpper()); // Lấy chỉ số cụ thể từ service theo dạng in hoa
            if (metric == null) // Nếu không tìm thấy
            {
                // Trả về HTTP 404 kèm thông báo lỗi
                return NotFound(new { message = $"Metric with key '{key}' not found." });
            }

            // Ánh xạ thực thể DB sang DTO
            var dto = new MetricPulseDto
            {
                MetricName = metric.MetricName,
                MetricKey = metric.MetricKey,
                Category = metric.Category,
                Value = metric.Value,
                StandardDeviation = metric.StandardDeviation,
                SampleCount = metric.SampleCount,
                ChangeRate = metric.ChangeRate,
                PulseStatus = metric.PulseStatus,
                LastUpdated = metric.LastUpdated
            };

            return Ok(dto); // Trả về HTTP 200 kèm thông tin chỉ số
        }

        /// <summary>
        /// API tiếp nhận điểm dữ liệu đo lường từ xa (telemetry) mới từ máy thiết bị hoặc quản trị viên.
        /// </summary>
        [HttpPost("ingest")] // Định nghĩa API dạng POST: /api/pulse/ingest
        [Authorize(Roles = "IoTDevice,Admin")] // Yêu cầu người dùng phải đăng nhập và có quyền 'IoTDevice' hoặc 'Admin'
        [EnableRateLimiting("write-policy")] // Áp dụng chính sách giới hạn lượt ghi (write limit)
        [ProducesResponseType(StatusCodes.Status202Accepted)] // Trả về 202 khi dữ liệu được xếp hàng đợi thành công
        [ProducesResponseType(StatusCodes.Status400BadRequest)] // Trả về 400 nếu dữ liệu đầu vào không hợp lệ
        [ProducesResponseType(StatusCodes.Status401Unauthorized)] // Trả về 401 nếu chưa đăng nhập hoặc không đủ quyền
        public async Task<IActionResult> IngestTelemetry([FromBody] IngestionRequest request)
        {
            // Tạo đối tượng dữ liệu thô từ yêu cầu gửi lên
            var rawData = new RawMarketData
            {
                MetricKey = request.MetricKey.ToUpper(), // Chuyển Key thành in hoa
                Value = request.Value, // Giá trị đo lường
                Region = request.Region, // Vùng sản xuất / phân phối
                Source = request.Source, // Nguồn phát sinh dữ liệu (như Sensor_A, Manual...)
                Timestamp = DateTimeOffset.UtcNow // Thiết lập mốc thời gian ghi nhận hiện tại
            };

            // Đưa điểm dữ liệu thô này vào hàng đợi ngầm an toàn (Bounded Channel)
            bool queued = await _ingestionService.IngestAsync(rawData);

            if (!queued) // Nếu hàng đợi đã đầy cứng (vượt quá 10000 phần tử)
            {
                // Trả về lỗi 503 (Dịch vụ quá tải tạm thời)
                return StatusCode(StatusCodes.Status503ServiceUnavailable, 
                    new { message = "Telemetry queue buffer full. Try again later." });
            }

            // Trả về HTTP 202 Accepted thông báo dữ liệu đã được xếp hàng chờ xử lý bất đồng bộ
            return Accepted(new { message = "Telemetry telemetry queued for async batch processing." });
        }

        /// <summary>
        /// Đường dẫn truyền trực tiếp dữ liệu (SSE - Server-Sent Events) để cập nhật chỉ số thời gian thực cho Client.
        /// </summary>
        [HttpGet("stream")] // Định nghĩa API dạng GET: /api/pulse/stream
        [Produces("text/event-stream")] // Định nghĩa định dạng đầu ra của luồng là Event Stream chuyên dụng
        public async Task StreamMetrics(CancellationToken cancellationToken)
        {
            // Thiết lập các Header HTTP cần thiết cho việc duy trì kết nối luồng SSE lâu dài
            Response.ContentType = "text/event-stream"; // Đặt kiểu nội dung
            Response.Headers.Append("Cache-Control", "no-cache"); // Cấm trình duyệt cache lại luồng này
            Response.Headers.Append("Connection", "keep-alive"); // Giữ kết nối mạng luôn hoạt động (keep-alive)

            var bodyWriter = Response.Body; // Lấy bộ ghi nội dung của phản hồi HTTP

            // Định nghĩa hàm xử lý sự kiện (callback) khi có chỉ số chiến lược được cập nhật
            Action<StrategyMetricPulse> onMetricUpdatedHandler = async (metric) =>
            {
                try
                {
                    // Chuyển đổi dữ liệu chỉ số chiến lược mới thành chuỗi JSON sử dụng chuẩn CamelCase (chữ thường đầu từ)
                    var json = System.Text.Json.JsonSerializer.Serialize(new MetricPulseDto
                    {
                        MetricName = metric.MetricName,
                        MetricKey = metric.MetricKey,
                        Category = metric.Category,
                        Value = metric.Value,
                        StandardDeviation = metric.StandardDeviation,
                        SampleCount = metric.SampleCount,
                        ChangeRate = metric.ChangeRate,
                        PulseStatus = metric.PulseStatus,
                        LastUpdated = metric.LastUpdated
                    }, new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                    });

                    // Định dạng SSE yêu cầu bắt đầu bằng chữ "data: " và kết thúc bằng hai dấu xuống dòng "\n\n"
                    var message = $"data: {json}\n\n";
                    var bytes = System.Text.Encoding.UTF8.GetBytes(message); // Chuyển chuỗi thành mảng byte UTF-8
                    
                    await bodyWriter.WriteAsync(bytes, cancellationToken); // Ghi mảng byte này vào luồng phản hồi HTTP gửi tới client
                    await bodyWriter.FlushAsync(cancellationToken); // Đẩy ngay lập tức dữ liệu đi qua mạng mà không chờ bộ đệm đầy
                }
                catch
                {
                    // Ném ngoại lệ khi client đóng tab/ngắt kết nối mạng đột ngột. Không làm gì cả để tránh lỗi crash hệ thống.
                }
            };

            // Đăng ký hàm callback ở trên vào sự kiện tĩnh của MetricService để nhận thông báo mỗi khi tính toán xong chỉ số
            _metricService.OnMetricUpdated += onMetricUpdatedHandler;

            try
            {
                // Gửi một gói tin comment chào hỏi ban đầu xác nhận đã kết nối thành công tới client
                var handshakeBytes = System.Text.Encoding.UTF8.GetBytes(": connected\n\n");
                await bodyWriter.WriteAsync(handshakeBytes, cancellationToken);
                await bodyWriter.FlushAsync(cancellationToken);

                // Vòng lặp duy trì kết nối luồng SSE hoạt động liên tục thông qua gửi tín hiệu heartbeat định kỳ
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(15000, cancellationToken); // Gửi heartbeat mỗi 15 giây
                    
                    var keepAliveBytes = System.Text.Encoding.UTF8.GetBytes(": keep-alive\n\n"); // Gói tin keep-alive trống
                    await bodyWriter.WriteAsync(keepAliveBytes, cancellationToken);
                    await bodyWriter.FlushAsync(cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Bắt ngoại lệ bình thường khi client đóng trình duyệt/hủy kết nối, không coi đây là lỗi
            }
            finally
            {
                // CỰC KỲ QUAN TRỌNG: Hủy đăng ký callback khi kết nối đóng để ngăn chặn rò rỉ bộ nhớ (Memory Leak)
                _metricService.OnMetricUpdated -= onMetricUpdatedHandler;
            }
        }
    }
}
