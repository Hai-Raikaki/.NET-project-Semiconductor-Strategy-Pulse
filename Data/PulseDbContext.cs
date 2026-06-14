using Microsoft.AspNetCore.Identity; // Import thư viện quản lý phân quyền và người dùng Identity
using Microsoft.AspNetCore.Identity.EntityFrameworkCore; // Import DbContext có hỗ trợ sẵn Identity
using Microsoft.EntityFrameworkCore; // Import thư viện Entity Framework Core làm việc với DB
using SemiconductorStrategyPulse.Models; // Import các thực thể thực bảng cơ sở dữ liệu

namespace SemiconductorStrategyPulse.Data
{
    // Lớp PulseDbContext kế thừa từ IdentityDbContext để hỗ trợ sẵn quản lý tài khoản người dùng và bảo mật JWT
    public class PulseDbContext : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>
    {
        // Constructor truyền các thiết lập kết nối (Options) lên lớp cơ sở DbContext
        public PulseDbContext(DbContextOptions<PulseDbContext> options) : base(options)
        {
        }

        // Định nghĩa bảng dữ liệu thô nhận được từ các cảm biến/thiết bị đo lường từ xa (Raw Telemetry Data)
        public DbSet<RawMarketData> RawMarketData { get; set; } = null!;
        // Định nghĩa bảng tổng hợp các chỉ số chiến lược bán dẫn (Strategic Indicator Indicators)
        public DbSet<StrategyMetricPulse> StrategyMetricPulses { get; set; } = null!;
        // Định nghĩa bảng lưu trữ các mã Refresh Token phục vụ xoay vòng khóa phiên làm việc
        public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;

        // Phương thức thiết lập cấu hình lược đồ (Schema) CSDL và các chỉ mục (Index)
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder); // Gọi hàm khởi tạo mặc định của Identity để dựng các bảng User, Role

            // Cấu hình riêng cho thực thể bảng RawMarketData
            modelBuilder.Entity<RawMarketData>(entity =>
            {
                entity.HasKey(e => e.Id); // Khai báo khóa chính Id
                
                // Tạo chỉ mục kết hợp (Composite Index) trên 2 cột MetricKey và Timestamp
                // Hỗ trợ tăng tốc độ truy vấn lọc dữ liệu lịch sử thô theo Key để tính toán trung bình/độ lệch chuẩn ngầm
                entity.HasIndex(e => new { e.MetricKey, e.Timestamp })
                      .HasDatabaseName("IX_RawMarketData_MetricKey_Timestamp");

                // Tạo chỉ mục đơn trên cột Timestamp phục vụ các truy vấn sắp xếp thời gian nhanh chóng
                entity.HasIndex(e => e.Timestamp)
                      .HasDatabaseName("IX_RawMarketData_Timestamp");
            });

            // Cấu hình riêng cho thực thể bảng StrategyMetricPulse
            modelBuilder.Entity<StrategyMetricPulse>(entity =>
            {
                entity.HasKey(e => e.Id); // Khai báo khóa chính
                
                // Tạo chỉ mục duy nhất (Unique Index) trên cột MetricKey
                // Ngăn chặn việc tạo trùng lặp khóa chỉ số chiến lược và tối ưu tốc độ tìm kiếm chỉ số bằng Key
                entity.HasIndex(e => e.MetricKey)
                      .IsUnique()
                      .HasDatabaseName("IX_StrategyMetricPulses_MetricKey");
            });

            // Cấu hình riêng cho thực thể bảng RefreshToken
            modelBuilder.Entity<RefreshToken>(entity =>
            {
                entity.HasKey(e => e.Id); // Khai báo khóa chính
                
                // Tạo chỉ mục duy nhất cho chuỗi token để tìm kiếm nhanh chóng khi làm mới Token (Refresh)
                entity.HasIndex(e => e.Token)
                      .IsUnique()
                      .HasDatabaseName("IX_RefreshTokens_Token");

                // Thiết lập mối quan hệ 1-Nhiều (1 User có nhiều Refresh Tokens)
                entity.HasOne(e => e.User)
                      .WithMany() // Không khai báo danh sách Tokens ngược lại trong AppUser để giữ Model tinh gọn
                      .HasForeignKey(e => e.UserId) // Khai báo khóa ngoại UserId
                      .OnDelete(DeleteBehavior.Cascade); // Nếu xóa User, tự động xóa sạch các Refresh Token đi kèm để dọn DB
            });
        }
    }
}

