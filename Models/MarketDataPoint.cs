using System; // Import kiểu dữ liệu hệ thống cơ bản

namespace SemiconductorStrategyPulse.Models
{
    // Đối tượng biểu diễn một điểm dữ liệu thị trường biến động thời gian thực (phục vụ biểu đồ SignalR)
    public class MarketDataPoint
    {
        public double Nvda { get; set; } // Giá cổ phiếu Nvidia tại thời điểm đo
        public double Tsm { get; set; } // Giá cổ phiếu TSMC tại thời điểm đo
        public double Sox { get; set; } // Trị số bán dẫn SOX Index tại thời điểm đo
        public DateTimeOffset Timestamp { get; set; } // Mốc thời gian ghi nhận (có thông tin múi giờ)
        
        // Tỷ lệ phần trăm thay đổi trên chu kỳ tick hiện tại (hỗ trợ phân loại màu xanh/đỏ hoặc sắp xếp biểu đồ trên giao diện)
        public double NvdaChange { get; set; } // % Biến động của Nvidia
        public double TsmChange { get; set; } // % Biến động của TSMC
        public double SoxChange { get; set; } // % Biến động của chỉ số SOX
    }
}
