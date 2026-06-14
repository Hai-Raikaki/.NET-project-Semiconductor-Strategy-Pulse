using System; // Import thư viện cơ bản hệ thống
using System.Collections.Generic; // Import cấu trúc tập hợp danh sách
using System.Threading.Tasks; // Import hỗ trợ lập trình bất đồng bộ Task
using SemiconductorStrategyPulse.Models; // Import thực thể dữ liệu (Models)

namespace SemiconductorStrategyPulse.Services
{
    // Giao diện (Interface) định nghĩa các dịch vụ quản lý và tính toán chỉ số chiến lược bán dẫn
    public interface IMetricService
    {
        // Sự kiện thông báo khi một chỉ số được cập nhật xong (dùng cho Server-Sent Events stream)
        event Action<StrategyMetricPulse> OnMetricUpdated;
        
        // Lấy danh sách tất cả chỉ số chiến lược đã tổng hợp
        Task<IEnumerable<StrategyMetricPulse>> GetAllMetricsAsync();
        
        // Lấy thông tin một chỉ số cụ thể bằng mã khóa Key
        Task<StrategyMetricPulse?> GetMetricByKeyAsync(string key);
        
        // Tính toán lại giá trị trung bình, độ lệch chuẩn cho một chỉ số theo Key dựa trên dữ liệu thô
        Task RecalculateMetricAsync(string key);
    }
}
