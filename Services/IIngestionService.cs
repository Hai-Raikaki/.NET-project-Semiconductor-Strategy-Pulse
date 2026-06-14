using System.Threading.Tasks; // Import hỗ trợ lập trình bất đồng bộ Task / ValueTask
using SemiconductorStrategyPulse.Models; // Import thực thể dữ liệu (Models)

namespace SemiconductorStrategyPulse.Services
{
    // Giao diện (Interface) định nghĩa các phương thức cho dịch vụ nhận dữ liệu đo lường từ xa (Ingestion)
    public interface IIngestionService
    {
        // Ghi nhận điểm dữ liệu thô bất đồng bộ vào hàng đợi (trả về true nếu thành công)
        ValueTask<bool> IngestAsync(RawMarketData data);
    }
}
