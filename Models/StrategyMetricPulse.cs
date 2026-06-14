using System; // Import kiểu dữ liệu hệ thống cơ bản

namespace SemiconductorStrategyPulse.Models
{
    // Thực thể StrategyMetricPulse lưu trữ thông tin chỉ số chiến lược bán dẫn đã được xử lý tổng hợp
    public class StrategyMetricPulse
    {
        public Guid Id { get; set; } = Guid.NewGuid(); // Khóa chính định danh duy nhất của chỉ số, tự sinh Guid mới
        public string MetricName { get; set; } = string.Empty; // Tên hiển thị thân thiện (ví dụ: "Advanced Node Yield Rate (%)")
        public string MetricKey { get; set; } = string.Empty;  // Mã định danh chỉ số duy nhất (ví dụ: "YIELD_RATE", "BOOK_TO_BILL")
        public string Category { get; set; } = string.Empty;   // Danh mục phân loại chỉ số (ví dụ: "SUPPLY_CHAIN", "FINANCIAL", "CAPACITY")
        public double Value { get; set; } // Giá trị trung bình (Mean) tính từ 500 điểm dữ liệu thô gần nhất
        public double StandardDeviation { get; set; } // Độ lệch chuẩn (Standard Deviation) đo lường mức độ biến động
        public int SampleCount { get; set; } // Tổng số lượng mẫu dữ liệu thô được sử dụng để tính toán
        public double ChangeRate { get; set; } // Tỷ lệ % thay đổi giá trị trung bình so với trị số tổng hợp kỳ trước
        public string PulseStatus { get; set; } = "STABLE"; // Trạng thái chỉ số chiến lược (ví dụ: "CRITICAL", "STABLE", "GROWING", "DECLINING")
        public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow; // Mốc thời gian cập nhật giá trị gần nhất
    }
}
