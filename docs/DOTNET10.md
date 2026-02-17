# .NET 10 Research: Modern Features and Changes for New Projects

> Compiled February 2026 from official Microsoft documentation, devblogs, and community sources.
> .NET 10 was released November 11, 2025 as a Long-Term Support (LTS) release, supported through November 2028.

---

## Table of Contents

1. [Solution Files: The New .slnx Format](#1-solution-files-the-new-slnx-format)
2. [Project System Changes](#2-project-system-changes)
3. [C# 14 Language Features](#3-c-14-language-features)
4. [Build System](#4-build-system)
5. [ASP.NET Core 10](#5-aspnet-core-10)
6. [Hosting Model](#6-hosting-model)
7. [Dependency Injection](#7-dependency-injection)
8. [Configuration](#8-configuration)
9. [Logging](#9-logging)
10. [Testing](#10-testing)
11. [NuGet](#11-nuget)
12. [dotnet CLI](#12-dotnet-cli)
13. [Performance Improvements](#13-performance-improvements)
14. [Breaking Changes](#14-breaking-changes)

---

## 1. Solution Files: The New .slnx Format

### Overview

.NET 10 defaults to the new **XML-based `.slnx` solution file format** when running `dotnet new sln`. This replaces the legacy `.sln` format that used a proprietary, GUID-heavy syntax. The `.slnx` format was introduced in .NET 9 SDK 9.0.200 and became the default in .NET 10.

### Why It Matters

- **Dramatically smaller**: An internal Microsoft solution went from 72 lines down to 11 lines.
- **Human-readable XML**: No more GUIDs, no more opaque metadata.
- **Fewer merge conflicts**: Git diffs are smaller and easier to resolve.
- **Aligned with .csproj**: The solution format now uses XML, consistent with project files.

### Syntax and Structure

Basic `.slnx` with solution folders:

```xml
<Solution>
  <Folder Name="/src/">
    <Project Path="src/MyApp.API/MyApp.API.csproj" />
    <Project Path="src/MyApp.Domain/MyApp.Domain.csproj" />
    <Project Path="src/MyApp.Infrastructure/MyApp.Infrastructure.csproj" />
  </Folder>
  <Folder Name="/tests/">
    <Project Path="tests/MyApp.Tests/MyApp.Tests.csproj" />
  </Folder>
  <Folder Name="/Solution Items/">
    <File Path="Directory.Build.props" />
    <File Path="Directory.Packages.props" />
    <File Path="global.json" />
  </Folder>
</Solution>
```

Key XML elements:
| Element | Description |
|---|---|
| `<Solution>` | Root element |
| `<Project Path="..." />` | References a project file (.csproj, .fsproj, etc.) |
| `<Folder Name="...">` | Virtual solution folder for grouping |
| `<File Path="..." />` | Non-project file included in a folder |

### Creating a New .slnx

```bash
# .NET 10 defaults to .slnx
dotnet new sln --name MyApp

# Add projects
dotnet sln add src/MyApp.API/MyApp.API.csproj
dotnet sln add src/MyApp.Domain/MyApp.Domain.csproj
```

### Migrating from .sln

```bash
# Migrate existing solution
dotnet sln migrate

# Or migrate a specific solution
dotnet sln MySolution.sln migrate
```

Via Visual Studio: File > Save Solution As... > select "XML Solution File (*.slnx)".

**Important**: Do NOT keep both `.sln` and `.slnx` in the same repository.

### Tooling Support

- **MSBuild**: Full support
- **Visual Studio 2022 17.13+**: Full support
- **Visual Studio 2026**: Full support
- **VS Code C# Dev Kit**: Full support
- **JetBrains Rider**: Supported
- **global.json requirement**: SDK 9.0.200 minimum

---

## 2. Project System Changes

### Target Framework Moniker

.NET 10 uses `net10.0` as its TFM:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
</Project>
```

For a web project:
```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
</Project>
```

Platform-specific TFMs:
- `net10.0-windows` (WinForms, WPF)
- `net10.0-android36` (Android API 36, JDK 21)
- `net10.0-ios`, `net10.0-maccatalyst`, etc.

### C# 14 Language Version

C# 14 ships with .NET 10. It is the default language version when targeting `net10.0`. You can explicitly set it:

```xml
<PropertyGroup>
  <LangVersion>14</LangVersion>
  <!-- Or for preview features: -->
  <LangVersion>preview</LangVersion>
</PropertyGroup>
```

### NuGet Package Pruning (New Default)

Projects targeting `net10.0` automatically prune unused framework-provided package references. This reduces restore time, disk space, and false positives from NuGet Audit. Disable with:

```xml
<PropertyGroup>
  <RestoreEnablePackagePruning>false</RestoreEnablePackagePruning>
</PropertyGroup>
```

### File-Based Apps (New)

C# 14 introduces file-based applications -- single `.cs` files that can be run directly:

```bash
#!/usr/bin/env dotnet
Console.WriteLine("Hello from a file-based app!");
```

File-based programs can reference projects with `#:project ../ClassLib/ClassLib.csproj` and are published in NativeAOT mode by default.

---

## 3. C# 14 Language Features

C# 14 ships with .NET 10 (November 2025). Key features:

### 3.1 Extension Members (Headline Feature)

The new `extension` block syntax enables extension properties, operators, and static members -- not just methods:

```csharp
public static class StringExtensions
{
    extension(string s)
    {
        // Extension property
        public bool IsNullOrEmpty => string.IsNullOrEmpty(s);

        // Extension method (new syntax, replaces old 'this' parameter style)
        public string Truncate(int maxLength)
            => s.Length <= maxLength ? s : s[..maxLength] + "...";
    }
}

// Usage:
string name = "Hello World";
bool empty = name.IsNullOrEmpty;      // Extension property
string short = name.Truncate(5);      // Extension method
```

Static extension members:
```csharp
public static class PointExtensions
{
    extension(Point)
    {
        public static Point Origin => Point.Empty;
    }
}
// Usage: Point.Origin
```

Generic extension blocks:
```csharp
public static class EnumerableExtensions
{
    extension<TSource>(IEnumerable<TSource> source)
    {
        public bool IsEmpty => !source.Any();
    }
}
// Usage: myList.IsEmpty
```

**Limitations**: Extension members cannot define backing fields or add state to the target type. They are stateless.

**Breaking change**: If you have types or aliases named `extension`, you must rename them.

### 3.2 Field Keyword for Properties

Access the compiler-generated backing field directly in property accessors:

```csharp
// Before C# 14:
private string _name = "";
public string Name
{
    get => _name;
    set => _name = value ?? throw new ArgumentNullException(nameof(value));
}

// C# 14:
public string Name
{
    get => field;
    set => field = value ?? throw new ArgumentNullException(nameof(value));
} = ""; // initializer sets the backing field
```

The `field` keyword is contextual -- only available inside `get`/`set` accessors. Use `@field` or `this.field` to disambiguate if you have a symbol named `field`.

### 3.3 Null-Conditional Assignment

The `?.` and `?[]` operators can now appear on the left side of assignments:

```csharp
// Before C# 14:
if (customer != null)
{
    customer.Name = "Updated";
}

// C# 14:
customer?.Name = "Updated";

// Works with compound operators:
customer?.OrderCount += 1;

// The right side only evaluates if left side is non-null (short-circuit)
customer?.Log = GetExpensiveLogEntry();
```

**Note**: Increment/decrement operators (`++`, `--`) cannot be used with null-conditional assignment.

### 3.4 Implicit Span Conversions

Implicit conversions among arrays, `Span<T>`, and `ReadOnlySpan<T>`:

```csharp
void ProcessData(ReadOnlySpan<int> data) { /* ... */ }

int[] array = [1, 2, 3];
ProcessData(array);          // Implicit conversion, no .AsSpan() needed

Span<int> span = array;      // Implicit
ReadOnlySpan<int> ros = span; // Implicit
```

This enables better JIT optimization with fewer temporary variables and bounds checks.

### 3.5 User-Defined Compound Assignment Operators

Define explicit `+=`, `-=`, etc. to avoid unnecessary value copying:

```csharp
public struct Vector3
{
    public float X, Y, Z;

    // Traditional operator (creates a new value)
    public static Vector3 operator +(Vector3 a, Vector3 b)
        => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    // C# 14: Compound assignment (modifies in place, avoids copy)
    public static void operator +=(ref Vector3 a, Vector3 b)
    {
        a.X += b.X;
        a.Y += b.Y;
        a.Z += b.Z;
    }
}
```

### 3.6 Partial Constructors and Events

Extends `partial` to constructors and events (C# 13 added partial properties):

```csharp
public partial class MyViewModel
{
    // Defining declaration (e.g., hand-written)
    public partial MyViewModel(string name);

    // Implementing declaration (e.g., source-generated)
    public partial MyViewModel(string name)
    {
        Name = name;
        Initialize();
    }
}
```

Only the implementing declaration can include `this()` or `base()` initializers.

### 3.7 Numeric String Comparison

Compare strings numerically instead of lexicographically:

```csharp
// Using CompareOptions.NumericOrdering
var result = string.Compare("file2.txt", "file10.txt",
    CultureInfo.CurrentCulture, CompareOptions.NumericOrdering);
// result < 0 (file2 comes before file10)
```

---

## 4. Build System

### 4.1 global.json

Pin your SDK version:

```json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestFeature"
  }
}
```

The `rollForward` policy options remain: `patch`, `feature`, `minor`, `major`, `latestPatch`, `latestFeature`, `latestMinor`, `latestMajor`, `disable`.

### 4.2 Directory.Build.props

Central place for shared MSBuild properties across all projects:

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>14</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
  </PropertyGroup>
</Project>
```

### 4.3 Directory.Packages.props (Central Package Management)

NuGet Central Package Management (CPM) is the recommended approach for multi-project solutions:

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
  </PropertyGroup>

  <ItemGroup>
    <!-- Define versions centrally -->
    <PackageVersion Include="Microsoft.Extensions.Hosting" Version="10.0.0" />
    <PackageVersion Include="Serilog.AspNetCore" Version="9.0.0" />
    <PackageVersion Include="xunit" Version="3.0.0" />
  </ItemGroup>

  <ItemGroup>
    <!-- Global packages applied to all projects -->
    <GlobalPackageReference Include="SonarAnalyzer.CSharp" Version="10.0.0" />
  </ItemGroup>
</Project>
```

In individual `.csproj` files, omit the `Version`:
```xml
<ItemGroup>
  <PackageReference Include="Microsoft.Extensions.Hosting" />
</ItemGroup>
```

Key features:
- **Transitive pinning**: Override transitive dependency versions to patch vulnerabilities.
- **GlobalPackageReference**: Apply packages (e.g., analyzers) to every project.
- **Package source mapping**: Use `NU1507` warning to enforce source mapping for security.
- **Opt-out per project**: `<ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>`.

### 4.4 MSBuild Tasks Unification

Starting with .NET 10, `msbuild.exe` and Visual Studio 2026 can run MSBuild tasks built for .NET (not just .NET Framework). This eliminates the need for maintaining separate task implementations.

---

## 5. ASP.NET Core 10

### 5.1 Built-in Minimal API Validation

Automatic validation using DataAnnotations:

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddValidation(); // New in .NET 10

var app = builder.Build();

app.MapPost("/products", (Product product) =>
{
    return Results.Created($"/products/{product.Id}", product);
});

public record Product(
    [property: Required] int Id,
    [property: Required, StringLength(100)] string Name,
    [property: Range(0.01, 10000)] decimal Price
);
```

On validation failure, returns 400 Bad Request automatically. Customize with `IProblemDetailsService`. Disable per endpoint with `.DisableValidation()`. AOT-friendly via source generator.

### 5.2 Server-Sent Events (SSE) Support

Native SSE API for one-way server-to-client streaming:

```csharp
app.MapGet("/notifications", (CancellationToken ct) =>
{
    async IAsyncEnumerable<SseItem<Notification>> Stream(
        [EnumeratorCancellation] CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            yield return SseItem.Create(
                new Notification("New update available"),
                eventType: "notification");
            await Task.Delay(5000, token);
        }
    }

    return TypedResults.ServerSentEvents(Stream(ct));
});
```

Use SSE for dashboards, notifications, progress bars. Use SignalR for bi-directional, complex real-time apps.

### 5.3 OpenAPI 3.1 Support

- Default OpenAPI spec version is now 3.1.
- YAML output via `.yaml`/`.yml` routes.
- XML doc comments automatically included in generated docs (enable `<GenerateDocumentationFile>true</GenerateDocumentationFile>`).
- Internal OpenAPI.NET library updated to v2.0 (breaking change for custom transformer authors).

### 5.4 JSON Patch with System.Text.Json

New JSON Patch implementation based on `System.Text.Json` (replacing the Newtonsoft.Json-based one), with significant performance benefits.

### 5.5 PipeReader-Based JSON Parsing

MVC and Minimal APIs now support `PipeReader`-based JSON parsing for better throughput.

### 5.6 Empty Form Value Handling

When using `[FromForm]` with complex objects, empty string values are now converted to `null` for nullable types (e.g., `DateOnly?`) instead of causing parse failures.

### 5.7 Safer Redirect Validation

`RedirectHttpResult.IsLocalUrl` provides a safer way to validate redirect targets.

### 5.8 Authentication & Authorization Changes

- Cookie authentication no longer redirects for API endpoints -- returns `401`/`403` status codes directly.
- Built-in **passkey** registration and authentication support in ASP.NET Core Identity.
- Enhanced OIDC and Microsoft Entra ID integration with encrypted token caching.

### 5.9 Blazor Updates

- Comprehensive **metrics and tracing** for component lifecycle, navigation, events, circuit management.
- **Bundler-friendly output** for WebAssembly (Gulp, Webpack, Rollup support via `WasmBundlerFriendlyBootConfig`).
- `ResourcePreloader` component replaces `<link>` headers for preloading WASM assets.
- `NavLink` ignores query string/fragment with `NavLinkMatch.All`.
- `QuickGrid` new `HideColumnOptionsAsync` method.

---

## 6. Hosting Model

### Key Breaking Change: Legacy Hosting Deprecated

`WebHostBuilder`, `IWebHost`, and `WebHost` are now **marked obsolete** in .NET 10. The modern pattern is mandatory:

```csharp
// The ONLY supported pattern going forward:
var builder = WebApplication.CreateBuilder(args);

// Configure services
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure middleware
app.MapControllers();
app.MapOpenApi();

app.Run();
```

If you have legacy code using `WebHostBuilder`, `IWebHost`, or `WebHost`, migrate to `WebApplication.CreateBuilder()` now.

---

## 7. Dependency Injection

### Keyed Services (Continued from .NET 8)

Register and resolve multiple implementations by key:

```csharp
builder.Services.AddKeyedSingleton<INotifier, EmailNotifier>("email");
builder.Services.AddKeyedSingleton<INotifier, SmsNotifier>("sms");

// Resolve:
app.MapGet("/notify", ([FromKeyedServices("email")] INotifier notifier) =>
{
    notifier.Send("Hello");
});
```

### AOT-Optimized Service Registration

With `PublishAot=true`, source generators produce optimized service registration code, eliminating reflection overhead for `Configure<T>()` and `AddScoped<TService, TImplementation>()` calls.

### Service Graph Caching

When a service graph is first resolved, the container creates and caches an execution plan for the entire dependency tree. Subsequent resolutions reuse this cached plan for near-zero overhead.

---

## 8. Configuration

### No Major API Changes

The `IConfiguration` system remains fundamentally the same. Key patterns:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Configuration sources (in priority order, last wins):
// 1. appsettings.json
// 2. appsettings.{Environment}.json
// 3. User secrets (Development)
// 4. Environment variables
// 5. Command-line arguments

// Custom environment variable prefix:
builder.Configuration.AddEnvironmentVariables("MYAPP_");

// Bind to options:
builder.Services.Configure<MyOptions>(
    builder.Configuration.GetSection("MyOptions"));
```

### Dynamic Reload

File-based configuration providers reload logging config by default. Programmatic reload: `IConfigurationRoot.Reload()`.

---

## 9. Logging

### Changes

- **Exception diagnostics suppressed**: When `IExceptionHandler.TryHandleAsync` returns `true`, exception details are no longer logged (behavioral change).
- The overall logging architecture (`ILogger<T>`, `ILoggerFactory`, provider model) is unchanged.
- Hot-reload of log levels via file configuration providers continues to work.

### Standard Setup

```csharp
var builder = WebApplication.CreateBuilder(args);

// Logging is configured via appsettings.json by default:
// "Logging": { "LogLevel": { "Default": "Information" } }

// Add additional providers:
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Or configure log levels programmatically:
builder.Logging.SetMinimumLevel(LogLevel.Warning);
```

---

## 10. Testing

### Microsoft.Testing.Platform (MTP) -- Now Natively Integrated

.NET 10 marks the turning point where MTP becomes the **first-class, natively integrated** test runner for `dotnet test`. In .NET 9, MTP required manual opt-in; in .NET 10, it is natively supported.

### Framework Support

| Framework | MTP Support | Opt-In Property |
|---|---|---|
| **MSTest** | Built-in | N/A (default) |
| **xUnit v3** | Supported | `<UseMicrosoftTestingPlatformRunner>true</UseMicrosoftTestingPlatformRunner>` |
| **NUnit 5** | Supported | `<EnableNUnitRunner>true</EnableNUnitRunner>` + `<OutputType>Exe</OutputType>` |
| **TUnit** | Built entirely on MTP | N/A (default) |

**Important**: xUnit v2 and MSTest v2 do NOT support MTP. You must upgrade first.

### Key Benefits of MTP

- **Faster execution**: Embedded directly in test projects, no `vstest.console` dependency.
- **NativeAOT support**: Tests can be compiled ahead-of-time.
- **Self-contained test executables**: Tests produce standalone executables.
- **Better diagnostics**: Enhanced in .NET 10.

### Recommended Setup

Add to `Directory.Build.props` to ensure all test projects use MTP:

```xml
<PropertyGroup>
  <TestingPlatformDotnetTestSupport>true</TestingPlatformDotnetTestSupport>
</PropertyGroup>
```

### Migration from VSTest

- `RunSettings` replaced by `testconfig.json` (though MSTest and NUnit still support RunSettings).
- "Logger" concept replaced by "reporter".
- xUnit v3 uses a different filter format than VSTest.
- Visual Studio Test Explorer supports MTP from version 17.14+.

---

## 11. NuGet

### Transitive Dependency Auditing (New Default)

For projects targeting `net10.0`, `NuGetAuditMode` defaults to `all` (previously `direct`):

```xml
<!-- This is now the default for net10.0 -->
<PropertyGroup>
  <NuGetAuditMode>all</NuGetAuditMode>
</PropertyGroup>
```

Both direct AND transitive dependencies are scanned for vulnerabilities during `dotnet restore`.

### Audit Severity Configuration

```xml
<PropertyGroup>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  <!-- Keep vulnerability warnings as warnings, not errors -->
  <WarningsNotAsErrors>NU1901;NU1902;NU1903;NU1904</WarningsNotAsErrors>
  <!-- Or treat only high/critical as errors -->
  <WarningsAsErrors>$(WarningsAsErrors);NU1903;NU1904</WarningsAsErrors>
</PropertyGroup>
```

### Useful CLI Commands

```bash
# List vulnerable packages
dotnet list package --vulnerable

# Trace why a package is referenced
dotnet nuget why MySolution.slnx SomePackage

# Update all vulnerable packages
dotnet package update --vulnerable
```

### Package Source Mapping

Warning `NU1507` appears with multiple package sources. Configure source mapping in `nuget.config`:

```xml
<configuration>
  <packageSources>
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <add key="myFeed" value="https://myfeed.example.com/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
    <packageSource key="myFeed">
      <package pattern="MyCompany.*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
```

---

## 12. dotnet CLI

### New Commands and Features

| Command/Feature | Description |
|---|---|
| `dotnet tool exec` | Execute a .NET tool without installing it (ideal for CI/CD) |
| `dotnet sln migrate` | Convert `.sln` to `.slnx` format |
| `dotnet new sln` | Now defaults to `.slnx` format |
| `--cli-schema` | Outputs JSON tree representation of CLI commands |
| `dotnet package update --vulnerable` | Update all vulnerable packages |
| `dotnet nuget why` | Trace why a package is referenced |

### File-Based Program Execution

```bash
# Run a .cs file directly
dotnet run app.cs

# Or with shebang support on Unix
chmod +x app.cs
./app.cs
```

### MSBuild Unification

`msbuild.exe` and Visual Studio 2026 can now run MSBuild tasks built for .NET, not just .NET Framework. This eliminates duplicate task maintenance.

---

## 13. Performance Improvements

### JIT Compiler

- **Struct arguments**: Members placed directly in registers instead of on the stack, eliminating unnecessary memory operations.
- **Loop inversion**: Graph-based loop recognition replaces lexical analysis, improving optimization for `for` and `while` loops.
- **Array interface devirtualization**: Eliminates virtual method calls when passing arrays to `IEnumerable<T>`, making iterations up to 68% faster.
- **Stack allocation**: Small arrays of both value and reference types can be stack-allocated when escape analysis proves they don't outlive their scope.
- **Block reordering**: Uses asymmetric Travelling Salesman Problem heuristic (3-opt) for near-optimal basic block ordering, improving hot path density.
- **Hardware acceleration**: Intel AVX10.2, Arm64 SVE/SVE2 support -- 15-30% raw performance gains in vectorized workloads.

### NativeAOT

- Smaller binaries, faster startup.
- Type preinitializer supports all `conv.*` and `neg` opcodes.
- Android: Startup improved from ~1.3s (MonoAOT) to 271-331ms.
- Cold start improvements up to 86% in serverless environments.
- File-based apps default to NativeAOT publishing.

### Garbage Collector

- Arm64 write-barrier improvements reduce GC pause times by 8-20%.
- Stack allocation reduces GC pressure significantly.

### ASP.NET Core

- Optimized middleware dispatch, static pipeline analysis, faster endpoint selection.
- 3x smaller standard deviation in execution times (less jitter).
- Up to 2x faster with 70-90% less GC pressure in some workloads.

---

## 14. Breaking Changes

### Tooling Requirements

- **.NET 10 requires Visual Studio 2026**. Visual Studio 2022 cannot target .NET 10 or use C# 14.
- Minimum Rider version with C# 14 support: ReSharper/Rider 2025.3.

### SDK Breaking Changes

| Change | Type | Impact |
|---|---|---|
| `dotnet new sln` defaults to `.slnx` | Behavioral | CI/CD scripts may need updating |
| NuGetAudit defaults to `all` for `net10.0` | Behavioral | Transitive vulnerability warnings appear |
| Package pruning enabled by default | Behavioral | Some edge-case restore behavior changes |

### ASP.NET Core Breaking Changes

| Change | Type | Impact |
|---|---|---|
| `WebHostBuilder`/`IWebHost`/`WebHost` obsoleted | Source | Must migrate to `WebApplication.CreateBuilder()` |
| `IActionContextAccessor` obsoleted | Source | Replace with alternatives |
| Razor runtime compilation obsoleted | Source | Use Hot Reload instead |
| Cookie auth no longer redirects for API endpoints | Behavioral | APIs return 401/403 directly |
| `AddRazorRuntimeCompilation` marked obsolete | Source | Use Hot Reload |
| `IncludeOpenAPIAnalyzers` deprecated | Source | MVC API analyzers removed |
| OpenAPI.NET updated to v2.0 | Binary | Custom transformers need updating |

### Runtime & Libraries Breaking Changes

| Change | Type | Impact |
|---|---|---|
| APIs marked `[Obsolete]` in .NET 8/9 removed | Binary | Recompilation needed |
| Default encoding changes to UTF-8 in some APIs | Behavioral | File handling may be affected |
| `System.Linq.Async` replaced by `System.Linq.AsyncEnumerable` | Source | Remove old package, update method names |
| C# 13 stricter compiler rules | Source | New warnings/errors |
| Uri > 65K characters no longer throws | Behavioral | Previously silent bugs may surface |
| Exception details suppressed when TryHandleAsync returns true | Behavioral | Logging changes |

### EF Core 10

- Dynamic conditional bulk delete breaking change.
- Various query translation changes.

### Migration Path

1. Update `global.json` to .NET 10 SDK.
2. Change `<TargetFramework>` to `net10.0`.
3. Update NuGet packages to .NET 10-compatible versions.
4. Address compiler warnings and errors.
5. Test thoroughly -- use `dotnet list package --vulnerable` for security.
6. Use the **.NET Upgrade Assistant** for automated guidance.

---

## Sources

- [Microsoft Learn -- What's New in .NET 10](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/overview)
- [Announcing .NET 10 -- .NET Blog](https://devblogs.microsoft.com/dotnet/announcing-dotnet-10/)
- [What's New in C# 14 -- Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14)
- [Introducing C# 14 -- .NET Blog](https://devblogs.microsoft.com/dotnet/introducing-csharp-14/)
- [C# 14 Extension Members -- .NET Blog](https://devblogs.microsoft.com/dotnet/csharp-exploring-extension-members/)
- [What's New in ASP.NET Core 10 -- Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/release-notes/aspnetcore-10.0?view=aspnetcore-10.0)
- [Introducing SLNX Support -- .NET Blog](https://devblogs.microsoft.com/dotnet/introducing-slnx-support-dotnet-cli/)
- [New Simpler Solution File Format -- Visual Studio Blog](https://devblogs.microsoft.com/visualstudio/new-simpler-solution-file-format/)
- [SLNX Breaking Change -- Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/compatibility/sdk/10.0/dotnet-new-sln-slnx-default)
- [The New .slnx Solution Format -- Milan Jovanovic](https://www.milanjovanovic.tech/blog/the-new-slnx-solution-format-migration-guide)
- [.slnx Format -- NDepend Blog](https://blog.ndepend.com/slnx-the-new-net-solution-xml-file-format/)
- [Microsoft.Testing.Platform Adoption -- .NET Blog](https://devblogs.microsoft.com/dotnet/mtp-adoption-frameworks/)
- [dotnet test with MTP -- .NET Blog](https://devblogs.microsoft.com/dotnet/dotnet-test-with-mtp/)
- [Performance Improvements in .NET 10 -- .NET Blog](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-10/)
- [Breaking Changes in .NET 10 -- Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/compatibility/10)
- [Migration from ASP.NET Core 9 to 10 -- Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/migration/90-to-100?view=aspnetcore-10.0)
- [NuGetAudit 2.0 -- .NET Blog](https://devblogs.microsoft.com/dotnet/nugetaudit-2-0-elevating-security-and-trust-in-package-management/)
- [NuGetAudit Transitive Packages Breaking Change -- Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/compatibility/sdk/10.0/nugetaudit-transitive-packages)
- [Central Package Management -- Microsoft Learn](https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management)
- [SDK and Tooling Changes -- Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/sdk)
- [Runtime Changes -- Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/runtime)
- [SSE in ASP.NET Core 10 -- Milan Jovanovic](https://www.milanjovanovic.tech/blog/server-sent-events-in-aspnetcore-and-dotnet-10)
- [.NET Conf 2025 Recap -- .NET Blog](https://devblogs.microsoft.com/dotnet/dotnet-conf-2025-recap/)
- [C# 14 Field Keyword -- Laurent Kempe](https://laurentkempe.com/2025/12/27/csharp-14-field-keyword-simplifying-property-accessors/)
- [C# 14 Extension Members Guide -- Laurent Kempe](https://laurentkempe.com/2025/12/29/csharp-14-extension-members-complete-guide/)
- [C# 14 Null-Conditional Assignment -- Laurent Kempe](https://laurentkempe.com/2025/12/28/csharp-14-null-conditional-assignment-complete-guide/)
