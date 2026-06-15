// 1. Cấu hình chi tiết cho các thẻ đo lường Telemetry chiến lược
const TelemetryConfig = {
    // Chỉ số Book-to-Bill: Tỷ lệ đơn hàng trên doanh thu hóa đơn
    BOOK_TO_BILL: { 
        valId: 'val-book-to-bill',       // ID của thẻ HTML chứa giá trị số hiển thị
        statusId: 'status-book-to-bill', // ID của thẻ HTML chứa nhãn trạng thái (Ví dụ: Stable, Critical)
        changeId: 'change-book-to-bill', // ID của thẻ HTML chứa tỷ lệ phần trăm thay đổi (+/-%)
        sdId: 'sd-book-to-bill',         // ID chứa giá trị Độ lệch chuẩn (Standard Deviation)
        nId: 'n-book-to-bill',           // ID chứa số lượng mẫu dữ liệu đã tích lũy (Sample Count)
        cardId: 'card-book-to-bill',     // ID của khung chứa vật lý (Thẻ Card) để chạy hiệu ứng phát sáng
        decimals: 2                      // Số chữ số hiển thị sau dấu phẩy thập phân là 2
    },
    // Chỉ số Average Lead Time: Thời gian trung bình từ lúc đặt hàng đến khi giao hàng
    LEAD_TIME: { 
        valId: 'val-lead-time', 
        statusId: 'status-lead-time', 
        changeId: 'change-lead-time', 
        sdId: 'sd-lead-time', 
        nId: 'n-lead-time', 
        cardId: 'card-lead-time', 
        decimals: 1                      // Số chữ số hiển thị sau dấu phẩy thập phân là 1
    },
    // Chỉ số Yield Rate: Tỷ lệ sản xuất bán dẫn đạt chất lượng ở các tiến trình nâng cao
    YIELD_RATE: { 
        valId: 'val-yield-rate', 
        statusId: 'status-yield-rate', 
        changeId: 'change-yield-rate', 
        sdId: 'sd-yield-rate', 
        nId: 'n-yield-rate', 
        cardId: 'card-yield-rate', 
        decimals: 1                      // Số chữ số hiển thị sau dấu phẩy thập phân là 1
    },
    // Chỉ số CapEx Momentum: Đà đầu tư tài sản cố định/nhà máy mới
    CAPEX: { 
        valId: 'val-capex', 
        statusId: 'status-capex', 
        changeId: 'change-capex', 
        sdId: 'sd-capex', 
        nId: 'n-capex', 
        cardId: 'card-capex', 
        decimals: 1                      // Số chữ số hiển thị sau dấu phẩy thập phân là 1
    }
};

// Cấu hình chi tiết các thẻ báo giá cho các mã cổ phiếu thị trường bán dẫn
const StockConfig = {
    // Cổ phiếu NVIDIA (NVDA)
    NVDA: { 
        valId: 'val-nvda',               // ID thẻ HTML hiển thị giá hiện tại
        changeId: 'change-nvda',         // ID thẻ HTML hiển thị tỷ lệ thay đổi giá
        cardId: 'card-nvda'              // ID khung thẻ để nhấp nháy Neon khi đổi giá
    },
    // Cổ phiếu TSMC (TSM)
    TSM: { 
        valId: 'val-tsm', 
        changeId: 'change-tsm', 
        cardId: 'card-tsm' 
    },
    // Chỉ số PHLX Semiconductor Index (SOX)
    SOX: { 
        valId: 'val-sox', 
        changeId: 'change-sox', 
        cardId: 'card-sox' 
    }
};

// Ánh xạ các phần tử DOM giao diện tĩnh từ HTML
const sseStatus = document.getElementById('sse-status');                  // Lấy thẻ trạng thái luồng SSE
const signalrStatus = document.getElementById('signalr-status');          // Lấy thẻ trạng thái luồng SignalR
const consoleLogs = document.getElementById('console-logs');              // Lấy khung chứa nhật ký log chẩn đoán
const clearConsoleBtn = document.getElementById('clear-console');          // Lấy nút click xóa sạch nhật ký console
const metricSelect = document.getElementById('metric-select');            // Lấy dropdown chọn loại Metric mô phỏng
const unitIndicator = document.getElementById('unit-indicator');          // Lấy nhãn hiển thị đơn vị đo lường đo đạc
const simulatorForm = document.getElementById('simulator-form');          // Lấy biểu mẫu form gửi telemetry giả lập
const simAlert = document.getElementById('sim-alert');                    // Lấy thẻ hiển thị thông báo kết quả gửi form

// =================================================================================
// 2. PHẦN HOẠT HỌA BIỂU ĐỒ SÓNG NHỊP TIM ĐIỆN TỬ (CANVAS 2D)
// =================================================================================
const canvas = document.getElementById('pulse-wave');                     // Lấy phần tử vẽ Canvas đồ họa
const ctx = canvas.getContext('2d');                                      // Lấy ngữ cảnh vẽ 2D để thao tác đồ họa
let waveOffset = 0;                                                       // Tọa độ lệch X để dịch chuyển sóng chạy ngang
let flashWaveIntensity = 0;                                               // Cường độ phát sáng tăng cường khi nhận xung dữ liệu

// Hàm điều chỉnh kích thước Canvas cho khớp với khung cha thực tế
function resizeCanvas() {
    canvas.width = canvas.parentElement.clientWidth;                      // Gán chiều rộng canvas bằng chiều rộng thẻ cha
    canvas.height = canvas.parentElement.clientHeight;                    // Gán chiều cao canvas bằng chiều cao thẻ cha
}
window.addEventListener('resize', resizeCanvas);                          // Đăng ký sự kiện lắng nghe khi người dùng đổi size trình duyệt
resizeCanvas();                                                           // Thực thi co giãn canvas lần đầu ngay khi chạy script

// Vòng lặp vẽ và cập nhật hoạt họa sóng điện tử nhịp tim liên tục
function animatePulseWave() {
    ctx.clearRect(0, 0, canvas.width, canvas.height);                    // Xóa sạch toàn bộ hình vẽ cũ trên khung canvas
    
    // Vẽ hệ thống lưới kỹ thuật màu mờ chìm phía sau
    ctx.strokeStyle = 'rgba(255, 255, 255, 0.01)';                        // Thiết lập màu viền lưới nhạt tối đa
    ctx.lineWidth = 1;                                                    // Độ rộng nét vẽ lưới là 1 pixel
    for (let x = 0; x < canvas.width; x += 30) {                          // Lặp qua các tọa độ dọc cách nhau 30 pixel
        ctx.beginPath();                                                  // Khởi tạo đường vẽ mới
        ctx.moveTo(x, 0);                                                 // Đặt bút ở đỉnh trên
        ctx.lineTo(x, canvas.height);                                     // Kéo nét thẳng xuống đáy
        ctx.stroke();                                                     // Vẽ đường lưới dọc
    }
    for (let y = 0; y < canvas.height; y += 15) {                         // Lặp qua các tọa độ ngang cách nhau 15 pixel
        ctx.beginPath();                                                  // Khởi tạo đường vẽ mới
        ctx.moveTo(0, y);                                                 // Đặt bút ở lề trái
        ctx.lineTo(canvas.width, y);                                      // Kéo nét thẳng sang lề phải
        ctx.stroke();                                                     // Vẽ đường lưới ngang
    }
    
    // Bắt đầu quy trình vẽ đường sóng nhịp tim chính
    ctx.beginPath();                                                      // Khởi tạo nét vẽ
    ctx.lineWidth = 1.5;                                                  // Độ dày nét vẽ sóng là 1.5 pixel
    
    // Tạo màu vẽ dải Gradient đổi từ xanh Cyan -> Tím -> xanh Cyan
    const gradient = ctx.createLinearGradient(0, 0, canvas.width, 0);     // Khởi tạo dải màu tuyến tính chạy từ trái sang phải
    gradient.addColorStop(0, 'rgba(0, 240, 255, 0.6)');                    // Điểm mốc trái cùng: Xanh Cyan
    gradient.addColorStop(0.5, 'rgba(189, 0, 255, 0.6)');                  // Điểm mốc chính giữa: Tím Neon
    gradient.addColorStop(1, 'rgba(0, 240, 255, 0.6)');                    // Điểm mốc phải cùng: Xanh Cyan
    ctx.strokeStyle = gradient;                                           // Thiết lập màu nét vẽ bằng dải màu Gradient vừa tạo
    
    // Tạo bóng sáng mờ ảo Neon tùy thuộc vào flashWaveIntensity
    ctx.shadowBlur = 4 + (flashWaveIntensity * 12);                       // Biên độ phát sáng tỏa ra ngoài nét vẽ
    ctx.shadowColor = '#00f0ff';                                          // Màu phát sáng tỏa là xanh Cyan Neon

    const centerY = canvas.height / 2;                                    // Xác định tọa độ dọc nằm ở trung tâm Canvas
    const waveFrequency = 0.02;                                           // Tần số sóng (Độ dốc/mật độ của các ngọn sóng)
    const waveAmplitude = 10;                                             // Biên độ mặc định (độ cao sóng thông thường)
    
    // Lặp qua từng pixel theo trục hoành X để vẽ biên độ sóng Y
    for (let x = 0; x < canvas.width; x++) {
        // Hàm sóng hình sin chuẩn kết hợp waveOffset để tạo chuyển động
        let y = Math.sin((x * waveFrequency) + waveOffset) * waveAmplitude;
        
        // Tạo nhịp tim đột biến (Heartbeat Spikes) lặp lại đều đặn mỗi 240px
        const spike = (x + (waveOffset * 80)) % 240;
        if (spike > 100 && spike < 125) {                                 // Nếu nằm trong khung phát sinh nhịp đập nhọn
            const ratio = (spike - 100) / 25;                             // Tính toán tỷ lệ phần trăm tiến trình nhịp đập
            // Sử dụng hàm sine tần số cao để tạo hình ảnh nhịp đập nhọn đứng lên và đi xuống
            y += Math.sin(ratio * Math.PI * 4) * (waveAmplitude * 2.2 + (flashWaveIntensity * 25));
        }

        if (x === 0) {
            ctx.moveTo(x, centerY + y);                                   // Nếu là điểm đầu tiên, đặt điểm vẽ khởi nguồn
        } else {
            ctx.lineTo(x, centerY + y);                                   // Vẽ đoạn thẳng nối tiếp các pixel
        }
    }
    ctx.stroke();                                                         // Tô màu thực tế cho nét vẽ vừa thiết lập
    ctx.shadowBlur = 0;                                                   // Reset thuộc tính bóng sáng để tránh lỗi cho các thao tác vẽ sau
    
    // Điều chỉnh độ lệch ngang để sóng di động về bên trái
    waveOffset -= 0.04 + (flashWaveIntensity * 0.04);
    // Giảm dần cường độ kích thích phát sáng của nhịp đập về trạng thái thường
    if (flashWaveIntensity > 0) flashWaveIntensity -= 0.02;
    
    requestAnimationFrame(animatePulseWave);                              // Đăng ký trình duyệt gọi lại hàm này ở khung hình kế tiếp
}
requestAnimationFrame(animatePulseWave);                                  // Bắt đầu khởi động chạy hoạt họa sóng

// Hàm kích hoạt phát sáng Neon cực đại khi có tín hiệu dữ liệu mới
function excitePulseWave() {
    flashWaveIntensity = 1.0;                                             // Thiết lập cường độ phát sáng lên mức tối đa (1.0)
}

// =================================================================================
// 3. NHẬT KÝ CHẨN ĐOÁN (DIAGNOSTIC LOGGER) VÀ HIỆU ỨNG THẺ
// =================================================================================

// Ghi log kèm thời gian cụ thể vào Terminal ảo trên trang
function logConsole(message, type = 'info') {
    const time = new Date().toLocaleTimeString();                         // Lấy chuỗi thời gian hiện tại (hh:mm:ss)
    const line = document.createElement('div');                           // Tạo một thẻ div mới chứa dòng log
    line.className = `log-line ${type}`;                                  // Gán class CSS tương ứng với loại log (ví dụ: error, success)
    line.innerHTML = `[${time}] ${message}`;                              // Gán nội dung văn bản kèm tag thời gian
    consoleLogs.appendChild(line);                                        // Đẩy dòng log mới vào khung chứa HTML
    consoleLogs.scrollTop = consoleLogs.scrollHeight;                     // Cuộn thanh trượt của khung log xuống dòng mới nhất dưới cùng
    
    // Nếu số lượng dòng vượt quá 40 dòng, thực hiện dọn dẹp dòng cũ để giải phóng RAM
    while (consoleLogs.childElementCount > 40) {
        consoleLogs.removeChild(consoleLogs.firstChild);                  // Loại bỏ dòng đầu tiên (dòng cũ nhất)
    }
}

// Lắng nghe sự kiện click nút xóa màn hình console
clearConsoleBtn.addEventListener('click', () => {
    consoleLogs.innerHTML = '';                                           // Xóa sạch mọi thẻ con bên trong khung log
    logConsole('Console logs cleared by operator.', 'system');            // Ghi nhận dòng hệ thống báo đã xóa
});

// Tạo hiệu ứng nhấp nháy phát sáng Neon viền card khi giá trị được cập nhật
function flashCard(cardId, dir) {
    const cardEl = document.getElementById(cardId);                      // Lấy thẻ HTML của card theo ID
    if (!cardEl) return;                                                 // Kiểm tra an toàn: thoát nếu không tìm thấy card
    
    const className = dir === 'up' ? 'flash-up' : 'flash-down';           // Chọn class tương ứng (tăng là xanh, giảm là đỏ)
    cardEl.classList.remove('flash-up', 'flash-down');                    // Gỡ bỏ class cũ nếu có
    void cardEl.offsetWidth;                                              // Dòng đặc biệt buộc trình duyệt vẽ lại thuộc tính CSS (reflow)
    cardEl.classList.add(className);                                      // Đắp class hiệu ứng mới để chạy CSS animation
}

// =================================================================================
// 4. THIẾT LẬP VÀ ĐIỀU KHIỂN BIỂU ĐỒ ĐƯỜNG (CHART.JS ENGINE)
// =================================================================================
let liveChart;                                                            // Biến lưu đối tượng biểu đồ Chart.js
const maxChartTicks = 20;                                                 // Số lượng điểm dữ liệu tối đa hiển thị cùng lúc trên trục hoành

// Hàm khởi tạo biểu đồ
function initChart(initialData = []) {
    const ctx = document.getElementById('performance-chart').getContext('2d'); // Lấy context vẽ biểu đồ
    
    const labels = initialData.map(d => new Date(d.timestamp).toLocaleTimeString()); // Ánh xạ danh sách nhãn thời gian trục hoành
    const nvdaData = initialData.map(d => d.nvda);                        // Ánh xạ mảng dữ liệu giá cổ phiếu NVDA
    const tsmData = initialData.map(d => d.tsm);                          // Ánh xạ mảng dữ liệu giá cổ phiếu TSM
    const soxData = initialData.map(d => d.sox);                          // Ánh xạ mảng điểm số chỉ số index SOX

    liveChart = new Chart(ctx, {                                          // Tạo thể hiện Chart.js mới
        type: 'line',                                                     // Kiểu biểu đồ dạng đường vẽ (Line)
        data: {
            labels: labels,                                               // Cung cấp nhãn trục X
            datasets: [                                                   // Danh sách các bộ đường vẽ trên biểu đồ
                {
                    label: 'NVIDIA (NVDA)',                               // Tên đường vẽ 1
                    data: nvdaData,                                       // Dữ liệu giá trị y tương ứng
                    borderColor: '#00f0ff',                               // Màu đường vẽ là xanh ngọc (Cyan)
                    borderWidth: 2,                                       // Độ dày nét vẽ là 2 pixel
                    pointRadius: 0,                                       // Ẩn chấm tròn mặc định để đường liền nét đẹp mắt
                    pointHoverRadius: 6,                                  // Bán kính chấm khi rê chuột qua là 6 pixel
                    pointHitRadius: 15,                                   // Vùng nhận chuột mở rộng là 15 pixel xung quanh
                    yAxisID: 'y-left',                                    // Chỉ định hiển thị theo thang chia trục Y bên trái
                    tension: 0.15                                         // Độ cong nhẹ của đường vẽ tránh gấp khúc
                },
                {
                    label: 'TSMC (TSM)',                                  // Tên đường vẽ 2
                    data: tsmData,
                    borderColor: '#00ff66',                               // Màu đường vẽ là xanh lá cây Neon
                    borderWidth: 2,
                    pointRadius: 0,
                    pointHoverRadius: 6,
                    pointHitRadius: 15,
                    yAxisID: 'y-left',                                    // Cũng thuộc thang trục Y bên trái (đơn vị Đô-la $)
                    tension: 0.15
                },
                {
                    label: 'SOX Index',                                   // Tên đường vẽ 3
                    data: soxData,
                    borderColor: '#bd00ff',                               // Màu đường vẽ là màu tím Neon
                    borderWidth: 2,
                    pointRadius: 0,
                    pointHoverRadius: 6,
                    pointHitRadius: 15,
                    yAxisID: 'y-right',                                   // Liên kết với trục Y riêng bên phải (đơn vị Điểm số)
                    tension: 0.15
                }
            ]
        },
        options: {
            responsive: true,                                             // Tự động co giãn biểu đồ khớp với khung chứa
            maintainAspectRatio: false,                                   // Cho phép tự do giãn tỉ lệ đứng không bị cố định
            interaction: {
                intersect: false,                                         // Bật di chuột ngang: không cần trỏ trực tiếp vào nét vẽ
                mode: 'index'                                             // Bật cột dọc: hiển thị giá trị toàn bộ đường vẽ tại mốc X đó
            },
            scales: {
                x: {
                    grid: { color: 'rgba(255,255,255,0.03)' },             // Vẽ lưới dọc màu xám cực nhạt
                    ticks: { color: '#8899b5', font: { size: 9 } }        // Định dạng chữ nhãn thời gian nhỏ gọn
                },
                'y-left': {
                    position: 'left',                                     // Vị trí trục Y bên trái
                    grid: { color: 'rgba(255,255,255,0.03)' },             // Vẽ lưới ngang mờ
                    ticks: { color: '#00f0ff', font: { size: 9 } },       // Màu số trục Y theo màu Cyan
                    title: { display: true, text: 'NVDA/TSM Price ($)', color: '#00f0ff', font: { size: 10 } } // Nhãn trục
                },
                'y-right': {
                    position: 'right',                                    // Vị trí trục Y bên phải
                    grid: { drawOnChartArea: false },                     // Tránh vẽ đè nét lưới chéo ngang của trục phải lên trục trái
                    ticks: { color: '#bd00ff', font: { size: 9 } },       // Màu số trục Y theo màu tím
                    title: { display: true, text: 'SOX Index (points)', color: '#bd00ff', font: { size: 10 } }
                }
            },
            plugins: {
                legend: { display: false }                                // Vô hiệu hóa chú giải mặc định của Chart.js
            }
        }
    });
}

// Cập nhật điểm dữ liệu báo giá mới vào biểu đồ dạng cuốn chiếu
function updateChart(newPoint) {
    if (!liveChart) return;                                               // Kiểm tra an toàn biểu đồ đã tồn tại chưa
    
    const timeLabel = new Date(newPoint.timestamp).toLocaleTimeString(); // Định dạng mốc thời gian nhận tick mới
    
    liveChart.data.labels.push(timeLabel);                                // Đẩy nhãn thời gian mới vào cuối biểu đồ
    liveChart.data.datasets[0].data.push(newPoint.nvda);                  // Đẩy giá NVDA mới
    liveChart.data.datasets[1].data.push(newPoint.tsm);                   // Đẩy giá TSM mới
    liveChart.data.datasets[2].data.push(newPoint.sox);                   // Đẩy điểm SOX mới

    if (liveChart.data.labels.length > maxChartTicks) {                   // Nếu vượt quá giới hạn hiển thị tối đa (20 điểm)
        liveChart.data.labels.shift();                                    // Loại bỏ mốc thời gian cũ nhất ở đầu trục
        liveChart.data.datasets[0].data.shift();                          // Loại bỏ dữ liệu cũ của NVDA
        liveChart.data.datasets[1].data.shift();                          // Loại bỏ dữ liệu cũ của TSM
        liveChart.data.datasets[2].data.shift();                          // Loại bỏ dữ liệu cũ của SOX
    }
    
    liveChart.update('none');                                             // Cập nhật biểu đồ ở chế độ không chạy chuyển động mượt phụ để tối ưu CPU
}

// =================================================================================
// 5. LUỒNG DỮ LIỆU TELEMETRY (SERVER-SENT EVENTS - SSE)
// =================================================================================
function initSseTelemetry() {
    logConsole('Connecting to Telemetry stream (/api/pulse/stream)...', 'system'); // Ghi log thông báo kết nối
    
    const eventSource = new EventSource('/api/pulse/stream');             // Thiết lập luồng bắt đầu kết nối SSE đến endpoint API
    
    eventSource.onopen = () => {                                          // Trình nghe sự kiện khi kết nối thành công mở ra
        sseStatus.textContent = 'Connected';                              // Cập nhật chữ hiển thị trạng thái kết nối
        sseStatus.className = 'status-badge connected';                    // Chuyển màu nhãn badge sang màu xanh lá
        logConsole('SSE Telemetry stream connected successfully.', 'success'); // Ghi log báo công
    };
    
    eventSource.onmessage = (event) => {                                  // Trình nghe khi nhận được gói tin dữ liệu mới từ máy chủ
        try {
            const metric = JSON.parse(event.data);                        // Giải mã chuỗi JSON nhận được thành đối tượng Javascript
            const config = TelemetryConfig[metric.metricKey];             // Tìm cấu hình tương ứng trong bản đồ TelemetryConfig
            if (!config) return;                                          // Kiểm tra an toàn: thoát nếu gói dữ liệu lạ

            // Ghi nhật ký chi tiết thông số nhận được
            logConsole(`TELEMETRY UPDATE: ${metric.metricKey} -> ${metric.value.toFixed(config.decimals)} (${metric.pulseStatus})`, 'incoming');
            
            // Cập nhật giá trị số mới nhất lên thẻ số liệu đo đạc
            document.getElementById(config.valId).textContent = metric.value.toFixed(config.decimals);
            
            // Cập nhật nhãn trạng thái và đắp class CSS màu tương ứng
            const statusEl = document.getElementById(config.statusId);
            statusEl.textContent = metric.pulseStatus;
            statusEl.className = 'status-tag ' + metric.pulseStatus.toLowerCase();
            
            // Tính toán và định dạng chuỗi tỷ lệ phần trăm thay đổi (+/-%)
            const changeEl = document.getElementById(config.changeId);
            const dir = metric.changeRate > 0 ? '+' : '';                 // Thêm dấu cộng nếu số dương
            changeEl.textContent = `${dir}${metric.changeRate.toFixed(2)}%`;
            // Gán màu đỏ nếu âm, xanh lá nếu dương, xám nếu đứng im
            changeEl.className = 'change-rate ' + (metric.changeRate > 0 ? 'positive' : metric.changeRate < 0 ? 'negative' : 'neutral');
            
            // Đổ dữ liệu phụ: Độ lệch chuẩn và tổng số lượng mẫu vào thẻ
            document.getElementById(config.sdId).textContent = metric.standardDeviation.toFixed(2);
            document.getElementById(config.nId).textContent = metric.sampleCount;
            
            // Chạy hiệu ứng nháy viền thẻ card và kích hoạt dao động sóng nhịp tim mạnh mẽ
            flashCard(config.cardId, metric.changeRate >= 0 ? 'up' : 'down');
            excitePulseWave();
            
        } catch (err) {
            // Loại bỏ dòng cảnh báo kết nối thông thường từ máy chủ để tránh rác log
            if (event.data !== 'connected' && event.data !== 'keep-alive') {
                logConsole('SSE Event data parsing failed: ' + err.message, 'error');
            }
        }
    };
    
    eventSource.onerror = () => {                                         // Trình nghe sự kiện khi có sự cố ngắt kết nối luồng SSE
        sseStatus.textContent = 'Disconnected';                           // Đổi chữ hiển thị
        sseStatus.className = 'status-badge disconnected';                 // Đổi nhãn màu sang đỏ báo động
        logConsole('SSE disconnected. Reconnecting automatically...', 'error'); // Báo lỗi ngắt kết nối
    };
}

// Hàm tải dữ liệu trạng thái telemetry hiện thời từ cơ sở dữ liệu khi bắt đầu chạy trang
async function fetchTelemetryInitial() {
    try {
        const res = await fetch('/api/pulse/metrics');                    // Thực thi HTTP GET lấy dữ liệu ban đầu
        if (!res.ok) throw new Error('Status: ' + res.status);             // Quăng lỗi nếu phản hồi không thành công
        const data = await res.json();                                    // Giải nén cấu trúc JSON thành mảng
        
        data.forEach(metric => {                                          // Duyệt qua từng bản ghi metric
            const config = TelemetryConfig[metric.metricKey];             // Định vị thuộc tính HTML từ config
            if (!config) return;                                          // Bỏ qua nếu ko cấu hình
            
            // Cập nhật giá trị, trạng thái, biến động phần trăm và thông số thống kê khởi điểm
            document.getElementById(config.valId).textContent = metric.value.toFixed(config.decimals);
            
            const statusEl = document.getElementById(config.statusId);
            statusEl.textContent = metric.pulseStatus;
            statusEl.className = 'status-tag ' + metric.pulseStatus.toLowerCase();
            
            const changeEl = document.getElementById(config.changeId);
            const dir = metric.changeRate > 0 ? '+' : '';
            changeEl.textContent = `${dir}${metric.changeRate.toFixed(2)}%`;
            changeEl.className = 'change-rate ' + (metric.changeRate > 0 ? 'positive' : metric.changeRate < 0 ? 'negative' : 'neutral');
            
            document.getElementById(config.sdId).textContent = metric.standardDeviation.toFixed(2);
            document.getElementById(config.nId).textContent = metric.sampleCount;
            
            logConsole(`Database metric loaded: ${metric.metricKey} = ${metric.value.toFixed(config.decimals)}`, 'system');
        });
    } catch (err) {
        logConsole('Failed to retrieve initial database telemetry: ' + err.message, 'error'); // Báo lỗi nếu nạp data thất bại
    }
}

// =================================================================================
// 6. KÊNH BÁO GIÁ CỔ PHIẾU THỜI GIAN THỰC (SIGNALR WEBSOCKETS)
// =================================================================================
let lastStockPoint = null;                                                // Biến lưu trữ báo giá gần nhất để so sánh xu hướng lên xuống

function initSignalrTicker() {
    logConsole('Connecting to SignalR stock hub (/api/market/hub)...', 'system');
    
    const connection = new signalR.HubConnectionBuilder()                 // Khởi tạo trình xây dựng kết nối SignalR
        .withUrl('/api/market/hub')                                       // Chỉ định đường dẫn WebSocket Hub phía backend
        .withAutomaticReconnect()                                         // Cấu hình tự động kết nối lại khi rớt mạng
        .build();                                                         // Tạo đối tượng kết nối hoàn chỉnh
        
    connection.onreconnecting(() => {                                     // Kích hoạt khi SignalR mất mạng và đang cố kết nối lại
        signalrStatus.textContent = 'Connecting...';
        signalrStatus.className = 'status-badge connecting';
        logConsole('SignalR disconnecting. Reconnecting...', 'error');
    });
    
    connection.onreconnected(() => {                                      // Kích hoạt khi quá trình kết nối lại tự động thành công
        signalrStatus.textContent = 'Connected';
        signalrStatus.className = 'status-badge connected';
        logConsole('SignalR link re-established.', 'success');
    });

    connection.onclose(() => {                                            // Kích hoạt khi kết nối SignalR bị đóng hoàn toàn
        signalrStatus.textContent = 'Disconnected';
        signalrStatus.className = 'status-badge disconnected';
        logConsole('SignalR socket link closed.', 'error');
    });
    
    // Đăng ký nhận sự kiện cập nhật giá cổ phiếu thời gian thực từ hub
    connection.on('ReceiveMarketUpdate', (newPoint) => {
        const prev = lastStockPoint;                                      // Lưu mốc giá trước để so sánh tăng hay giảm
        lastStockPoint = newPoint;                                        // Ghi đè mốc giá mới nhất
        
        logConsole(`MARKET TICK: NVDA=$${newPoint.nvda} TSM=$${newPoint.tsm} SOX=${newPoint.sox}pts`, 'incoming');
        
        // Đổ thông tin giá trị cổ phiếu hiện tại lên giao diện các thẻ card
        document.getElementById('val-nvda').textContent = newPoint.nvda.toFixed(2);
        document.getElementById('val-tsm').textContent = newPoint.tsm.toFixed(2);
        document.getElementById('val-sox').textContent = newPoint.sox.toFixed(2);
        
        // Thiết lập tỉ lệ thay đổi giá NVDA kèm màu sắc tăng (xanh) / giảm (đỏ)
        const chgNvda = document.getElementById('change-nvda');
        chgNvda.textContent = `${newPoint.nvdaChange >= 0 ? '+' : ''}${newPoint.nvdaChange.toFixed(2)}%`;
        chgNvda.className = 'change-rate ' + (newPoint.nvdaChange >= 0 ? 'positive' : 'negative');
        
        // Thiết lập tỉ lệ thay đổi giá TSM
        const chgTsm = document.getElementById('change-tsm');
        chgTsm.textContent = `${newPoint.tsmChange >= 0 ? '+' : ''}${newPoint.tsmChange.toFixed(2)}%`;
        chgTsm.className = 'change-rate ' + (newPoint.tsmChange >= 0 ? 'positive' : 'negative');
        
        // Thiết lập tỉ lệ thay đổi điểm số index SOX
        const chgSox = document.getElementById('change-sox');
        chgSox.textContent = `${newPoint.soxChange >= 0 ? '+' : ''}${newPoint.soxChange.toFixed(2)}%`;
        chgSox.className = 'change-rate ' + (newPoint.soxChange >= 0 ? 'positive' : 'negative');
        
        // Nếu có mốc giá cũ trước đó, so sánh để nháy màu viền Neon cho thẻ card cổ phiếu
        if (prev) {
            flashCard('card-nvda', newPoint.nvda >= prev.nvda ? 'up' : 'down');
            flashCard('card-tsm', newPoint.tsm >= prev.tsm ? 'up' : 'down');
            flashCard('card-sox', newPoint.sox >= prev.sox ? 'up' : 'down');
        }
        
        updateChart(newPoint);                                            // Đẩy điểm giá vừa nhận vào biểu đồ Chart.js
    });
    
    // Thực thi bắt đầu kết nối SignalR Socket lên server
    connection.start()
        .then(() => {
            signalrStatus.textContent = 'Connected';
            signalrStatus.className = 'status-badge connected';
            logConsole('SignalR real-time stock link connected.', 'success');
        })
        .catch(err => {
            signalrStatus.textContent = 'Error';
            signalrStatus.className = 'status-badge disconnected';
            logConsole('SignalR socket initialization failed: ' + err.message, 'error');
        });
}

// Tải lịch sử báo giá cũ từ máy chủ để dựng đường vẽ ban đầu cho biểu đồ đường
async function fetchStockInitial() {
    try {
        const res = await fetch('/api/market/history');                    // Gửi HTTP GET lấy lịch sử giao dịch
        if (!res.ok) throw new Error('Status: ' + res.status);
        const data = await res.json();
        
        if (data && data.length > 0) {                                    // Kiểm tra mảng lịch sử có phần tử không
            lastStockPoint = data[data.length - 1];                       // Lấy bản ghi cuối cùng làm giá trị hiện thời
            
            // Gán giá trị cổ phiếu và phần trăm thay đổi ban đầu
            document.getElementById('val-nvda').textContent = lastStockPoint.nvda.toFixed(2);
            document.getElementById('val-tsm').textContent = lastStockPoint.tsm.toFixed(2);
            document.getElementById('val-sox').textContent = lastStockPoint.sox.toFixed(2);
            
            document.getElementById('change-nvda').textContent = `${lastStockPoint.nvdaChange >= 0 ? '+' : ''}${lastStockPoint.nvdaChange.toFixed(2)}%`;
            document.getElementById('change-tsm').textContent = `${lastStockPoint.tsmChange >= 0 ? '+' : ''}${lastStockPoint.tsmChange.toFixed(2)}%`;
            document.getElementById('change-sox').textContent = `${lastStockPoint.soxChange >= 0 ? '+' : ''}${lastStockPoint.soxChange.toFixed(2)}%`;
            
            initChart(data);                                              // Khởi tạo biểu đồ với toàn bộ mảng lịch sử nạp được
            logConsole(`Loaded ${data.length} stock historical points into chart.`, 'system');
        } else {
            initChart([]);                                                // Khởi tạo biểu đồ trống nếu ko có lịch sử
        }
    } catch (err) {
        logConsole('Failed to retrieve initial stock history: ' + err.message, 'error');
        initChart([]);
    }
}

// =================================================================================
// 7. TRÌNH ĐIỀU KHIỂN HỖ TRỢ HIỂN THỊ ĐƠN VỊ CỦA BỘ NHẬP SIMULATOR
// =================================================================================
metricSelect.addEventListener('change', () => {                           // Sự kiện thay đổi lựa chọn metric giả lập
    const key = metricSelect.value;                                       // Lấy mã khóa đo lường được chọn
    let unit = 'value';                                                   // Đơn vị mặc định là chữ value chung chung
    if (key === 'BOOK_TO_BILL') unit = 'ratio';                           // Đơn vị của Book to Bill là tỉ số
    else if (key === 'LEAD_TIME') unit = 'weeks';                         // Đơn vị của Lead Time là tuần
    else if (key === 'YIELD_RATE') unit = '%';                            // Đơn vị Yield rate là phần trăm
    else if (key === 'CAPEX') unit = '$B USD';                            // Đơn vị CapEx là tỷ đô Mỹ
    unitIndicator.textContent = unit;                                     // Gán đơn vị đo lường lên nhãn của input nhập liệu
});

// =================================================================================
// 8. BẢO MẬT PHIÊN LÀM VIỆC, COOKIES VÀ BẢNG ĐIỀU KHIỂN GIẢ LẬP
// =================================================================================

let currentUser = null;                                                   // Đối tượng chứa thông tin Operator hiện thời đăng nhập

const userProfileNav = document.getElementById('user-profile-nav');        // Khung hồ sơ tài khoản góc trên bên phải

// Gán liên kết chuyển trang cho nút Đăng Nhập / Đăng Ký tĩnh ngoài trang chủ
const initSigninBtn = document.getElementById('nav-signin-btn');
const initSignupBtn = document.getElementById('nav-signup-btn');
if (initSigninBtn) initSigninBtn.addEventListener('click', () => { window.location.href = '/login.html'; });
if (initSignupBtn) initSignupBtn.addEventListener('click', () => { window.location.href = '/register.html'; });

const simSecurityBadge = document.getElementById('simulator-security-badge'); // Nhãn chứng thực bảo mật bảng điều khiển
const simAuthMsg = document.getElementById('simulator-auth-msg');          // Đoạn text thông báo chi tiết bảo mật
const submitBtn = document.getElementById('submit-btn');                  // Nút bấm POST dữ liệu telemetry

// Wrapper xử lý HTTP Fetch thông thường, tích hợp cơ chế tự động xoay vòng Refresh Token tĩnh lặng (Silent Token Rotation)
async function authenticatedFetch(url, options = {}) {
    options.headers = options.headers || {};                              // Thiết lập tiêu đề headers mặc định rỗng nếu chưa có
    
    let res = await fetch(url, options);                                  // Thực thi gửi yêu cầu HTTP nguyên bản lên server
    
    if (res.status === 401) {                                             // Nếu máy chủ từ chối với mã lỗi 401 (Hết hạn Access Token)
        logConsole('Access token expired. Requesting silent token rotation...', 'system');
        
        try {
            // Gửi yêu cầu gia hạn Token âm thầm (Server tự đọc Refresh Token HttpOnly cookie để sinh Access Token mới)
            const refreshRes = await fetch('/api/auth/refresh-token', { method: 'POST' });
            
            if (refreshRes.ok) {                                          // Nếu máy chủ cấp lại Token mới thành công
                const refreshData = await refreshRes.json();              // Giải nén cấu trúc JSON
                currentUser = refreshData.user;                           // Cập nhật thông tin operator hiện tại
                updateAuthUI();                                           // Đồng bộ lại giao diện người dùng
                logConsole('Silent token refresh succeeded. Retrying request...', 'success');
                
                res = await fetch(url, options);                          // Thực hiện lại yêu cầu HTTP nguyên bản lần hai với Cookie mới
            } else {
                throw new Error('Refresh token rejected');                // Quăng lỗi nếu Refresh Token cũng hết hạn/không hợp lệ
            }
        } catch (err) {
            logConsole('Session expired. Redirecting to sign in page...', 'error'); // Báo động hết phiên làm việc
            currentUser = null;                                           // Reset bộ nhớ user về null
            updateAuthUI();                                               // Đưa UI về trạng thái chưa đăng nhập
            
            setTimeout(() => {
                window.location.href = '/login.html';                      // Chuyển hướng người dùng về trang Đăng nhập sau 1 giây
            }, 1000);
        }
    }
    
    return res;                                                           // Trả về kết quả HTTP Response thu được
}

// Cập nhật giao diện thanh tài khoản & Thực hiện phân quyền chức năng giả lập (Role check)
function updateAuthUI() {
    if (currentUser) {
        // Giao diện khi người dùng ĐÃ ĐĂNG NHẬP
        const roleLower = currentUser.role.toLowerCase();                 // Lấy tên Role dạng chữ thường làm tên class CSS màu sắc
        userProfileNav.innerHTML = `
            <div class="user-badge">
                <span class="user-name">${currentUser.fullName}</span>
                <span class="user-role-tag ${roleLower}">${currentUser.role}</span>
            </div>
            <button id="nav-signout-btn" class="btn-signout">Sign Out</button>
        `;
        
        const signoutBtn = document.getElementById('nav-signout-btn');    // Lấy nút đăng xuất động vừa tạo ra
        if (signoutBtn) signoutBtn.addEventListener('click', handleLogout); // Đăng ký sự kiện click đăng xuất
        
        // Phân quyền Ingestion: Chỉ vai trò Admin hoặc IoTDevice mới được phép bấm nút bắn dữ liệu telemetry
        if (currentUser.role === 'Admin' || currentUser.role === 'IoTDevice') {
            simSecurityBadge.textContent = 'AUTHORIZED';                  // Hiện huy hiệu màu xanh
            simSecurityBadge.className = 'secure-badge';
            simAuthMsg.innerHTML = `Logged in as <strong>${currentUser.fullName}</strong> (${currentUser.role}). Ingestion active.`;
            submitBtn.disabled = false;                                   // Kích hoạt lại nút Submit
            submitBtn.classList.remove('disabled');                       // Loại bỏ thuộc tính mờ nút
        } else {
            // Vai trò không đủ quyền hạn (ví dụ: Standard User)
            simSecurityBadge.textContent = 'FORBIDDEN';                   // Hiện huy hiệu đỏ cấm
            simSecurityBadge.className = 'secure-badge error';
            simAuthMsg.innerHTML = `Account role <strong>${currentUser.role}</strong> lacks ingestion clearance. Admin/IoTDevice required.`;
            submitBtn.disabled = true;                                    // Vô hiệu hóa nút Submit
            submitBtn.classList.add('disabled');
        }
    } else {
        // Giao diện khi người dùng CHƯA ĐĂNG NHẬP (Khách vãng lai)
        userProfileNav.innerHTML = `
            <button id="nav-signin-btn" class="btn-glow-small">Sign In</button>
            <button id="nav-signup-btn" class="btn-glow-small outline">Sign Up</button>
        `;
        
        const signinBtn = document.getElementById('nav-signin-btn');
        const signupBtn = document.getElementById('nav-signup-btn');
        
        if (signinBtn) signinBtn.addEventListener('click', () => { window.location.href = '/login.html'; });
        if (signupBtn) signupBtn.addEventListener('click', () => { window.location.href = '/register.html'; });
        
        simSecurityBadge.textContent = 'UNAUTHORIZED';                    // Báo chưa xác thực (Đỏ)
        simSecurityBadge.className = 'secure-badge error';
        simAuthMsg.textContent = 'Please sign in with an Admin or IoTDevice account to enable telemetry ingestion.';
        submitBtn.disabled = true;
        submitBtn.classList.add('disabled');
    }
}

// Xử lý hành động bấm nút Đăng Xuất (Sign Out)
async function handleLogout() {
    try {
        await authenticatedFetch('/api/auth/logout', { method: 'POST' });  // Gọi API logout phía backend để xóa cookies JWT
        currentUser = null;                                               // Xóa trạng thái user cục bộ
        updateAuthUI();                                                   // Chuyển đổi giao diện về chưa đăng nhập
        logConsole('Operator session terminated. Cookies cleared.', 'system');
    } catch (err) {
        currentUser = null;                                               // Đảm bảo xóa trạng thái user kể cả khi API gặp lỗi mạng
        updateAuthUI();
    }
}

// Gửi form giả lập điểm dữ liệu telemetry mới lên Ingestion Pipeline
simulatorForm.addEventListener('submit', async (e) => {
    e.preventDefault();                                                   // Chặn hành động tải lại trang mặc định của form submit
    simAlert.className = 'alert-box hide';                                // Ẩn mọi cảnh báo cũ đang hiển thị
    
    const key = metricSelect.value;                                       // Lấy khóa metric muốn gửi (ví dụ: LEAD_TIME)
    const value = parseFloat(document.getElementById('metric-value').value); // Lấy trị số người dùng nhập vào dạng float
    
    logConsole(`Operator dispatching secure telemetry: [${key} = ${value}]...`, 'system');
    
    try {
        // Thực thi gửi POST API kèm dữ liệu JSON thông qua authenticatedFetch bảo mật
        const ingestRes = await authenticatedFetch('/api/pulse/ingest', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                metricKey: key,
                value: value,
                region: 'APAC-SOUTH-HUB',
                source: 'Diagnostics Operator Console'
            })
        });
        
        if (ingestRes.status === 403) {
            throw new Error('Forbidden. Ingestion privilege required.');  // Lỗi thiếu quyền hạn vai trò tài khoản
        }
        if (ingestRes.status === 401) {
            throw new Error('Unauthorized. Please sign in.');            // Lỗi chưa đăng nhập
        }
        if (!ingestRes.ok) {
            throw new Error(`Server returned status code ${ingestRes.status}`); // Các lỗi hệ thống khác
        }
        
        const ingestData = await ingestRes.json();                        // Phân giải JSON dữ liệu phản hồi trả về
        
        simAlert.textContent = 'TELEMETRY DISPATCHED SUCCESSFULLY: ' + ingestData.message; // Gán nội dung thông báo thành công
        simAlert.className = 'alert-box success';                         // Chuyển hộp thoại cảnh báo sang màu xanh lục
        logConsole(`Telemetry ingestion accepted: ${ingestData.message}`, 'success');
        
        document.getElementById('metric-value').value = '';               // Xóa sạch ô nhập số để sẵn sàng cho lần nhập tiếp theo
    } catch (err) {
        simAlert.textContent = 'PIPELINE ERROR: ' + err.message;          // Hiển thị nội dung lỗi đường ống dẫn
        simAlert.className = 'alert-box error';                           // Chuyển hộp cảnh báo sang màu đỏ báo lỗi
        logConsole('Diagnostics ingestion error: ' + err.message, 'error');
    }
});

// Kiểm tra xem trình duyệt có cookie phiên làm việc hợp lệ trước đó không khi tải trang lần đầu
async function checkActiveSession() {
    try {
        const res = await fetch('/api/auth/me');                          // Gọi API kiểm tra danh tính hiện tại
        if (res.ok) {
            currentUser = await res.json();                               // Nếu thành công, nhận diện Operator
            updateAuthUI();
            logConsole(`Active session detected for ${currentUser.fullName} (${currentUser.role}).`, 'success');
        } else if (res.status === 401) {
            // Trường hợp token hết hạn, cố gắng làm mới âm thầm bằng Refresh Token ngay lập tức
            const refreshRes = await fetch('/api/auth/refresh-token', { method: 'POST' });
            if (refreshRes.ok) {
                const refreshData = await refreshRes.json();
                currentUser = refreshData.user;
                updateAuthUI();
                logConsole(`Silent refresh successful. Operator: ${currentUser.fullName}.`, 'success');
            } else {
                updateAuthUI();                                           // Đưa về trạng thái chưa đăng nhập nếu refresh thất bại
            }
        } else {
            updateAuthUI();
        }
    } catch (err) {
        updateAuthUI();
    }
}

// =================================================================================
// 9. TRÌNH TỰ KHỞI ĐỘNG HỆ THỐNG TUẦN TỰ KHI LOAD TRANG
// =================================================================================
checkActiveSession().then(() => {                                         // Bước 1: Xác thực danh tính Operator trước
    fetchTelemetryInitial();                                              // Bước 2: Nạp dữ liệu các thẻ đo lường Telemetry chiến lược
    fetchStockInitial().then(() => {                                      // Bước 3: Nạp lịch sử báo giá cổ phiếu
        initSseTelemetry();                                               // Bước 4: Khởi chạy luồng đẩy dữ liệu đo lường real-time (SSE)
        initSignalrTicker();                                              // Bước 5: Khởi chạy WebSocket báo giá cổ phiếu real-time (SignalR)
    });
});
