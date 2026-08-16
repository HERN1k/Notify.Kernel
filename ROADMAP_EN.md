# 🚀 Notify.Kernel — Roadmap & TODO

> Migration and development plan for **Notify.Kernel** — a high-performance notification CLI core built on **.NET 11 (Native AOT)**, replacing the legacy PHP module.

![.NET](https://img.shields.io/badge/.NET-11%20(Native%20AOT)-512BD4?logo=dotnet&logoColor=white)
![Status](https://img.shields.io/badge/status-in%20progress-yellow)
![Progress](https://img.shields.io/badge/progress-22%2F25%20done-brightgreen)

**Progress:** ✅ 22/25 tasks done (88%) · ❌ 2 dropped/deferred · ⏳ 1 in progress

---

## 📑 Contents

- [Project Architecture](#-project-architecture)
- [1. Design & Data Exchange Protocol](#1-design--data-exchange-protocol)
- [2. Project Setup & Configuration (.NET Native AOT)](#2-project-setup--configuration-net-native-aot)
- [3. Core Implementation](#3-core-implementation)
- [4. Performance & Parallelism](#4-performance--parallelism)
- [5. Native AOT Compatibility](#5-native-aot-compatibility)
- [6. Integration with the PHP Monolith](#6-integration-with-the-php-monolith)
- [7. Logging, Monitoring & Console Output](#7-logging-monitoring--console-output)
- [8. Testing, Build & Deployment](#8-testing-build--deployment)
- [Exit Code Matrix](#-exit-code-matrix)

---

## 🏗 Project Architecture

```text
Notify.Kernel/
├── Core/
│   ├── Abstractions/
│   │   ├── INotificationProvider.cs     # Abstraction for providers (SMS, Viber, Email)
│   │   └── IWorkflowEngine.cs           # Workflow execution engine interface
│   └── Models/
│       ├── CustomerDto.cs               # DTO matching the MySQL entity
│       ├── WorkflowConfig.cs            # Workflow scenario configuration model
│       └── NotificationPayload.cs       # Notification payload model
├── Infrastructure/
│   ├── Data/
│   │   └── CustomerRepository.cs        # Data access via Dapper / MySqlConnector
│   ├── Providers/
│   │   ├── SmsClubProvider.cs           # SMS provider (SmsClub API)
│   │   ├── ViberSmsClubProvider.cs      # Viber provider (SmsClub API)
│   │   └── EsputnikProvider.cs          # Email provider (eSputnik API)
│   └── Serialization/
│       └── SourceGenerationContext.cs   # System.Text.Json source generator for Native AOT
├── Services/
│   └── WorkflowEngine.cs                # Scenario/step execution orchestrator
├── Program.cs                           # Entry point, CLI parser, DI bootstrap, exit codes
└── Notify.csproj
```

---

## 1. Design & Data Exchange Protocol

- [x] **Define the PHP ↔ .NET data exchange protocol**
    - [x] Data passed via CLI arguments (for quick, targeted triggers)
    - [ ] ~~JSON passed via `stdin` or temp files (for complex structured data)~~ — ❌ **dropped**
- [ ] ~~**Design YAML workflow schemas**~~ — ❌ **dropped / deferred**
    - Analyze and document the file structure under `config/workflows`
    - Create matching C# DTO models (`WorkflowConfig`, `WorkflowStep`, `WorkflowAction`)
- [x] **Database audit & mapping**
    - Catalog every SQL query the PHP module executed (`YamlWorkflowAction`)
    - Optimize queries for `Dapper` and `MySqlConnector`, mapping columns directly (`snake_case` → `PascalCase`)

## 2. Project Setup & Configuration (.NET Native AOT)

- [x] **Initialize the console project** — `dotnet new console -n Notify.Kernel`
- [x] **Enable Native AOT compilation** — added `<PublishAot>true</PublishAot>` to the `.csproj`
- [x] **Select and verify AOT-compatible dependencies**

| Package | Purpose |
| --- | --- |
| `Microsoft.Extensions.Configuration` | Reading `appsettings.json` and environment variables |
| `VYaml` | YAML parsing, verified for Static Code Generation compatibility |
| `MySqlConnector` | Async, AOT-compatible MySQL driver |
| `Dapper` | Micro-ORM (`DefaultTypeMap.MatchNamesWithUnderscores = true`, no dynamic reflection) |
| `System.Net.Http.Json` | Source-generator–based HTTP clients |
| `Microsoft.Extensions.Logging` | Console and file logging |

## 3. Core Implementation

- [x] **Workflow parsing engine** — reads `.yaml` configs and builds the scenario execution graph
- [x] **Notification providers** (implementing `INotificationProvider` via `HttpClient`)
    - [x] `SmsSmsclubProvider`
    - [x] `ViberSmsclubProvider`
    - [x] `EmailEsputnikProvider`
- [x] **Lifecycle porting** — moved `OnEnter`, `Start`, `OnLeave` logic from PHP into the corresponding workflow event-handling services

## 4. Performance & Parallelism

- [x] **Parallel processing**
    - `Parallel.ForEachAsync` for bulk sends / running multiple scenarios in parallel
    - `MaxDegreeOfParallelism` capped to avoid hitting provider API rate limits
- [x] **Async I/O** — 100% `async/await` coverage for all network operations (MySQL, REST APIs)

## 5. Native AOT Compatibility

- [x] **JSON source generation** — `JsonSerializerContext` for all DTOs, no runtime reflection
- [x] **Dependency injection** — `Microsoft.Extensions.DependencyInjection` with no on-the-fly code generation
- [x] **AOT anti-pattern cleanup** — static analysis confirming no `dynamic`, `Type.GetType()`, `MakeGenericType()`, or unresolved trimming warnings

## 6. Integration with the PHP Monolith

- [x] **PHP wrapper** — rewrote entry points (e.g. `notify/index.php`) to invoke the .NET binary via `exec()` / `proc_open()`
- [x] **Standardized exit codes** — a clear system-wide exit code matrix for correct error handling on the PHP side (see [table below](#-exit-code-matrix))

## 7. Logging, Monitoring & Console Output

- [x] **Unified logging format** — writes to `./logs` in a format matching the legacy PHP app
- [x] **Console output formatting** — cleaned up `stdout` noise, formatted for correct display in the admin panel when invoked through the web UI

## 8. Testing, Build & Deployment

- [ ] **Unit & Integration Tests** — ⏳ *in progress*
    - Cover the YAML parser and scenario orchestrator with tests
    - Write integration tests with mock servers for the SMS/Email providers
- [x] **Release build & verification**
    - Build for the target OS: `dotnet publish -c Release -r linux-x64`
    - Binary size < 30 MB, cold-start time < 15 ms
    - Built a script (`scripts/build-images.js`) that produces artifacts for both platforms (`linux-x64` and `win-x64`) in a single run, placing them in `publish/linux` and `publish/windows`
    - 💡 Thanks to Native AOT, each binary is a self-contained native executable — running it on the target machine requires no .NET installed at all (no SDK, no runtime) — just copy the artifact from `publish/` and run it

---

## 🚦 Exit Code Matrix

| Code | State | Action on the PHP side |
| :---: | --- | --- |
| `0` | ✅ **Success** — workflow completed successfully | Process the result and end the script successfully |
| `1` | ⚠️ **Invalid Arguments** — missing or malformed CLI arguments | Log the call/argument error |
| `2` | ⚠️ **Configuration Error** — config read or syntax error | Notify developers, halt execution |
| `3` | 🔌 **Database Error** — DB connection or SQL execution failure | Queue the task for retry |
| `4` | 📡 **Provider Error** — external API failure (SmsClub, eSputnik, etc.) | Log the send failure, route to fallback/DLQ |
| `5` | ⚠️ **Invalid Operation** — action invalid for the current system state | Log the business-logic error |
| `6` | ⏹ **Operation Canceled** — operation canceled (timeout or task cancellation) | Log the interruption, do not retry |
| `7` | 📂 **File Not Found** — required file missing (template, data file) | Verify the file exists at the expected path |
| `8` | ⚠️ **Invalid Config** — config read but values failed validation | Log the schema/parameter validation error |
| `99` | 🔥 **Unhandled Exception** — unexpected critical failure | Critical log to the system queue, notify the dev team |

---

<sub>Last updated: 2026-08-16</sub>