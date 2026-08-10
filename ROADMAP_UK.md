# Notify.Kernel — Roadmap & TODO

Цей документ містить детальний план міграції та розробки високопродуктивного CLI-ядра сповіщень **Notify.Kernel** на базе **.NET 11 (Native AOT)** для заміни PHP-модуля.

---

## 🏗 Архітектурна структура проекту

```text
Notify.Kernel/
├── Core/
│   ├── Abstractions/
│   │   ├── INotificationProvider.cs     # Абстракція для провайдерів (SMS, Viber, Email)
│   │   └── IWorkflowEngine.cs           # Інтерфейс виконуючого движка
│   └── Models/
│       ├── CustomerDto.cs               # DTO сутності сумісної з MySQL
│       ├── WorkflowConfig.cs            # Модель конфігурації сценарію
│       └── NotificationPayload.cs       # Модель корисного навантаження сповіщення
├── Infrastructure/
│   ├── Data/
│   │   └── CustomerRepository.cs        # Доступ до даних через Dapper / MySqlConnector
│   ├── Providers/
│   │   ├── SmsClubProvider.cs           # Провайдер SMS (SmsClub API)
│   │   ├── ViberSmsClubProvider.cs      # Провайдер Viber (SmsClub API)
│   │   └── EsputnikProvider.cs          # Провайдер Email (eSputnik API)
│   └── Serialization/
│       └── SourceGenerationContext.cs   # System.Text.Json Source Generator для Native AOT
├── Services/
│   └── WorkflowEngine.cs                # Оркестратор виконання сценаріїв та кроків
├── Program.cs                           # Точка входу, СLI-парсер, Bootstrap DI, Exit Codes
└── Notify.csproj

```

---

## 📋 План виконання робіт (Checklist)

### 1. Проектування та протокол обміну даними

* [x] **Визначити протокол передачі даних від PHP до .NET**:
* [x] *Передача через CLI-аргументи* (для точкових і швидких тригерів).
* [ ] *Передача JSON через `stdin` або тимчасові файли* (для складних структурованих даних).

* [ ] **Розробити схеми YAML-воркфлоу**:
* Проаналізувати та зафіксувати структуру файлів у `config/workflows`.
* Створити відповідні C# DTO-моделі (`WorkflowConfig`, `WorkflowStep`, `WorkflowAction`).

* [ ] **Аудит та мапінг бази даних**:
* Виписати всі SQL-запити, які виконував PHP-модуль (`YamlWorkflowAction`).
* Оптимізувати запити під `Dapper` та `MySqlConnector` з урахуванням прямого мапінгу колонок (`snake_case` -> `PascalCase`).

---

### 2. Подготовка та конфігурація проекту (.NET Native AOT)

* [x] **Ініціалізація консольного проекту**: `dotnet new console -n Notify.Kernel`.
* [x] **Активація компіляції Native AOT**: Додано `<PublishAot>true</PublishAot>` у `.csproj`.
* [ ] **Вибір та перевірка AOT-сумісних залежностей**:
* `Microsoft.Extensions.Configuration` — зчитування `appsettings.json` та зміних середовища.
* `YamlDotNet` або `SharpYaml` — перевірити сумісність із Static Code Generation під час розбору YAML.
* `MySqlConnector` — асинхронний AOT-сумісний драйвер MySQL.
* `Dapper` — мікро-ORM (налаштувати без використання динамічної рефлексії через `DefaultTypeMap.MatchNamesWithUnderscores = true`).
* `System.Net.Http.Json` — HTTP-клієнти на базі генераторів коду.
* `Microsoft.Extensions.Logging` — консольне та файлове логування.

---

### 3. Реалізація Core-функціоналу (Ядро системи)

* [ ] **Движок парсингу воркфлоу**:
* Реалізувати читання `.yaml` конфігурацій та побудову графу виконання сценаріїв.

* [ ] **Розробка провайдерів сповіщень**:
* [ ] `SmsSmsclubProvider` (реалізація `INotificationProvider` через `HttpClient`).
* [ ] `ViberSmsclubProvider` (реалізація `INotificationProvider` через `HttpClient`).
* [ ] `EmailEsputnikProvider` (реалізація `INotificationProvider` через `HttpClient`).

* [ ] **Портування Життєвого Циклу (Lifecycle)**:
* Перенести логіку `OnEnter`, `Start`, `OnLeave` з PHP у відповідні сервіси обробки подій воркфлоу.

---

### 4. Оптимізація продуктивності та паралелізм

* [ ] **Паралельна обробка (TPL)**:
* Впровадити `Parallel.ForEachAsync` для масових розсилок або паралельного виконання декількох сценаріїв.
* Налаштувати обмеження `MaxDegreeOfParallelism` для запобігання Rate Limit від API провайдерів.

* [ ] **Асинхронний I/O**:
* Забезпечити 100% покриття `async/await` для всіх мережевих операцій (MySQL, REST API).

---

### 5. Адаптація та сумісність з Native AOT

* [ ] **JSON Source Generation**:
* Створити `JsonSerializerContext` для всіх DTO, щоб виключити Runtime Reflection.

* [ ] **Dependency Injection**:
* Налаштувати `Microsoft.Extensions.DependencyInjection` без використання генерації коду "на льоту".

* [ ] **Усунення антипатернів AOT**:
* Провести static analysis на відсутність `dynamic`, `Type.GetType()`, `MakeGenericType()` та невизначеного триммінгу.

---

### 6. Інтеграція з PHP-монолітом

* [ ] **Створення PHP Wrapper**:
* Переписати точки входу (наприклад, `notify/index.php`) для виклику `.NET` бінарника через `exec()` або `proc_open()`.

* [ ] **Стандартизація коду відповідей (Exit Codes)**:
* Впровадити чітку матрицю системних кодів завершення для коректної обробки помилок на стороні PHP.

---

### 7. Логування, Моніторинг та Консольний Вивід

* [ ] **Єдиний формат логування**:
* Налаштувати запис у папки `./logs` у форматі, аналогічному до legacy PHP-додатка.

* [ ] **Форматування консольного виводу**:
* Очистити `stdout` від службового сміття; форматувати вивід для коректного відображення в адмін-панелі (у разі запуска через веб-інтерфейс).

---

### 8. Тестування, Компіляція та Деплой

* [ ] **Unit & Integration Tests**:
* Покрити тестами парсер YAML та оркестратор сценаріїв.
* Написати інтеграційні тести з mock-серверами для провайдерів SMS/Email.

* [ ] **Релізний билд та верифікація**:
* Скомпілювати під цільову ОС: `dotnet publish -c Release -r linux-x64`.
* Перевірити розмір бінарного файлу (< 30 MB) та час холодного старту (< 15 ms).

---

## 🚦 Матриця кодів завершення (Exit Codes)

| Код (`Exit Code`) | Опис стани | Дія на стороні PHP |
| --- | --- | --- |
| `0` | **Success**: Воркфлоу успішно виконано | Завершити скрипт успішно |
| `1` | **Invalid Arguments**: Некоректні CLI-аргументи | Записати в лог помилку виклику |
| `2` | **Configuration Error**: Помилка парсингу YAML/JSON | Сповістити розробників |
| `3` | **Database Error**: Помилка з'єднання або виконання SQL | Відправити на повторну спробу (Retry) |
| `4` | **Provider Error**: Помилка зовнішніх API (SmsClub/eSputnik) | Зафіксувати сбій розсилки |
| `99` | **Unhandled Exception**: Непередбачена критична помилка | Критичний лог у системну чергу |