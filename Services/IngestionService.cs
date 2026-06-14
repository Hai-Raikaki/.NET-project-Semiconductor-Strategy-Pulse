using System.Threading.Channels; // Import thư viện quản lý Thread-Safe Queue Channel
using System.Threading.Tasks; // Import hỗ trợ lập trình bất đồng bộ Task / ValueTask
using SemiconductorStrategyPulse.Models; // Import các Model thực thể dữ liệu

namespace SemiconductorStrategyPulse.Services
{
    // Dịch vụ tiếp nhận dữ liệu đo lường từ xa (Ingestion Service)
    public class IngestionService : IIngestionService
    {
        // Khai báo hàng đợi thread-safe dùng chung trong ứng dụng để chứa dữ liệu thô
        private readonly Channel<RawMarketData> _channel;

        // Constructor thực hiện tiêm hàng đợi (Channel) được đăng ký từ Program.cs
        public IngestionService(Channel<RawMarketData> channel)
        {
            _channel = channel;
        }

        // Thực hiện ghi dữ liệu telemetry thô nhận được từ API vào hàng đợi ngầm
        public ValueTask<bool> IngestAsync(RawMarketData data)
        {
            // Ghi dữ liệu vào hàng đợi một cách không bị nghẽn (non-blocking).
            // Nếu hàng đợi bị đầy, cấu hình giới hạn (DropOldest) sẽ tự xử lý êm xuôi mà không làm chậm/đóng băng luồng xử lý HTTP Request của Client.
            bool written = _channel.Writer.TryWrite(data); // Trả về true nếu ghi thành công, ngược lại trả về false
            return ValueTask.FromResult(written); // Chuyển kết quả thành ValueTask bất đồng bộ để tránh cấp phát tài nguyên không cần thiết
        }
    }
}
