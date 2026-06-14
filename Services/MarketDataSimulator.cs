using System; // Import các kiểu dữ liệu cơ bản của hệ thống .NET
using System.Collections.Generic; // Import các kiểu dữ liệu tập hợp như List, Dictionary
using System.Linq; // Import thư viện LINQ để xử lý tập hợp dữ liệu
using System.Threading; // Import thư viện hỗ trợ quản lý đa luồng và CancellationToken
using System.Threading.Tasks; // Import thư viện hỗ trợ lập trình bất đồng bộ (Task/async/await)
using Microsoft.AspNetCore.SignalR; // Import thư viện SignalR hỗ trợ giao tiếp thời gian thực (real-time websocket)
using Microsoft.Extensions.Caching.Memory; // Import dịch vụ lưu trữ bộ nhớ đệm tạm thời (In-Memory Cache)
using Microsoft.Extensions.Hosting; // Import lớp chạy ngầm BackgroundService
using Microsoft.Extensions.Logging; // Import thư viện hỗ trợ ghi nhật ký log hệ thống
using SemiconductorStrategyPulse.Hubs; // Import SignalR Hub của dự án
using SemiconductorStrategyPulse.Models; // Import các thực thể dữ liệu (Model)

namespace SemiconductorStrategyPulse.Services
{
    // MarketDataSimulator chạy ngầm để giả lập biến động giá cổ phiếu ngành bán dẫn (NVDA, TSM, SOX) mỗi 2 giây
    public class MarketDataSimulator : BackgroundService
    {
        // Khai báo Context kết nối đến SignalR Hub để gửi tin nhắn tới client
        private readonly IHubContext<MarketHub> _hubContext;
        // Khai báo bộ nhớ đệm (In-Memory Cache) để lưu lịch sử giá
        private readonly IMemoryCache _cache;
        // Khai báo Logger để ghi nhận log mô phỏng dữ liệu
        private readonly ILogger<MarketDataSimulator> _logger;
        // Khai báo đối tượng sinh số ngẫu nhiên phục vụ thuật toán Random Walk
        private readonly Random _random = new Random();
 
        // Danh sách lưu trữ lịch sử điểm dữ liệu (tạo cửa sổ trượt chứa tối đa 20 điểm dữ liệu)
        private readonly List<MarketDataPoint> _history = new List<MarketDataPoint>();
        // Khai báo đối tượng khóa (lock) để đồng bộ luồng khi đọc/ghi vào danh sách lịch sử
        private readonly object _lock = new object();
 
        // Thiết lập giá trị khởi điểm ban đầu cho các cổ phiếu/chỉ số
        private double _nvda = 450.0; // Cổ phiếu Nvidia
        private double _tsm = 100.0;  // Cổ phiếu TSMC
        private double _sox = 3500.0; // Chỉ số bán dẫn Philadelphia Semiconductor Index (SOX)
 
        // Định nghĩa Key dùng để lưu và truy xuất lịch sử giá từ Memory Cache
        public const string CacheKeyHistory = "market_history";
 
        // Hàm khởi tạo nhận Dependency Injection từ Service Container
        public MarketDataSimulator(
            IHubContext<MarketHub> hubContext,
            IMemoryCache cache,
            ILogger<MarketDataSimulator> logger)
        {
            _hubContext = hubContext; // Tiêm SignalR Hub Context
            _cache = cache; // Tiêm Memory Cache
            _logger = logger; // Tiêm Logger
 
            // Tạo sẵn lịch sử dữ liệu giả định 20 điểm trước đó để biểu đồ client hiển thị đầy đủ ngay khi tải trang
            InitializeHistory();
        }
 
        // Khởi tạo lịch sử dữ liệu giả lập cho 20 mốc thời gian trước đó
        private void InitializeHistory()
        {
            lock (_lock) // Đảm bảo đồng bộ luồng, tránh xung đột ghi dữ liệu từ luồng khác
            {
                var time = DateTimeOffset.UtcNow.AddSeconds(-40); // Bắt đầu lùi thời gian lại 40 giây trước
                for (int i = 0; i < 20; i++)
                {
                    // Lưu lại giá cũ của vòng lặp trước
                    double prevNvda = _nvda;
                    double prevTsm = _tsm;
                    double prevSox = _sox;
 
                    // Sinh tỷ lệ thay đổi ngẫu nhiên từ -2% đến +2%
                    double nvdaChange = (_random.NextDouble() * 0.04) - 0.02; 
                    double tsmChange = (_random.NextDouble() * 0.04) - 0.02;
                    double soxChange = (_random.NextDouble() * 0.04) - 0.02;
 
                    // Tính toán giá trị mới sau biến động ngẫu nhiên và làm tròn tới 2 chữ số thập phân
                    _nvda = Math.Round(_nvda * (1 + nvdaChange), 2);
                    _tsm = Math.Round(_tsm * (1 + tsmChange), 2);
                    _sox = Math.Round(_sox * (1 + soxChange), 2);
 
                    // Tạo đối tượng điểm dữ liệu thị trường mới
                    var point = new MarketDataPoint
                    {
                        Nvda = _nvda,
                        Tsm = _tsm,
                        Sox = _sox,
                        Timestamp = time,
                        NvdaChange = Math.Round(nvdaChange * 100, 2), // Phần trăm thay đổi Nvidia
                        TsmChange = Math.Round(tsmChange * 100, 2),   // Phần trăm thay đổi TSMC
                        SoxChange = Math.Round(soxChange * 100, 2)    // Phần trăm thay đổi SOX
                    };
 
                    _history.Add(point); // Thêm điểm dữ liệu vào danh sách lịch sử
                    time = time.AddSeconds(2); // Tăng thời gian lên 2 giây cho điểm tiếp theo
                }
 
                // Ghi bản sao danh sách lịch sử này vào bộ nhớ đệm cache
                _cache.Set(CacheKeyHistory, _history.ToList());
            }
        }
 
        // Vòng lặp chính chạy ngầm để cập nhật giá trị mới liên tục mỗi 2 giây
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Market Price Telemetry Simulator started ticking every 2s."); // Log thông báo trình giả lập hoạt động
 
            // Chạy liên tục cho đến khi ứng dụng tắt
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Chờ đợi 2 giây trước lần cập nhật tiếp theo (bất đồng bộ)
                    await Task.Delay(2000, stoppingToken);
 
                    // Lưu trữ giá hiện tại
                    double prevNvda = _nvda;
                    double prevTsm = _tsm;
                    double prevSox = _sox;
 
                    // Tạo biến động ngẫu nhiên từ -2% đến +2%
                    double nvdaChange = (_random.NextDouble() * 0.04) - 0.02;
                    double tsmChange = (_random.NextDouble() * 0.04) - 0.02;
                    double soxChange = (_random.NextDouble() * 0.04) - 0.02;
 
                    // Áp dụng biến động và làm tròn
                    _nvda = Math.Round(_nvda * (1 + nvdaChange), 2);
                    _tsm = Math.Round(_tsm * (1 + tsmChange), 2);
                    _sox = Math.Round(_sox * (1 + soxChange), 2);
 
                    // Tạo một điểm dữ liệu mới tại thời điểm hiện tại
                    var newPoint = new MarketDataPoint
                    {
                        Nvda = _nvda,
                        Tsm = _tsm,
                        Sox = _sox,
                        Timestamp = DateTimeOffset.UtcNow, // Lấy mốc thời gian hiện tại
                        NvdaChange = Math.Round(nvdaChange * 100, 2),
                        TsmChange = Math.Round(tsmChange * 100, 2),
                        SoxChange = Math.Round(soxChange * 100, 2)
                    };
 
                    lock (_lock) // Đồng bộ luồng để ghi điểm mới an toàn
                    {
                        _history.Add(newPoint); // Thêm điểm mới vào đuôi danh sách
                        if (_history.Count > 20)
                        {
                            _history.RemoveAt(0); // Nếu vượt quá 20 điểm, xóa điểm cũ nhất ở đầu danh sách (Cửa sổ trượt)
                        }
 
                        // Cập nhật lại danh sách mới vào Memory Cache
                        _cache.Set(CacheKeyHistory, _history.ToList());
                    }
 
                    // Log debug thông tin cập nhật giá mới
                    _logger.LogDebug("Broadcasting market tick: NVDA={Nvda}, TSM={Tsm}, SOX={Sox}", _nvda, _tsm, _sox);
                    // Phát sóng thời gian thực (broadcast) điểm dữ liệu mới này tới tất cả Client đang kết nối SignalR
                    await _hubContext.Clients.All.SendAsync("ReceiveMarketUpdate", newPoint, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break; // Dừng vòng lặp nhẹ nhàng khi nhận yêu cầu hủy ứng dụng
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred in MarketDataSimulator loop."); // Ghi log lỗi nếu vòng lặp xảy ra ngoại lệ
                }
            }
        }
    }
}
