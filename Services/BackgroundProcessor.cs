using System; // Import các kiểu dữ liệu cơ bản của hệ thống .NET
using System.Collections.Generic; // Import các kiểu dữ liệu tập hợp như List, Dictionary
using System.Linq; // Import thư viện LINQ để truy vấn và thao tác trên tập hợp dữ liệu
using System.Threading; // Import thư viện hỗ trợ quản lý đa luồng và CancellationToken
using System.Threading.Channels; // Import thư viện Channel để sử dụng hàng đợi thread-safe tốc độ cao
using System.Threading.Tasks; // Import thư viện hỗ trợ lập trình bất đồng bộ (Task/async/await)
using Microsoft.Extensions.DependencyInjection; // Import thư viện quản lý Dependency Injection và tạo Scope
using Microsoft.Extensions.Hosting; // Import thư viện cung cấp lớp nền chạy ngầm BackgroundService
using Microsoft.Extensions.Logging; // Import thư viện hỗ trợ ghi nhận nhật ký hệ thống (logs)
using SemiconductorStrategyPulse.Data; // Import lớp Database Context của dự án
using SemiconductorStrategyPulse.Models; // Import các Model thực thể dữ liệu của dự án

namespace SemiconductorStrategyPulse.Services
{
    // BackgroundProcessor chạy ngầm liên tục để lấy dữ liệu telemetry từ hàng đợi lưu vào Database và tính lại chỉ số
    public class BackgroundProcessor : BackgroundService
    {
        // Khai báo hàng đợi thread-safe chứa dữ liệu telemetry thô chờ xử lý
        private readonly Channel<RawMarketData> _channel;
        // Khai báo Service Provider để tạo scope truy xuất các dịch vụ Scoped như DbContext
        private readonly IServiceProvider _serviceProvider;
        // Khai báo Logger ghi nhận lại vết quá trình xử lý ngầm
        private readonly ILogger<BackgroundProcessor> _logger;

        // Hàm khởi tạo nhận vào các Dependency được tiêm từ hệ thống DI Container
        public BackgroundProcessor(
            Channel<RawMarketData> channel,
            IServiceProvider serviceProvider,
            ILogger<BackgroundProcessor> _logger)
        {
            _channel = channel; // Lưu hàng đợi dùng chung
            _serviceProvider = serviceProvider; // Lưu provider để tạo scope sau này
            this._logger = _logger; // Lưu logger
        }

        // Phương thức lõi chạy ngầm được tự động kích hoạt khi ứng dụng khởi chạy
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Background Telemetry Ingestion Processor started."); // Ghi log tiến trình bắt đầu chạy
            var batch = new List<RawMarketData>(); // Tạo danh sách lưu trữ tạm thời các phần tử trước khi lưu hàng loạt (batching)
            var lastFlushTime = DateTime.UtcNow; // Ghi nhận thời gian cuối cùng thực hiện đẩy dữ liệu xuống DB
            const int maxBatchSize = 1000; // Định nghĩa kích thước lô tối đa là 1000 phần tử để tối ưu hiệu suất ghi

            // Lặp liên tục cho đến khi ứng dụng có yêu cầu dừng (stoppingToken được kích hoạt)
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Nếu còn dữ liệu chưa xử lý trong lô tạm, tiến hành flush ngay trước khi bị chặn đợi dữ liệu mới
                    if (batch.Any())
                    {
                        await FlushBatchAsync(batch); // Đẩy lô dữ liệu hiện tại vào database
                        batch.Clear(); // Xóa sạch danh sách tạm để chuẩn bị cho lô tiếp theo
                        lastFlushTime = DateTime.UtcNow; // Cập nhật thời gian flush cuối cùng
                    }

                    // Chờ đợi bất đồng bộ cho đến khi có dữ liệu mới được đưa vào hàng đợi
                    if (await _channel.Reader.WaitToReadAsync(stoppingToken))
                    {
                        // Đọc tất cả các phần tử đang sẵn có trong hàng đợi mà không chặn luồng
                        while (_channel.Reader.TryRead(out var item))
                        {
                            batch.Add(item); // Thêm dữ liệu thô vừa đọc vào danh sách lô tạm
                            // Nếu kích thước lô đạt giới hạn 1000, lập tức lưu xuống DB luôn
                            if (batch.Count >= maxBatchSize)
                            {
                                await FlushBatchAsync(batch); // Lưu lô dữ liệu hiện tại
                                batch.Clear(); // Giải phóng danh sách tạm
                                lastFlushTime = DateTime.UtcNow; // Cập nhật mốc thời gian flush
                            }
                        }
                    }

                    // Tạm dừng ngắn để nhường tài nguyên CPU cho các luồng xử lý khác
                    await Task.Delay(50, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break; // Thoát vòng lặp an toàn khi nhận được yêu cầu hủy ứng dụng
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing market telemetry channel batch."); // Log lỗi xảy ra trong quá trình chạy ngầm
                    // Tạm dừng 2 giây trước khi thử lại để tránh làm nghẽn CPU khi DB hoặc hệ thống gặp lỗi liên tục
                    await Task.Delay(2000, stoppingToken);
                }
            }

            // Khi ứng dụng chuẩn bị tắt hoàn toàn, kiểm tra xem còn dữ liệu sót lại trong lô tạm hay không
            if (batch.Any())
            {
                try
                {
                    await FlushBatchAsync(batch); // Cố gắng đẩy nốt số dữ liệu còn sót lại vào database trước khi tắt hẳn
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error flushing remaining batch elements during shutdown."); // Log lỗi nếu lưu thất bại lúc tắt app
                }
            }
        }

        // Phương thức lưu lô dữ liệu xuống database và kích hoạt tính toán lại chỉ số
        private async Task FlushBatchAsync(List<RawMarketData> batch)
        {
            _logger.LogInformation("Flushing {Count} telemetry events to the database.", batch.Count); // Ghi log số lượng sự kiện chuẩn bị ghi
            
            using var scope = _serviceProvider.CreateScope(); // Tạo một Scope tạm thời để giải phóng bộ nhớ sau khi dùng xong
            var dbContext = scope.ServiceProvider.GetRequiredService<PulseDbContext>(); // Lấy DbContext trong phạm vi Scope tạm
            var metricService = scope.ServiceProvider.GetRequiredService<IMetricService>(); // Lấy MetricService trong phạm vi Scope tạm

            // Thêm danh sách lô dữ liệu thô vào DbSet tương ứng
            await dbContext.RawMarketData.AddRangeAsync(batch);
            await dbContext.SaveChangesAsync(); // Lưu thay đổi xuống cơ sở dữ liệu PostgreSQL/SQLite thực tế

            // Lọc ra danh sách các Key (chỉ số) độc nhất vừa mới cập nhật trong lô dữ liệu này
            var updatedKeys = batch.Select(x => x.MetricKey).Distinct().ToList();

            // Duyệt qua từng Key chỉ số vừa được ghi nhận để thực hiện tính toán lại giá trị chiến lược
            foreach (var key in updatedKeys)
            {
                try
                {
                    await metricService.RecalculateMetricAsync(key); // Tính toán lại giá trị trung bình, độ lệch chuẩn, cập nhật cache và stream qua SSE
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error recalculating strategy metric for key: {Key}", key); // Log lỗi nếu quá trình tính toán lại của một Key bị lỗi
                }
            }
        }
    }
}
