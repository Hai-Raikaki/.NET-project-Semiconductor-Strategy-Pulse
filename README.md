# Semiconductor Strategy Pulse: My .NET Self-Learning Journey

Welcome to my portfolio project! I built **Semiconductor Strategy Pulse** to push my skills beyond standard, basic CRUD tutorials. Instead of building another "To-Do list" app, I wanted to tackle complex, production-grade architectural challenges: thread-safe background processing, real-time client notifications, secure state-of-the-art token rotation, and beautiful responsive layouts.

This application acts as a real-time monitoring center for semiconductor supply chains. It simulates live telemetry inputs (like chip yield rates and order backlogs) and stock price tickers, pushing them to a custom cyberpunk-themed dashboard without page refreshes.

---

## 🎯 What I Learned & Implemented (My Learning Goals)

By designing and coding this system from scratch, I successfully mastered the following advanced .NET and web architecture concepts:

* **High-Throughput Concurrency**: To prevent database locks during peak telemetry events, I bypassed direct database writes and implemented an in-memory queue using `System.Threading.Channels`.
* **Enterprise Security Standards**: I avoided saving JWT tokens in browser `localStorage` (which is vulnerable to XSS). Instead, I configured the backend to store JWT access tokens and database-linked refresh tokens in **HttpOnly, SameSite Cookies**, complemented by a secure **Refresh Token Rotation (RTR)** cycle.
* **Asynchronous Scoped Life Cycles**: I learned how to manage dependency injection lifetimes correctly by dynamically spawning scoped database contexts (`CreateScope()`) inside singleton background thread workers.
* **Dual Real-Time Technologies**: I implemented **Server-Sent Events (SSE)** for unidirectional telemetry streams and **SignalR (WebSockets)** for bidirectional market tickers, giving me hands-on experience with both push architectures.
* **Responsive Visual Interfaces**: I built a responsive, glassmorphic cyberpunk interface using Vanilla HTML, CSS, and JS (with Chart.js and HTML5 Canvas animations) that resizes dynamically and refolds smoothly for tablets and mobile devices.

---

## 💻 My System Architecture & Data Flow

I structured the application with a decoupled pipeline to ensure low-latency inputs and highly-reactive updates:

1. **The Telemetry Ingestion Flow**: 
   An authorized operator or IoT device submits a telemetry point (e.g., Yield Rate = 95.2%) to `/api/pulse/ingest`. The API controller validates the secure cookie token, writes the data point immediately to a thread-safe, bounded memory channel queue, and returns a `202 Accepted` response. This decouples incoming web traffic from heavy database persistence.
2. **The Batch Processing Worker**: 
   A hosted background service (`BackgroundProcessor`) monitors the channel queue. It retrieves incoming items and pools them. When the pool hits 1,000 items or 1 second passes, the worker creates a scoped database context, batch-saves the raw points to PostgreSQL, and triggers statistical recalculations.
3. **The Analytics & Recalculation Engine**: 
   The calculation service reads the last 20 raw points from the database, computes statistical values (standard deviation, mean, and change rates), stores the consolidated state in the metrics database tables, and invalidates the cache layer.
4. **The Real-Time Broadcast Stream**: 
   Upon recalculation, the server pushes updates down a persistent HTTP stream using Server-Sent Events (SSE). Concurrently, a background stock simulator updates prices for NVDA, TSM, and SOX every 2 seconds, broadcasting them via a SignalR WebSocket Hub.
5. **The Frontend Data Binder**: 
   The dashboard client script (`app.js`) listens to both the SSE stream and the SignalR WebSocket. It updates the DOM elements, flashes neon KPI highlights (green/red), adds points to Chart.js curves, and triggers canvas heartbeat wave spikes dynamically.

---

## 🔄 Detailed Walkthrough: How Telemetry Ingestion Works

Here is a step-by-step trace of exactly what happens when I post a telemetry point (e.g., Yield Rate = 94.5%) in the simulator:

### 1. Client Dispatch (Frontend)
- The operator selects "Advanced Node Yield Rate" and enters `94.5` on the Ingestion Simulator panel.
- JavaScript (`app.js`) intercepts the form submission and makes an authenticated `POST` request to `/api/pulse/ingest` carrying a JSON payload:
  ```json
  {
    "metricKey": "YIELD_RATE",
    "value": 94.5,
    "region": "APAC-SOUTH-HUB",
    "source": "Diagnostics Operator Console"
  }
  ```
- The request transparently carries my secure HttpOnly JWT `access_token` cookie for authorization.

### 2. Authorization & Ingest Queue (Backend Gateway)
- The server resolves the route to `PulseController.IngestTelemetry`.
- The `[Authorize(Roles = "IoTDevice,Admin")]` attribute blocks unauthorized clients (returning `401` or `403` if roles don't match).
- On success, the controller writes the telemetry object to my in-memory singleton channel (`Channel<RawMarketData>`).
- The API instantly responds with `202 Accepted` to the client. This means the client does not wait for database operations, allowing immediate response times.

### 3. Background Persistence (Hosted Background Worker)
- My `BackgroundProcessor` (a Hosted BackgroundService running in the background) detects that items are waiting in the channel reader.
- It pulls raw metrics from the channel and buffers them.
- Once 1,000 items are read or 1 second passes, it flushes the batch:
  - Spawns a transient Dependency Injection scope.
  - Resolves my scoped `PulseDbContext`.
  - Saves the batch of raw records to PostgreSQL.

### 4. Recalculation & Cache Update
- Immediately after database writes, the processor triggers the `MetricService`.
- The service retrieves the latest 20 telemetry entries from the database.
- It computes statistical formulas (variance, standard deviation, sample size, change rates) and saves the results to the `MetricState` database table.
- The cache service (`ICacheService`) is updated, ensuring future dashboard requests load instantly from memory rather than querying raw rows.

### 5. Real-Time UI Broadcast (SSE Push Stream)
- The background processor signals my SSE stream that new metrics are ready.
- `PulseController.StreamMetrics` pushes the recalculated metric state down the open HTTP channel `/api/pulse/stream` as a text event stream.
- The browser client receives the event, and `app.js` runs UI updates:
  - The **Advanced Node Yield Rate** card flashes with a green cyberpunk glow.
  - The standard deviation, sample size, and rolling average values update.
  - The live performance charts append a new point.
  - The canvas oscilloscope wave displays a brief heart-rate surge indicating ingestion activity.


---

## 🧮 Mathematical Formulas & Metric Logic

To compute strategic telemetry updates and simulate market behavior, I implemented various statistical and mathematical models. Here are the core formulas and code snippets:

### 1. Rolling Mean (Average)
* **Mathematical Formula**:
  $$\mu = \frac{1}{N} \sum_{i=1}^{N} x_i$$
  *Where
  $`x_i`$
  represents each raw telemetry point value, and $`N`$ represents the rolling sample size (up to 500).*
* **C# Implementation ([MetricService.cs:L90](/Services/MetricService.cs#L90))**:
  ```csharp
  double mean = values.Average();
  ```

### 2. Population Variance
* **Mathematical Formula**:
  $$\sigma^2 = \frac{1}{N} \sum_{i=1}^{N} (x_i - \mu)^2$$
  *Where $`(x_i - \mu)^2`$ is the squared deviation of each telemetry point from the calculated mean.*
* **C# Implementation ([MetricService.cs:L91](/Services/MetricService.cs#L91))**:
  ```csharp
  double variance = values.Select(v => Math.Pow(v - mean, 2)).Average();
  ```

### 3. Population Standard Deviation
* **Mathematical Formula**:
  $$\sigma = \sqrt{\sigma^2}$$
  *Measuring the volatility and spread of the telemetry points around the rolling mean.*
* **C# Implementation ([MetricService.cs:L92](/Services/MetricService.cs#L92))**:
  ```csharp
  double stdDev = Math.Sqrt(variance);
  ```

### 4. Percentage Change Rate
* **Mathematical Formula**:
  $$\text{Change Rate } (\%) = \frac{\mu_{\text{new}} - \mu_{\text{old}}}{\mu_{\text{old}}} \times 100$$
  *Where $`\mu_{\text{new}}`$ is the newly recalculated mean and $`\mu_{\text{old}}`$ is the previous rolling average stored in the database.*
* **C# Implementation ([MetricService.cs:L116-L120](/Services/MetricService.cs#L116-L120))**:
  ```csharp
  double changeRate = 0.0;
  if (oldVal != 0.0)
  {
      changeRate = ((mean - oldVal) / oldVal) * 100;
  }
  ```

### 5. Coefficient of Variation (CV) & Pulse Status Rules
* **Mathematical Formula**:
  $$CV = \frac{\sigma}{|\mu|}$$
  *The ratio of the standard deviation to the mean, representing relative variability.*
* **C# Implementation & Status Rules ([MetricService.cs:L181-L209](/Services/MetricService.cs#L181-L209))**:
  ```csharp
  private string DeterminePulseStatus(string key, double val, double stdDev, double changeRate)
  {
      // Coefficient of Variation (CV) = StdDev / Mean
      double cv = val != 0 ? stdDev / Math.Abs(val) : 0;
      
      // Extreme variance indicates high market instability
      if (cv > 0.20 && key == "YIELD_RATE")
      {
          return "CRITICAL"; // Unstable manufacturing yields
      }
      if (cv > 0.30)
      {
          return "CRITICAL";
      }

      if (key.ToUpper() == "BOOK_TO_BILL")
      {
          if (val > 1.15) return "GROWING"; // Boom
          if (val < 0.90) return "DECLINING"; // Recession
          return "STABLE";
      }

      if (Math.Abs(changeRate) > 3.0)
      {
          return changeRate > 0 ? "GROWING" : "DECLINING";
      }

      return "STABLE";
  }
  ```

### 6. Stock Price Random Walk (Mock Ticker Simulation)
* **Mathematical Formula**:
  $$`\text{Change} = (\text{Rand}_{[0,1)} \times 0.04) - 0.02`$$
  $$`\text{Price}_{\text{new}} = \text{Round}\left(\text{Price}_{\text{old}} \times (1 + \text{Change}), 2\right)`$$
  *Where
  $`\text{Rand}_{[0,1)}`$
  is a pseudo-random floating-point number between 0.0 and 1.0. This generates a random walk fluctuating between $`-2\%`$ and $`+2\%`$ on every 2-second tick.*
* **C# Implementation ([MarketDataSimulator.cs:L100-L106](/Services/MarketDataSimulator.cs#L100-L106))**:
  ```csharp
  double nvdaChange = (_random.NextDouble() * 0.04) - 0.02;
  _nvda = Math.Round(_nvda * (1 + nvdaChange), 2);
  ```

### 7. Heartbeat Wave Oscilloscope Canvas (ECG Simulation)
* **Mathematical Formula**:
  $$y = \sin(x \cdot f + \text{offset}) \cdot A + \text{Spike}(x)$$
  *Where
  $`f`$
  is frequency (0.02), $A$ is base amplitude (10),
  $`\text{offset}`$
  is the phase offset incremented on each draw loop, and $`\text{Spike}(x)`$ represents the localized pulse trigger modeling a heartbeat.*
* **JavaScript Implementation ([app.js:L70-L77](/wwwroot/app.js#L70-L77))**:
  ```javascript
  let y = Math.sin((x * waveFrequency) + waveOffset) * waveAmplitude;
  const spike = (x + (waveOffset * 80)) % 240;
  if (spike > 100 && spike < 125) {
      const ratio = (spike - 100) / 25;
      y += Math.sin(ratio * Math.PI * 4) * (waveAmplitude * 2.2 + (flashWaveIntensity * 25));
  }
  ```

---

## 🗺️ How I Built This Project (My Step-by-Step Log)

I followed a strict chronological sequence to build this project from an empty directory:

### 1️⃣ FIRST: I Coded the Database Foundation
* **Step 1: Project Setup & Package Configurations**
  - Scaffolded the project using the .NET Web API template.
  - Installed EF Core packages for PostgreSQL (`Npgsql.EntityFrameworkCore.PostgreSQL`) and ASP.NET Core Identity.
  - Configured PostgreSQL connection configurations, JWT parameters, and logging paths in [appsettings.json](/appsettings.json).
* **Step 2: Database Schema & Entity Definitions**
  - Configured `AppUser` extending `IdentityUser<Guid>` to override default identity tables with GUID identifiers.
  - Coded database mapping classes for [RawMarketData.cs](/Models/RawMarketData.cs) (raw events), [MetricState.cs](/Models/MetricState.cs) (recalculated stats), and [RefreshToken.cs](/Models/RefreshToken.cs) (session tracking).
* **Step 3: Database Context & Initial Data Seeding**
  - Inherited `PulseDbContext` from `IdentityDbContext` and mapped my custom entities to tables using Fluent API.
  - Programmed `SeedData.cs` to run automatic migrations and seed default test credentials (`admin@pulse.com` as Admin and `device@pulse.com` as IoTDevice) so the database is ready for testing instantly.

### 2️⃣ THEN: I Coded the Security & Concurrency Core
* **Step 4: Secure Cookie-based JWT Middleware**
  - Programmed [TokenService.cs](/Services/TokenService.cs) to handle token configurations.
  - Configured JWT Bearer options in [Program.cs]( /Project1/Program.cs), adding an event hook to transparently intercept and extract access tokens from incoming browser request cookies.
* **Step 5: Authorization Controller & Token Rotation**
  - Created [AuthController.cs](/Controllers/AuthController.cs) to process requests.
  - Wrote login methods that issue JWT access cookies (expires in 15 mins) and refresh cookies (expires in 7 days).
  - Wrote `/refresh-token` logic that implements cryptographically secure refresh token rotation in the database to prevent replay attacks.
* **Step 6: Thread-Safe Queue & Background Service**
  - Registered the singleton `Channel<RawMarketData>` in DI.
  - Wrote `IngestionService` to post items to the channel.
  - Coded [BackgroundProcessor.cs](/Services/BackgroundProcessor.cs) to pull items from the channel reader and batch-save them under a custom DI lifetime scope.
* **Step 7: Recalculation Service**
  - Coded statistical math formulas inside `MetricService.cs` to update standard deviation, sample counts, and percentage differences on every telemetry batch.

### 3️⃣ FINALLY: I Coded the Real-Time Streams & Frontend UI
* **Step 8: SignalR Websockets & SSE persistent streams**
  - Set up a SignalR Hub (`MarketHub`) and a background market price generator service to broadcast simulated stocks every 2 seconds.
  - Implemented the persistent, keep-alive SSE stream endpoint `/api/pulse/stream` in `PulseController`.
* **Step 9: Responsive UI & Glassmorphism Design**
  - Hand-crafted `index.html` dividing the layout into left column (Visual metrics and charts) and right column (Controls and logger consoles).
  - Coded `style.css` implementing HSL neon colors, custom canvas sizes, and CSS media queries to collapse columns and grids on smaller viewports.
* **Step 10: Client JS Event Handlers & Session Interceptors**
  - Coded `app.js` to parse incoming SSE/WebSocket JSON and update KPI cards.
  - Designed the HTML5 canvas oscilloscope pulse wave animations.
  - Coded the secure `authenticatedFetch` wrapper inside JS to intercept `401 Unauthorized` errors, request silent token rotations, and transparently retry operations before redirecting users to `login.html`.

---

## 📂 Project Directory Structure

```text
E:\.NET PROJECT\PROJECT1
│   Program.cs                      <-- Entry point, configures DI, cookie JWT authentication, and API pipelines.
│   appsettings.json                <-- Database Connection Strings, JWT secret keys, and logging settings.
│   appsettings.Development.json    <-- Local development overrides.
│   Dockerfile                      <-- Configuration for packaging the app as a container.
│   docker-compose.yml              <-- Orchestrates multi-container local services (PostgreSQL & Redis).
│   SemiconductorStrategyPulse.csproj <-- MSBuild project file detailing framework versions and packages.
│   SemiconductorStrategyPulse.http <-- Local scratchpad file for testing API endpoints.
│
├───Caching
│       ICacheService.cs            <-- Interface defining Cache get, set, and invalidation behaviors.
│       RedisCacheService.cs        <-- Implementation of caching utilizing a local Redis instance.
│
├───Controllers
│       AuthController.cs           <-- secure controllers managing register, login, logout, and token refresh.
│       PulseController.cs          <-- secure gateways to ingest telemetry and stream SSE metrics.
│
├───Data
│       PulseDbContext.cs           <-- EF Core context configuration mapping entities to database tables.
│       SeedData.cs                 <-- Automatically provisions default accounts (Admin / IoTDevice).
│
├───DTOs
│       AuthDtos.cs                 <-- Schemas for authentication request payloads.
│       IngestionRequest.cs         <-- Validation schema for telemetry inputs.
│       MetricPulseDto.cs           <-- Data structure representation for output stream events.
│
├───Hubs
│       MarketHub.cs                <-- SignalR WebSocket hub that streams stock ticks to the frontend.
│
├───Models
│       AppUser.cs                  <-- Extends default Identity account to support GUID keys and profile logs.
│       MetricState.cs              <-- Holds recalculated analytics data (StDev, change rates).
│       RawMarketData.cs            <-- Raw telemetry database mapping.
│       RefreshToken.cs             <-- Refresh token databases tracking.
│
├───Services
│       BackgroundProcessor.cs      <-- Background HostedService that batches and saves telemetry.
│       TokenService.cs             <-- Cryptographic helper that signs and generates secure tokens.
│       MarketDataSimulator.cs      <-- background worker that simulates stock market ticks.
│       MetricService.cs            <-- Service calculating rolling stats and managing cache.
│       IngestionService.cs         <-- Writes raw data to the queue channel.
│       IIngestionService.cs        <-- Ingestion service interface definition.
│       IMetricService.cs           <-- Recalculation service interface definition.
│       ITokenService.cs            <-- Token service interface definition.
│
└───wwwroot                         <-- Frontend static file assets.
        index.html                  <-- Main glassmorphism telemetry dashboard.
        style.css                   <-- Cyberpunk neon styling variables and responsive layout definitions.
        app.js                      <-- client stream router, Chart.js binder, and auth refresh wrapper.
        login.html                  <-- Standalone sign-in portal.
        register.html               <-- Standalone registration portal.
```

---

## 🔍 In-Depth File-by-File Technical Explanations

Here is a detailed breakdown of the files I wrote, explaining their specific responsibilities and how they fit into the system architecture:

### ⚙️ Root level Configurations
* **[Program.cs](/Program.cs)**: The heart and entry point of my .NET Core application. I configured the dependency injection container, database contexts, Redis, and security policies here. It registers my custom JWT cookie extraction filter and sets up middleware routing pipelines.
* **[appsettings.json](/appsettings.json)**: The centralized configuration file. I stored my PostgreSQL database connection strings, JWT security secrets (issuer, audience, and key), and Serilog logging formats in this file.
* **[Dockerfile](/Dockerfile)**: Defines the multi-stage build configuration to compile and bundle the .NET application into a lightweight runtime Docker image.
* **[docker-compose.yml](/docker-compose.yml)**: Orchestrates my local development microservices. It spins up a PostgreSQL database and a Redis server container instantly, ensuring my dev environment is self-contained.

### ⚡ Caching Services
* **[Caching/ICacheService.cs](/Caching/ICacheService.cs)**: The interface defining caching functions (Get, Set, and Invalidation) to decouple my services from a specific caching library.
* **[Caching/RedisCacheService.cs](/Caching/RedisCacheService.cs)**: Integrates StackExchange.Redis to cache aggregated strategic metrics. If Redis is unavailable, it gracefully handles exceptions and falls back to in-memory caching.

### 🎮 Web API Controllers
* **[Controllers/AuthController.cs](/Controllers/AuthController.cs)**: Exposes endpoints for managing user session state. It handles login verification, user registration, logout cookie revocation, and refresh token rotation validations.
* **[Controllers/PulseController.cs](/Controllers/PulseController.cs)**: Implements the telemetry ingestion POST endpoint, which routes raw data straight to the in-memory queue, and the Server-Sent Events (SSE) stream, which maintains open client channels to push updated statistics in real time.

### 🗄️ Database & Seeding Logic
* **[Data/PulseDbContext.cs](/Data/PulseDbContext.cs)**: The Entity Framework Core Database Context. It maps C# object entities directly to SQL tables in PostgreSQL and overrides standard ASP.NET Identity tables to use GUID primary keys.
* **[Data/SeedData.cs](/Data/SeedData.cs)**: Automates database provisioning on startup. It runs migrations and seeds security roles and default operator accounts so the project is usable immediately.

### 📦 Data Transfer Objects (DTOs)
* **[DTOs/AuthDtos.cs](/DTOs/AuthDtos.cs)**: Holds the structural request/response records for logins and registrations.
* **[DTOs/IngestionRequest.cs](/DTOs/IngestionRequest.cs)**: Validates input payloads for the telemetry ingestion endpoint, checking key properties like range and structure.
* **[DTOs/MetricPulseDto.cs](/DTOs/MetricPulseDto.cs)**: Formats metrics updates pushed via the Server-Sent Events stream.

### 🔗 WebSockets & SignalR Hubs
* **[Hubs/MarketHub.cs]( /Project1/Hubs/MarketHub.cs)**: A SignalR Hub that sets up bidirectional WebSocket endpoints, facilitating instant market price updates streaming to front-end clients.

### 🧩 Database Domain Models
* **[Models/AppUser.cs](/Models/AppUser.cs)**: Extends standard C# ASP.NET Core Identity users, introducing properties like custom full names, creation timestamps, and active logs.
* **[Models/MetricState.cs](/Models/MetricState.cs)**: The database schema representation for aggregated metrics (rolling averages, standard deviation, change percentages).
* **[Models/RawMarketData.cs](/Models/RawMarketData.cs)**: Defines the schema for logging raw telemetry inputs prior to processing.
* **[Models/RefreshToken.cs](/Models/RefreshToken.cs)**: Establishes the database schema for storing refresh tokens linked to specific users to validate sessions.

### ⚙️ Core Application Services
* **[Services/BackgroundProcessor.cs](/Services/BackgroundProcessor.cs)**: An asynchronous HostedService. It reads incoming telemetry from the channel queue reader, aggregates them into batches, persists them into PostgreSQL, and triggers statistical recalculations.
* **[Services/TokenService.cs](/Services/TokenService.cs)**: Contains security utility logic to create JWT access tokens and cryptographically random secure base64 refresh token strings.
* **[Services/MarketDataSimulator.cs](/Services/MarketDataSimulator.cs)**: A background worker service that runs price simulations for stocks (NVDA, TSM, and SOX index) and broadcasts them directly to clients via SignalR.
* **[Services/MetricService.cs](/Services/MetricService.cs)**: Implements mathematical formulas to compute standard deviation and change rates on the last 20 telemetry entries of a metric.
* **[Services/IngestionService.cs](/Services/IngestionService.cs)**: Provides a gateway service to write telemetry data to the in-memory bounded queue.

### 🌐 Frontend Client Interface
* **[wwwroot/index.html](/wwwroot/index.html)**: The HTML layout of the main dashboard, structured into visual analysis columns (left) and ingestion simulator consoles (right).
* **[wwwroot/style.css](/wwwroot/style.css)**: The custom CSS stylesheet that creates a translucent glassmorphic look with neon accent glows, and defines media query rules for mobile styling.
* **[wwwroot/app.js](/wwwroot/app.js)**: The core client script. It listens to WebSocket and SSE streams, handles Chart.js and wave drawings, and wraps all fetch operations in automatic token refresh interceptors.
* **[wwwroot/login.html](/wwwroot/login.html) & [wwwroot/register.html](/wwwroot/register.html)**: Monolithic glassmorphic forms for signing in and creating accounts.

---

## 🚀 How to Run and Verify My Project

### 1. Prerequisites
- **PostgreSQL** running locally on port `5432` with username `postgres` and password `0123`.
- A database named `semiconductor_pulse` created.
- **.NET SDK 10.0** or newer installed.

### 2. Startup
1. Open your terminal in this project directory:
   ```powershell
   dotnet run
   ```
2. Once the backend server starts, open your browser and navigate to:
   - **http://localhost:5107/**

### 3. Verification Sequence
- **Login Verification**: Click **Sign In** and login with email `admin@pulse.com` and password `Admin123`. Verify the Ingestion status bar turns green (**AUTHORIZED**).
- **Ingestion & Streaming Verification**: Select *Yield Rate*, enter value `94.5`, and click **POST TELEMETRY POINT**. Observe:
  1. The "Advanced Node Yield Rate" KPI card immediately flashes green.
  2. The displayed value updates instantly to `94.50%`.
  3. The black diagnostics terminal console logs the success payload.
  4. The green/purple heartbeat wave below the line charts experiences a sudden spike.
