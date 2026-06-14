using System; // Import kiểu dữ liệu hệ thống cơ bản

namespace SemiconductorStrategyPulse.Models
{
    // Thực thể RawMarketData đại diện cho bảng lưu trữ các điểm đo từ xa (telemetry) thô được nạp từ sensor hoặc API
    public class RawMarketData
    {
        public Guid Id { get; set; } = Guid.NewGuid(); // Khóa chính duy nhất định danh bản ghi, tự sinh mới dạng Guid
        public string MetricKey { get; set; } = string.Empty; // Mã định danh loại chỉ số (ví dụ: "CAPEX", "YIELD_RATE", "LEAD_TIME", "BOOK_TO_BILL")
        public double Value { get; set; } // Giá trị đo lường thô được gửi lên
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow; // Thời điểm ghi nhận dữ liệu (mặc định lấy thời gian UTC hiện tại)
        public string Region { get; set; } = string.Empty; // Vùng dữ liệu phát sinh (ví dụ: "APAC", "US", "EU", "GLOBAL")
        public string Source { get; set; } = string.Empty; // Nguồn thu thập dữ liệu (ví dụ: "TSMC_REPORT", "SIA_FEED", "ASML_ORDER", "Sensor_A")
    }
}
