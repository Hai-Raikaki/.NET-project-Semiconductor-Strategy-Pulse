using Microsoft.AspNetCore.SignalR; // Import thư viện thời gian thực SignalR

namespace SemiconductorStrategyPulse.Hubs
{
    // Lớp MarketHub kế thừa từ Hub (SignalR) để mở một cổng giao tiếp WebSocket hai chiều
    public class MarketHub : Hub
    {
        // Các Client kết nối tới Hub này sẽ tự động nhận các gói tin cập nhật biến động giá cổ phiếu thời gian thực được đẩy (Push) đi từ MarketDataSimulator
    }
}
