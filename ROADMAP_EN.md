
# Notify.Kernel — Roadmap & TODO

This document outlines a detailed migration and development plan for the high-performance notification CLI kernel **Notify.Kernel**, built on **.NET 11 (Native AOT)** to replace the PHP module.

---

## 🏗 Project Architectural Structure

```text
Notify.Kernel/
├── Core/
│   ├── Abstractions/
│   │   ├── INotificationProvider.cs     # Provider abstractions (SMS, Viber, Email)
│   │   └── IWorkflowEngine.cs           # Execution engine interface
│   └── Models/
│       ├── CustomerDto.cs               # MySQL-compatible entity DTO
│       ├── WorkflowConfig.cs            # Scenario configuration model
│       └── NotificationPayload.cs       # Notification payload model
├── Infrastructure/
│   ├── Data/
│   │   └── CustomerRepository.cs        # Data access via Dapper / MySqlConnector
│   ├── Providers/
│   │   ├── SmsClubProvider.cs           # SMS provider (SmsClub API)
│   │   ├── ViberSmsClubProvider.cs      # Viber provider (SmsClub API)
│   │   └── EsputnikProvider.cs          # Email provider (eSputnik API)
│   └── Serialization/
│       └── SourceGenerationContext.cs   # System.Text.Json Source Generator for Native AOT
├── Services/
│   └── WorkflowEngine.cs                # Orchestrator for executing scenarios and steps
├── Program.cs                           # Entry point, CLI parser, Bootstrap DI, Exit Codes
└── Notify.csproj

```

---

## 📋 Execution Plan (Checklist)

### 1. Design & Data Exchange Protocol

* [x] **Define data transfer protocol from PHP to .NET**:
* [x] *Transfer via CLI arguments* (for targeted and rapid triggers).
* [ ] *Transfer JSON via `stdin` or temporary files* (for complex structured data).
* [ ] **Develop YAML workflow schemas**:
* Analyze and lock in the file structure within `config/workflows`.
* Create corresponding C# DTO models (`WorkflowConfig`, `WorkflowStep`, `WorkflowAction`).
* [ ] **Database audit and mapping**:
* List all SQL queries executed by the PHP module (`YamlWorkflowAction`).
* Optimize queries for `Dapper` and `MySqlConnector`, taking into account direct column mapping (`snake_case` -> `PascalCase`).

---

### 2. Project Setup & Configuration (.NET Native AOT)

* [x] **Initialize console project**: `dotnet new console -n Notify.Kernel`.
* [x] **Enable Native AOT compilation**: Added `<PublishAot>true</PublishAot>` to `.csproj`.
* [ ] **Select and verify AOT-compatible dependencies**:
* `Microsoft.Extensions.Configuration` — reading `appsettings.json` and environment variables.
* `YamlDotNet` or `SharpYaml` — verify compatibility with Static Code Generation during YAML parsing.
* `MySqlConnector` — asynchronous AOT-compatible MySQL driver.
* `Dapper` — micro-ORM (configure without dynamic reflection using `DefaultTypeMap.MatchNamesWithUnderscores = true`).
* `System.Net.Http.Json` — HTTP clients based on source generators.
* `Microsoft.Extensions.Logging` — console and file logging.

---

### 3. Core Functionality Implementation (System Kernel)

* [ ] **Workflow Parsing Engine**:
* Implement reading of `.yaml` configurations and building the scenario execution graph.
* [ ] **Notification Providers Development**:
* [ ] `SmsSmsclubProvider` (`INotificationProvider` implementation via `HttpClient`).
* [ ] `ViberSmsclubProvider` (`INotificationProvider` implementation via `HttpClient`).
* [ ] `EmailEsputnikProvider` (`INotificationProvider` implementation via `HttpClient`).
* [ ] **Lifecycle Porting**:
* Port `OnEnter`, `Start`, and `OnLeave` logic from PHP to corresponding workflow event handler services.

---

### 4. Performance Optimization & Concurrency

* [ ] **Parallel Processing (TPL)**:
* Implement `Parallel.ForEachAsync` for mass mailings or concurrent scenario execution.
* Configure `MaxDegreeOfParallelism` limits to prevent API rate limiting from providers.
* [ ] **Asynchronous I/O**:
* Ensure 100% `async/await` coverage for all network operations (MySQL, REST API).

---

### 5. Native AOT Adaptation & Compatibility

* [ ] **JSON Source Generation**:
* Create `JsonSerializerContext` for all DTOs to eliminate Runtime Reflection.
* [ ] **Dependency Injection**:
* Configure `Microsoft.Extensions.DependencyInjection` without on-the-fly code generation.
* [ ] **AOT Anti-pattern Elimination**:
* Perform static analysis to ensure absence of `dynamic`, `Type.GetType()`, `MakeGenericType()`, and unhandled trimming.

---

### 6. Integration with PHP Monolith

* [ ] **PHP Wrapper Creation**:
* Rewrite entry points (e.g., `notify/index.php`) to execute the `.NET` binary via `exec()` or `proc_open()`.
* [ ] **Standardize Response Codes (Exit Codes)**:
* Implement a clear exit code matrix for proper error handling on the PHP side.

---

### 7. Logging, Monitoring & Console Output

* [ ] **Unified Logging Format**:
* Configure logging to `./logs` directory in a format compatible with the legacy PHP application.
* [ ] **Console Output Formatting**:
* Clean `stdout` of technical junk; format output for correct rendering in the admin panel (when invoked via web interface).

---

### 8. Testing, Compilation & Deployment

* [ ] **Unit & Integration Tests**:
* Cover YAML parser and scenario orchestrator with tests.
* Write integration tests with mock servers for SMS/Email providers.
* [ ] **Release Build & Verification**:
* Compile for target OS: `dotnet publish -c Release -r linux-x64`.
* Verify binary size (< 30 MB) and cold start time (< 15 ms).

---

## 🚦 Exit Codes Matrix

| Code (`Exit Code`) | Status Description | PHP-Side Action |
| --- | --- | --- |
| `0` | **Success**: Workflow executed successfully | Complete script successfully |
| `1` | **Invalid Arguments**: Incorrect CLI arguments | Log invocation error |
| `2` | **Configuration Error**: YAML/JSON parsing error | Notify developers |
| `3` | **Database Error**: Connection or SQL execution error | Retry execution |
| `4` | **Provider Error**: External API error (SmsClub/eSputnik) | Log dispatch failure |
| `99` | **Unhandled Exception**: Unexpected critical error | Log critical error to system queue |

```

```