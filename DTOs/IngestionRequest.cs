using System.ComponentModel.DataAnnotations; // Import thư viện hỗ trợ validate định dạng thuộc tính đầu vào (Data Annotations)

namespace SemiconductorStrategyPulse.DTOs
{
    // Đối tượng yêu cầu truyền nhận dữ liệu đo lường từ xa (Ingestion Request DTO) gửi từ các Sensor/Thiết bị
    public class IngestionRequest
    {
        [Required] // Yêu cầu bắt buộc
        [RegularExpression("^(BOOK_TO_BILL|LEAD_TIME|YIELD_RATE|CAPEX)$", 
            ErrorMessage = "MetricKey must be one of: BOOK_TO_BILL, LEAD_TIME, YIELD_RATE, CAPEX")] // Ràng buộc chỉ nhận 1 trong 4 mã khóa hợp lệ
        public string MetricKey { get; set; } = string.Empty;

        [Required] // Yêu cầu bắt buộc
        [Range(-10000.0, 1000000.0, ErrorMessage = "Value is out of valid range.")] // Đảm bảo giá trị nằm trong ngưỡng đo lường cho phép
        public double Value { get; set; }

        public string Region { get; set; } = "GLOBAL"; // Vùng ghi nhận dữ liệu (mặc định là GLOBAL)

        public string Source { get; set; } = "EXTERNAL_FEED"; // Nguồn gốc thu thập dữ liệu (mặc định là nguồn cấp dữ liệu ngoài)
    }
}
