// Unified Client Application Logic for Semiconductor Strategy & Market Tickers

// 1. Core Configuration Mapping for HTML elements
const TelemetryConfig = {
    BOOK_TO_BILL: { valId: 'val-book-to-bill', statusId: 'status-book-to-bill', changeId: 'change-book-to-bill', sdId: 'sd-book-to-bill', nId: 'n-book-to-bill', cardId: 'card-book-to-bill', decimals: 2 },
    LEAD_TIME: { valId: 'val-lead-time', statusId: 'status-lead-time', changeId: 'change-lead-time', sdId: 'sd-lead-time', nId: 'n-lead-time', cardId: 'card-lead-time', decimals: 1 },
    YIELD_RATE: { valId: 'val-yield-rate', statusId: 'status-yield-rate', changeId: 'change-yield-rate', sdId: 'sd-yield-rate', nId: 'n-yield-rate', cardId: 'card-yield-rate', decimals: 1 },
    CAPEX: { valId: 'val-capex', statusId: 'status-capex', changeId: 'change-capex', sdId: 'sd-capex', nId: 'n-capex', cardId: 'card-capex', decimals: 1 }
};

const StockConfig = {
    NVDA: { valId: 'val-nvda', changeId: 'change-nvda', cardId: 'card-nvda' },
    TSM: { valId: 'val-tsm', changeId: 'change-tsm', cardId: 'card-tsm' },
    SOX: { valId: 'val-sox', changeId: 'change-sox', cardId: 'card-sox' }
};

// UI Elements
const sseStatus = document.getElementById('sse-status');
const signalrStatus = document.getElementById('signalr-status');
const consoleLogs = document.getElementById('console-logs');
const clearConsoleBtn = document.getElementById('clear-console');
const metricSelect = document.getElementById('metric-select');
const unitIndicator = document.getElementById('unit-indicator');
const simulatorForm = document.getElementById('simulator-form');
const simAlert = document.getElementById('sim-alert');

// 2. Oscilloscope Pulse Canvas Animation
const canvas = document.getElementById('pulse-wave');
const ctx = canvas.getContext('2d');
let waveOffset = 0;
let flashWaveIntensity = 0;

function resizeCanvas() {
    canvas.width = canvas.parentElement.clientWidth;
    canvas.height = canvas.parentElement.clientHeight;
}
window.addEventListener('resize', resizeCanvas);
resizeCanvas();

function animatePulseWave() {
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    
    // Grid Lines
    ctx.strokeStyle = 'rgba(255, 255, 255, 0.01)';
    ctx.lineWidth = 1;
    for (let x = 0; x < canvas.width; x += 30) {
        ctx.beginPath(); ctx.moveTo(x, 0); ctx.lineTo(x, canvas.height); ctx.stroke();
    }
    for (let y = 0; y < canvas.height; y += 15) {
        ctx.beginPath(); ctx.moveTo(0, y); ctx.lineTo(canvas.width, y); ctx.stroke();
    }
    
    // Pulse Line
    ctx.beginPath();
    ctx.lineWidth = 1.5;
    
    const gradient = ctx.createLinearGradient(0, 0, canvas.width, 0);
    gradient.addColorStop(0, 'rgba(0, 240, 255, 0.6)');
    gradient.addColorStop(0.5, 'rgba(189, 0, 255, 0.6)');
    gradient.addColorStop(1, 'rgba(0, 240, 255, 0.6)');
    ctx.strokeStyle = gradient;
    ctx.shadowBlur = 4 + (flashWaveIntensity * 12);
    ctx.shadowColor = '#00f0ff';

    const centerY = canvas.height / 2;
    const waveFrequency = 0.02;
    const waveAmplitude = 10;
    
    for (let x = 0; x < canvas.width; x++) {
        let y = Math.sin((x * waveFrequency) + waveOffset) * waveAmplitude;
        
        // Add heartbeat spikes
        const spike = (x + (waveOffset * 80)) % 240;
        if (spike > 100 && spike < 125) {
            const ratio = (spike - 100) / 25;
            y += Math.sin(ratio * Math.PI * 4) * (waveAmplitude * 2.2 + (flashWaveIntensity * 25));
        }

        if (x === 0) {
            ctx.moveTo(x, centerY + y);
        } else {
            ctx.lineTo(x, centerY + y);
        }
    }
    ctx.stroke();
    ctx.shadowBlur = 0; // reset
    
    waveOffset -= 0.04 + (flashWaveIntensity * 0.04);
    if (flashWaveIntensity > 0) flashWaveIntensity -= 0.02;
    
    requestAnimationFrame(animatePulseWave);
}
requestAnimationFrame(animatePulseWave);

// Trigger wave excitement
function excitePulseWave() {
    flashWaveIntensity = 1.0;
}

// 3. Diagnostics Logger Console Helpers
function logConsole(message, type = 'info') {
    const time = new Date().toLocaleTimeString();
    const line = document.createElement('div');
    line.className = `log-line ${type}`;
    line.innerHTML = `[${time}] ${message}`;
    consoleLogs.appendChild(line);
    consoleLogs.scrollTop = consoleLogs.scrollHeight;
    
    while (consoleLogs.childElementCount > 40) {
        consoleLogs.removeChild(consoleLogs.firstChild);
    }
}

clearConsoleBtn.addEventListener('click', () => {
    consoleLogs.innerHTML = '';
    logConsole('Console logs cleared by operator.', 'system');
});

// Flash KPI Card on update
function flashCard(cardId, dir) {
    const cardEl = document.getElementById(cardId);
    if (!cardEl) return;
    
    const className = dir === 'up' ? 'flash-up' : 'flash-down';
    cardEl.classList.remove('flash-up', 'flash-down');
    void cardEl.offsetWidth; // force CSS reflow
    cardEl.classList.add(className);
}

// 4. Chart.js Live Ticker Setup
let liveChart;
const maxChartTicks = 20;

function initChart(initialData = []) {
    const ctx = document.getElementById('performance-chart').getContext('2d');
    
    const labels = initialData.map(d => new Date(d.timestamp).toLocaleTimeString());
    const nvdaData = initialData.map(d => d.nvda);
    const tsmData = initialData.map(d => d.tsm);
    const soxData = initialData.map(d => d.sox);

    liveChart = new Chart(ctx, {
        type: 'line',
        data: {
            labels: labels,
            datasets: [
                {
                    label: 'NVIDIA (NVDA)',
                    data: nvdaData,
                    borderColor: '#00f0ff',
                    borderWidth: 2,
                    pointRadius: 0,
                    yAxisID: 'y-left',
                    tension: 0.15
                },
                {
                    label: 'TSMC (TSM)',
                    data: tsmData,
                    borderColor: '#00ff66',
                    borderWidth: 2,
                    pointRadius: 0,
                    yAxisID: 'y-left',
                    tension: 0.15
                },
                {
                    label: 'SOX Index',
                    data: soxData,
                    borderColor: '#bd00ff',
                    borderWidth: 2,
                    pointRadius: 0,
                    yAxisID: 'y-right',
                    tension: 0.15
                }
            ]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            scales: {
                x: {
                    grid: { color: 'rgba(255,255,255,0.03)' },
                    ticks: { color: '#8899b5', font: { size: 9 } }
                },
                'y-left': {
                    position: 'left',
                    grid: { color: 'rgba(255,255,255,0.03)' },
                    ticks: { color: '#00f0ff', font: { size: 9 } },
                    title: { display: true, text: 'NVDA/TSM Price ($)', color: '#00f0ff', font: { size: 10 } }
                },
                'y-right': {
                    position: 'right',
                    grid: { drawOnChartArea: false },
                    ticks: { color: '#bd00ff', font: { size: 9 } },
                    title: { display: true, text: 'SOX Index (points)', color: '#bd00ff', font: { size: 10 } }
                }
            },
            plugins: {
                legend: { display: false } // custom legends are in index.html
            }
        }
    });
}

function updateChart(newPoint) {
    if (!liveChart) return;
    
    const timeLabel = new Date(newPoint.timestamp).toLocaleTimeString();
    
    liveChart.data.labels.push(timeLabel);
    liveChart.data.datasets[0].data.push(newPoint.nvda);
    liveChart.data.datasets[1].data.push(newPoint.tsm);
    liveChart.data.datasets[2].data.push(newPoint.sox);

    if (liveChart.data.labels.length > maxChartTicks) {
        liveChart.data.labels.shift();
        liveChart.data.datasets[0].data.shift();
        liveChart.data.datasets[1].data.shift();
        liveChart.data.datasets[2].data.shift();
    }
    
    liveChart.update('none'); // silent update
}

// 5. Connect and Process Server-Sent Events (SSE) Telemetry
function initSseTelemetry() {
    logConsole('Connecting to Telemetry stream (/api/pulse/stream)...', 'system');
    
    const eventSource = new EventSource('/api/pulse/stream');
    
    eventSource.onopen = () => {
        sseStatus.textContent = 'Connected';
        sseStatus.className = 'status-badge connected';
        logConsole('SSE Telemetry stream connected successfully.', 'success');
    };
    
    eventSource.onmessage = (event) => {
        try {
            const metric = JSON.parse(event.data);
            const config = TelemetryConfig[metric.metricKey];
            if (!config) return;

            logConsole(`TELEMETRY UPDATE: ${metric.metricKey} -> ${metric.value.toFixed(config.decimals)} (${metric.pulseStatus})`, 'incoming');
            
            // Update Card
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
            
            // Animation flash
            flashCard(config.cardId, metric.changeRate >= 0 ? 'up' : 'down');
            excitePulseWave();
            
        } catch (err) {
            if (event.data !== 'connected' && event.data !== 'keep-alive') {
                logConsole('SSE Event data parsing failed: ' + err.message, 'error');
            }
        }
    };
    
    eventSource.onerror = () => {
        sseStatus.textContent = 'Disconnected';
        sseStatus.className = 'status-badge disconnected';
        logConsole('SSE disconnected. Reconnecting automatically...', 'error');
    };
}

// Fetch Initial database states
async function fetchTelemetryInitial() {
    try {
        const res = await fetch('/api/pulse/metrics');
        if (!res.ok) throw new Error('Status: ' + res.status);
        const data = await res.json();
        
        data.forEach(metric => {
            const config = TelemetryConfig[metric.metricKey];
            if (!config) return;
            
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
        logConsole('Failed to retrieve initial database telemetry: ' + err.message, 'error');
    }
}

// 6. Connect and Process SignalR Tickers
let lastStockPoint = null;

function initSignalrTicker() {
    logConsole('Connecting to SignalR stock hub (/api/market/hub)...', 'system');
    
    const connection = new signalR.HubConnectionBuilder()
        .withUrl('/api/market/hub')
        .withAutomaticReconnect()
        .build();
        
    connection.onreconnecting(() => {
        signalrStatus.textContent = 'Connecting...';
        signalrStatus.className = 'status-badge connecting';
        logConsole('SignalR disconnecting. Reconnecting...', 'error');
    });
    
    connection.onreconnected(() => {
        signalrStatus.textContent = 'Connected';
        signalrStatus.className = 'status-badge connected';
        logConsole('SignalR link re-established.', 'success');
    });

    connection.onclose(() => {
        signalrStatus.textContent = 'Disconnected';
        signalrStatus.className = 'status-badge disconnected';
        logConsole('SignalR socket link closed.', 'error');
    });
    
    connection.on('ReceiveMarketUpdate', (newPoint) => {
        const prev = lastStockPoint;
        lastStockPoint = newPoint;
        
        logConsole(`MARKET TICK: NVDA=$${newPoint.nvda} TSM=$${newPoint.tsm} SOX=${newPoint.sox}pts`, 'incoming');
        
        // Update Stock UI
        document.getElementById('val-nvda').textContent = newPoint.nvda.toFixed(2);
        document.getElementById('val-tsm').textContent = newPoint.tsm.toFixed(2);
        document.getElementById('val-sox').textContent = newPoint.sox.toFixed(2);
        
        const chgNvda = document.getElementById('change-nvda');
        chgNvda.textContent = `${newPoint.nvdaChange >= 0 ? '+' : ''}${newPoint.nvdaChange.toFixed(2)}%`;
        chgNvda.className = 'change-rate ' + (newPoint.nvdaChange >= 0 ? 'positive' : 'negative');
        
        const chgTsm = document.getElementById('change-tsm');
        chgTsm.textContent = `${newPoint.tsmChange >= 0 ? '+' : ''}${newPoint.tsmChange.toFixed(2)}%`;
        chgTsm.className = 'change-rate ' + (newPoint.tsmChange >= 0 ? 'positive' : 'negative');
        
        const chgSox = document.getElementById('change-sox');
        chgSox.textContent = `${newPoint.soxChange >= 0 ? '+' : ''}${newPoint.soxChange.toFixed(2)}%`;
        chgSox.className = 'change-rate ' + (newPoint.soxChange >= 0 ? 'positive' : 'negative');
        
        // Flash triggers
        if (prev) {
            flashCard('card-nvda', newPoint.nvda >= prev.nvda ? 'up' : 'down');
            flashCard('card-tsm', newPoint.tsm >= prev.tsm ? 'up' : 'down');
            flashCard('card-sox', newPoint.sox >= prev.sox ? 'up' : 'down');
        }
        
        // Feed into Chart
        updateChart(newPoint);
    });
    
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

async function fetchStockInitial() {
    try {
        const res = await fetch('/api/market/history');
        if (!res.ok) throw new Error('Status: ' + res.status);
        const data = await res.json();
        
        if (data && data.length > 0) {
            lastStockPoint = data[data.length - 1];
            
            // Set latest stock UI states
            document.getElementById('val-nvda').textContent = lastStockPoint.nvda.toFixed(2);
            document.getElementById('val-tsm').textContent = lastStockPoint.tsm.toFixed(2);
            document.getElementById('val-sox').textContent = lastStockPoint.sox.toFixed(2);
            
            document.getElementById('change-nvda').textContent = `${lastStockPoint.nvdaChange >= 0 ? '+' : ''}${lastStockPoint.nvdaChange.toFixed(2)}%`;
            document.getElementById('change-tsm').textContent = `${lastStockPoint.tsmChange >= 0 ? '+' : ''}${lastStockPoint.tsmChange.toFixed(2)}%`;
            document.getElementById('change-sox').textContent = `${lastStockPoint.soxChange >= 0 ? '+' : ''}${lastStockPoint.soxChange.toFixed(2)}%`;
            
            initChart(data);
            logConsole(`Loaded ${data.length} stock historical points into chart.`, 'system');
        } else {
            initChart([]);
        }
    } catch (err) {
        logConsole('Failed to retrieve initial stock history: ' + err.message, 'error');
        initChart([]);
    }
}

// 7. Input select label helper
metricSelect.addEventListener('change', () => {
    const key = metricSelect.value;
    let unit = 'value';
    if (key === 'BOOK_TO_BILL') unit = 'ratio';
    else if (key === 'LEAD_TIME') unit = 'weeks';
    else if (key === 'YIELD_RATE') unit = '%';
    else if (key === 'CAPEX') unit = '$B USD';
    unitIndicator.textContent = unit;
});

// // ==========================================
// 8. Session, Cookie Auth & Ingestion Simulator Management
// ==========================================

let currentUser = null;

// UI Elements for Auth Navigation
const userProfileNav = document.getElementById('user-profile-nav');

// Bind immediately to initial static DOM buttons (prevents blocking before startup fetch resolves)
const initSigninBtn = document.getElementById('nav-signin-btn');
const initSignupBtn = document.getElementById('nav-signup-btn');
if (initSigninBtn) initSigninBtn.addEventListener('click', () => { window.location.href = '/login.html'; });
if (initSignupBtn) initSignupBtn.addEventListener('click', () => { window.location.href = '/register.html'; });

const simSecurityBadge = document.getElementById('simulator-security-badge');
const simAuthMsg = document.getElementById('simulator-auth-msg');
const submitBtn = document.getElementById('submit-btn');

// Wrapper for authenticated fetch that supports automatic silent refresh token rotation
async function authenticatedFetch(url, options = {}) {
    options.headers = options.headers || {};
    
    // Perform original request
    let res = await fetch(url, options);
    
    if (res.status === 401) {
        logConsole('Access token expired. Requesting silent token rotation...', 'system');
        
        try {
            // Attempt to refresh token (will read refresh_token cookie automatically)
            const refreshRes = await fetch('/api/auth/refresh-token', { method: 'POST' });
            
            if (refreshRes.ok) {
                const refreshData = await refreshRes.json();
                currentUser = refreshData.user;
                updateAuthUI();
                logConsole('Silent token refresh succeeded. Retrying request...', 'success');
                
                // Retry the original request
                res = await fetch(url, options);
            } else {
                throw new Error('Refresh token rejected');
            }
        } catch (err) {
            logConsole('Session expired. Redirecting to sign in page...', 'error');
            currentUser = null;
            updateAuthUI();
            
            // Redirect to login page after a short delay so operator can read the log
            setTimeout(() => {
                window.location.href = '/login.html';
            }, 1000);
        }
    }
    
    return res;
}

// Update UI based on current logged in user status
function updateAuthUI() {
    if (currentUser) {
        // Logged in state
        const roleLower = currentUser.role.toLowerCase();
        userProfileNav.innerHTML = `
            <div class="user-badge">
                <span class="user-name">${currentUser.fullName}</span>
                <span class="user-role-tag ${roleLower}">${currentUser.role}</span>
            </div>
            <button id="nav-signout-btn" class="btn-signout">Sign Out</button>
        `;
        
        const signoutBtn = document.getElementById('nav-signout-btn');
        if (signoutBtn) signoutBtn.addEventListener('click', handleLogout);
        
        // Update Simulator Ingestion Status
        if (currentUser.role === 'Admin' || currentUser.role === 'IoTDevice') {
            simSecurityBadge.textContent = 'AUTHORIZED';
            simSecurityBadge.className = 'secure-badge';
            simAuthMsg.innerHTML = `Logged in as <strong>${currentUser.fullName}</strong> (${currentUser.role}). Ingestion active.`;
            submitBtn.disabled = false;
            submitBtn.classList.remove('disabled');
        } else {
            simSecurityBadge.textContent = 'FORBIDDEN';
            simSecurityBadge.className = 'secure-badge error';
            simAuthMsg.innerHTML = `Account role <strong>${currentUser.role}</strong> lacks ingestion clearance. Admin/IoTDevice required.`;
            submitBtn.disabled = true;
            submitBtn.classList.add('disabled');
        }
    } else {
        // Logged out state
        userProfileNav.innerHTML = `
            <button id="nav-signin-btn" class="btn-glow-small">Sign In</button>
            <button id="nav-signup-btn" class="btn-glow-small outline">Sign Up</button>
        `;
        
        const signinBtn = document.getElementById('nav-signin-btn');
        const signupBtn = document.getElementById('nav-signup-btn');
        
        if (signinBtn) signinBtn.addEventListener('click', () => { window.location.href = '/login.html'; });
        if (signupBtn) signupBtn.addEventListener('click', () => { window.location.href = '/register.html'; });
        
        simSecurityBadge.textContent = 'UNAUTHORIZED';
        simSecurityBadge.className = 'secure-badge error';
        simAuthMsg.textContent = 'Please sign in with an Admin or IoTDevice account to enable telemetry ingestion.';
        submitBtn.disabled = true;
        submitBtn.classList.add('disabled');
    }
}

// Handle Logout
async function handleLogout() {
    try {
        await authenticatedFetch('/api/auth/logout', { method: 'POST' });
        currentUser = null;
        updateAuthUI();
        logConsole('Operator session terminated. Cookies cleared.', 'system');
    } catch (err) {
        currentUser = null;
        updateAuthUI();
    }
}

// Ingest Telemetry Submission
simulatorForm.addEventListener('submit', async (e) => {
    e.preventDefault();
    simAlert.className = 'alert-box hide';
    
    const key = metricSelect.value;
    const value = parseFloat(document.getElementById('metric-value').value);
    
    logConsole(`Operator dispatching secure telemetry: [${key} = ${value}]...`, 'system');
    
    try {
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
            throw new Error('Forbidden. Ingestion privilege required.');
        }
        if (ingestRes.status === 401) {
            throw new Error('Unauthorized. Please sign in.');
        }
        if (!ingestRes.ok) {
            throw new Error(`Server returned status code ${ingestRes.status}`);
        }
        
        const ingestData = await ingestRes.json();
        
        simAlert.textContent = 'TELEMETRY DISPATCHED SUCCESSFULLY: ' + ingestData.message;
        simAlert.className = 'alert-box success';
        logConsole(`Telemetry ingestion accepted: ${ingestData.message}`, 'success');
        
        document.getElementById('metric-value').value = '';
    } catch (err) {
        simAlert.textContent = 'PIPELINE ERROR: ' + err.message;
        simAlert.className = 'alert-box error';
        logConsole('Diagnostics ingestion error: ' + err.message, 'error');
    }
});

// Check active session on startup
async function checkActiveSession() {
    try {
        const res = await fetch('/api/auth/me');
        if (res.ok) {
            currentUser = await res.json();
            updateAuthUI();
            logConsole(`Active session detected for ${currentUser.fullName} (${currentUser.role}).`, 'success');
        } else if (res.status === 401) {
            // Attempt auto refresh on startup
            const refreshRes = await fetch('/api/auth/refresh-token', { method: 'POST' });
            if (refreshRes.ok) {
                const refreshData = await refreshRes.json();
                currentUser = refreshData.user;
                updateAuthUI();
                logConsole(`Silent refresh successful. Operator: ${currentUser.fullName}.`, 'success');
            } else {
                updateAuthUI();
            }
        } else {
            updateAuthUI();
        }
    } catch (err) {
        updateAuthUI();
    }
}

// Startup sequence
checkActiveSession().then(() => {
    fetchTelemetryInitial();
    fetchStockInitial().then(() => {
        initSseTelemetry();
        initSignalrTicker();
    });
});
