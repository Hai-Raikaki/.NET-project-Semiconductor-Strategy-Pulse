# This project is about Strategic Semiconductor pulse. Then, why does its interface look like a stock web?

❗ The web page has elements resembling a stock market platform because **financial market performance and semiconductor operations are deeply linked in the real world**. 

Here is the design and business rationale behind this layout:

---

### 1. The Core Split: Operations vs. Market Value
The interface is split into two columns to showcase the relationship between **manufacturing telemetry** (operational health) and **market capitalization** (financial value):
* **Strategic Telemetry Pulse:** This is the actual **Semiconductor Operations** side. It displays operational KPIs like **Yield Rate** (factory output quality), **Lead Time** (supply chain speed), **Book-to-Bill Ratio** (supply/demand), and **CapEx** (capital spending on fabrication machines).
* **Market Ticker Grid & Simulator:** This is the **Financial Market** side. It displays the stock prices of key market leaders: **NVIDIA (NVDA)** (representing design/demand), **TSMC (TSM)** (representing manufacturing/supply), and **SOX** (the Philadelphia Semiconductor Index, representing the health of the entire sector).

---

### 2. High-Density, Real-Time Visualizations
In modern enterprise dashboards, operational telemetry is often represented using the visual language of trading desks (real-time tickers, glowing neon cards, sparkline charts, and streaming log consoles). This design choice was made because:
* **High-Throughput Streaming:** The system streams stock price ticks every 2 seconds via **SignalR** ([MarketDataSimulator.cs](/Services/MarketDataSimulator.cs)) and recalculates metrics via **Server-Sent Events (SSE)**. 
* **The "Pulse" Analogy:** An oscilloscope-style heartbeat canvas and color-coded neon flashing indicators represent the operational "health" of the supply chain. If wafer yields fall, or lead times spike, the "pulse" of the industry destabilizes.

---

### 3. Business Context (Why Executives Care)
For semiconductor industry leaders, engineering and manufacturing data cannot be viewed in isolation. A drop in TSMC's **wafer yield rate** (Left Column) immediately impacts production capacity, which directly influences **TSM** and **NVDA** stock prices (Right Column) hours or days later. 

By displaying the **operational pulse** alongside **market stock tickers**, the dashboard simulates a unified command center showing how physical manufacturing performance translates to public market value in real time.



# The stock tickers dashboard do not show the same unit, and this is actually the standard and correct way these entities are tracked in the global financial markets.
Here is the financial explanation of why they are displayed this way and why this combination is highly appropriate for a semiconductor dashboard:

---

### 1. NVIDIA (NVDA) — Stock Price in USD (NASDAQ)
* **What it is:** A direct share price of a U.S. company listed on the tech-heavy **NASDAQ** exchange.
* **Unit:** **U.S. Dollars (USD) per share**.
* **Why it's shown:** Nvidia is a U.S. company. It lists its primary stock on NASDAQ (the standard exchange for tech giants like Microsoft, Apple, and Google). Its price represents the market value of one share of U.S. equity.

### 2. TSMC (TSM) — American Depositary Receipt (ADR) in USD (NYSE)
* **What it is:** An **American Depositary Receipt (ADR)** listed on the **New York Stock Exchange (NYSE)**. 
* **Unit:** **U.S. Dollars (USD) per ADR share** (1 TSM ADR represents 5 common shares of TSMC listed on the Taiwan Stock Exchange under code `2330`).
* **Why it's shown:** TSMC is a Taiwanese company. Because foreign companies cannot list their native shares directly on U.S. exchanges, U.S. depositary banks bundle Taiwanese shares into **ADRs** so they can be traded in U.S. Dollars during U.S. market hours.
* **Why NYSE:** Many large, international conglomerates (like TSMC, Sony, or Toyota) prefer the prestige and liquidity of the NYSE for their U.S. ADR listings rather than NASDAQ. 

### 3. SOX — Capitalization-Weighted Index Points (PHLX Sector Index)
* **What it is:** The **PHLX Semiconductor Sector Index (SOX)**.
* **Unit:** **Index Points** (not currency).
* **Why it's shown:** The SOX is not a company; it is a basket (index) of the 30 largest semiconductor companies in the U.S. (including Nvidia, Intel, AMD, Broadcom, TSMC, etc.). 
* **Why Capitalization-Weighted:** Instead of adding up stock prices (which doesn't represent real company size), the index weights companies by their **Market Capitalization** (Share Price $\times$ Total Shares). A $1\%$ move in Nvidia (multi-trillion dollar company) will shift the SOX index points much more than a $1\%$ move in a smaller semiconductor firm. It serves as the **macro-level benchmark** for the health of the entire industry.

---

### How they work together on the Dashboard (The Unified Metric)
Even though the raw units are different:
* **NVDA ($450.00 USD)** = Individual Tech Leader (Design/AI demand).
* **TSM ($100.00 USD)** = Individual Manufacturing Leader (Foundry/Supply).
* **SOX (3,500.00 Points)** = Whole-industry performance benchmark.

They are unified on the dashboard by their **Percentage Change Rate (\%)** (e.g., `+2.4%`, `-1.5%`). The sparkline graphs and neon indicators show their relative momentum, allowing executives to immediately see if individual leaders (Nvidia/TSMC) are outperforming or underperforming the overall semiconductor sector (SOX) in real time.

---

### 1. Where **ASP.NET Core** is used:
ASP.NET Core acts as the foundational framework orchestrating routing, HTTP request pipelines, middleware, dependency injection, and security.

* **Entry point and Middleware pipeline**:
  In [Program.cs](/Project1/Program.cs):
  - **Host Initialization** (Line 25): `var builder = WebApplication.CreateBuilder(args);` initializes the ASP.NET Core web server.
  - **Controllers Mapping** (Line 40 & 231): `builder.Services.AddControllers();` registers the MVC controller services, and `app.MapControllers();` maps route endpoints to controllers.
  - **HTTP Request Pipeline** (Lines 214-232): Configures native ASP.NET Core middlewares such as `app.UseStaticFiles()` (serving static frontend assets from `wwwroot`), `app.UseAuthentication()`, `app.UseAuthorization()`, and `app.UseRateLimiter()`.
* **ASP.NET Core Identity & JWT Authentication**:
  - Registered in [Program.cs](/Project1/Program.cs#L109-L161) using `builder.Services.AddIdentity<AppUser, IdentityRole<Guid>>()` to manage database-backed users and roles.
  - Configures `AddJwtBearer()` to hook authentication events for reading JWTs from HttpOnly cookies instead of the request header.

---

### 2. Where the **RESTful API** is implemented:
The RESTful endpoints are built using ASP.NET Core MVC controllers returning JSON payloads under standard HTTP verbs (GET, POST). They are located in the `/Controllers` directory:

#### **[PulseController.cs](/Project1/Controllers/PulseController.cs)**:
Exposes telemetry and data state APIs:
* **Class Configuration** (Lines 16-18):
  - `[ApiController]` enables automatic model validation.
  - `[Route("api/[controller]")]` maps the base URL prefix for this controller to `/api/pulse`.
  - Inherits from `ControllerBase` (the standard base class for API controllers without View support).
* **GET `/api/pulse/metrics`** (Line 35): Retrieves the current computed telemetry statistics from the database or Redis cache.
* **POST `/api/pulse/ingest`** (Around Line 53): 
  - Restricts access via role-based authorization: `[Authorize(Roles = "IoTDevice,Admin")]`.
  - Validates the incoming payload and returns a standard RESTful HTTP status code: `202 Accepted` (`Accepted(new { ... })`).
* **GET `/api/pulse/stream`** (Around Line 120): Initiates a persistent connection that streams data points to clients using Server-Sent Events (SSE).

#### **[AuthController.cs](Project1/Controllers/AuthController.cs)**:
Exposes authentication RESTful resources under `/api/auth`:
* **POST `/api/auth/register`**: Validates registration DTOs and creates new accounts in PostgreSQL.
* **POST `/api/auth/login`**: Verifies login payloads and appends secure HTTP-Only cookies (`access_token`, `refresh_token`) to the response.
* **POST `/api/auth/logout`**: Clears the authentication cookies.
* **POST `/api/auth/refresh-token`**: Executes refresh token validation and rotation logic.
* **GET `/api/auth/me`**: Fetches the authenticated user profile based on the cookie context.

#### **[MarketController.cs](/Project1/Controllers/MarketController.cs)**:
Exposes stock history RESTful resource under `/api/market`:
* **GET `/api/market/history`**: Returns the last 20 ticks of mock market stock prices.
