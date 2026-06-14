using System; // Import kiểu dữ liệu hệ thống cơ bản

namespace SemiconductorStrategyPulse.DTOs
{
    // Đối tượng DTO biểu diễn dữ liệu chỉ số chiến lược tổng hợp gửi về Client (phục vụ hiển thị trên UI)
    public class MetricPulseDto
    {
        public string MetricName { get; set; } = string.Empty; // Tên hiển thị thân thiện (ví dụ: "Advanced Node Yield Rate (%)")
        public string MetricKey { get; set; } = string.Empty; // Mã khóa định danh duy nhất (ví dụ: "YIELD_RATE")
        public string Category { get; set; } = string.Empty; // Danh mục phân loại chỉ số (ví dụ: "CAPACITY")
        public double Value { get; set; } // Giá trị trung bình sau tính toán
        public double StandardDeviation { get; set; } // Độ lệch chuẩn đo lường biến động
        public int SampleCount { get; set; } // Số mẫu dữ liệu thô đã phân tích
        public double ChangeRate { get; set; } // Tỷ lệ % thay đổi so với kỳ trước
        public string PulseStatus { get; set; } = string.Empty; // Trạng thái cảnh báo xung nhịp (ví dụ: "STABLE", "CRITICAL")
        public DateTimeOffset LastUpdated { get; set; } // Mốc thời gian cập nhật kết quả mới nhất
    }
}
