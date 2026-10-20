# EaseeChargerClient

Independent, unofficial C# client for Easee EV chargers, with a Windows desktop workbench and an interactive test console. Copyright Â© 2026 Neil Colvin. MIT licence. This project is not affiliated with or endorsed by Easee.

[GitHub repository](https://github.com/oznetmaster/EaseeChargerClient) · [HTML API documentation](https://oznetmaster.github.io/EaseeChargerClient/)

Version **0.1.0** is the initial NuGet release candidate; the package has not yet been published.

## Features and targets

- Typed asynchronous methods for all **168 non-deprecated REST operations** in the checked-in Easee OpenAPI contract, covering account, site, charger, installer and operator APIs.
- Typed request, response, enum and error models using System.Text.Json attributes, including nullable observation models and typed site JSON Patch values.
- SignalR current-state subscriptions and real-time updates, with subscription restoration after reconnection.
- Authentication with serialized, durable single-use refresh-token rotation. Requests and charging commands are never automatically replayed.
- NUnit 5 offline and separately enabled live suites, XML reference documentation for both library targets, and DocFX HTML documentation.

| Project | Targets | Purpose |
| --- | --- | --- |
| EaseeChargerClient | `net10.0`; `net472` | Independently packaged REST and SignalR library |
| EaseeChargerClient.Hosting | `net10.0-windows`; `net472` | Windows encrypted profiles and shared workbench services |
| EaseeChargerClient.TestConsole | `net10.0-windows`; `net472` | Interactive shell and command-line client |
| EaseeChargerClient.App | `net10.0-windows` | WPF desktop workbench |
| *.Tests | Corresponding targets | Offline NUnit 5 suites |
| EaseeChargerClient.LiveTests | `net10.0-windows`; `net472` | Separately enabled account and charger tests |

All projects use the latest C# language supported by the selected .NET 10 SDK. The .NET Framework build uses reference assemblies and Hafner compatibility packages. The production NuGet package contains the core library; the Windows companions are separate application outputs.

## Library use

Provide an `IEaseeTokenProvider`, or use `EaseeSession` to own one rotating token chain. The persistence callback must durably save every supplied token state, including its pending-refresh marker, before it completes.

```csharp
using EaseeChargerClient;

// LoadTokenAsync and SaveTokenAtomicallyAsync are application-owned secure storage.
EaseeToken? savedToken = await LoadTokenAsync(cancellationToken);
using var authentication = new EaseeClient();
using var session = new EaseeSession(authentication, SaveTokenAtomicallyAsync, savedToken);
using var client = new EaseeClient(new EaseeClientOptions { TokenProvider = session });

// On first use, or when the session requires reauthentication, obtain the account
// and password through the application's private interactive input and call:
// await session.SignInAsync(account, password, cancellationToken);

var response = await client.ChargerGetChargerByIdAsync(
    chargerSerial, cancellationToken: cancellationToken);
var charger = response.Value;

await using var updates = new EaseeRealtimeClient(session, new[] { chargerSerial }, update =>
{
    // DeviceId, ObservationId, DataType, Timestamp and the hub's string Value.
    return ProcessUpdateAsync(update);
});
await updates.StartAsync(cancellationToken);
// Keep the receiver alive until the application no longer needs updates.
```

The storage and update-handler functions in this example are application code. A caller-supplied `HttpClient`, `ILogger` and token provider remain caller-owned. Dispose `EaseeStreamResponse` after consuming its content. A stream's lifetime after the response headers arrive is controlled by its consumer.

REST methods return a typed `EaseeResponse<T>` with HTTP status, body and headers. Non-success statuses throw `EaseeApiException`; inspect `StatusCode`, `ErrorResponse`, `Problem` and `Headers`, including `Retry-After`. Published error DTOs and typed validation collections are preserved. Transport failures and cancellation surface directly. There is no automatic retry after a timeout or rate limit.

REST observations deserialize through attributes into `EaseeBooleanObservation`, `EaseeDoubleObservation`, `EaseeIntegerObservation`, `EaseeStringObservation` or `EaseePositionObservation`. Their nullable values preserve missing data, zero and false; their `DataType` is calculated from the concrete model. SignalR transmits its update values as strings, described by the accompanying data type.

Site JSON Patch operations use `EaseeSitePatchValue<T>` for concrete scalar, model, collection or explicit null values. During deserialization, the path selects the published site model's value type. Unknown paths fail. Arbitrary JSON is limited to the explicitly open-ended Problem Details extensions represented by `EaseeValue`.

## Windows desktop and secure profiles

Start the WPF application from the source checkout:

```powershell
dotnet run --project EaseeChargerClient.App -c Release
```

Choose a profile name and enter the account email/phone and password using **Sign in securely**. Passwords are never saved. Tokens and selections are encrypted for the current Windows user using DPAPI, outside the repository under `%LOCALAPPDATA%/EaseeChargerClient/Profiles/PROFILE`.

Use **List my sites**, choose a site by name and ID, then choose its charger by name and serial number. Selecting another site clears the previous charger selection. Use **Save selection** to retain the choice. Easee calls the charger serial number `id`; the site-charger results also label it `SerialNumber`.

The app provides charger inspection, real-time updates and a workbench for every supported operation. Modifying operations require the workbench's write permission. Returned account and charger data may be private.

Close the app before another process uses the same profile. One process owns a profile and its refresh-token chain for the entire session. Before a refresh exchange, the session durably records a pending marker; it stores the replacement before use. An interrupted or ambiguous exchange requires a fresh sign-in rather than reusing the old refresh token.

## Console and CLI

Launching the console without arguments opens an interactive shell. Use `help` to list commands. Password input is masked; credentials are never accepted as CLI options or printed as generic operation output.

```powershell
dotnet run --project EaseeChargerClient.TestConsole -c Release -f net10.0-windows -- login --profile tests
dotnet run --project EaseeChargerClient.TestConsole -c Release -f net10.0-windows -- operations
dotnet run --project EaseeChargerClient.TestConsole -c Release -f net10.0-windows -- describe Charger_GetChargerById
dotnet run --project EaseeChargerClient.TestConsole -c Release -f net10.0-windows -- select --profile tests --site SITE_ID --charger SERIAL
dotnet run --project EaseeChargerClient.TestConsole -c Release -f net10.0-windows -- call Charger_GetChargerById --profile tests
dotnet run --project EaseeChargerClient.TestConsole -c Release -f net10.0-windows -- stream --profile tests --seconds 30
```

Replace the example resource placeholders with identifiers from your account. Use `--arg name=value` repeatedly for parameters, `--body-file FILE` for a typed request body, `--header name=value` for additional headers, and `--output FILE` for a binary or streamed result. Strings are literal; non-string arguments use JSON syntax. Each modifying operation requires `--allow-write`. Both console targets offer the same commands. Exit codes are 0 for success, 1 for failure and 130 for cancellation.

## Build, tests and documentation

Windows, the .NET 10 SDK, PowerShell 7 and Python 3 are required for the complete solution and its generation workflow. .NET Framework runtime tests require a Windows Framework runtime; the WPF app requires the .NET 10 Windows desktop runtime when published as framework-dependent.

```powershell
dotnet build EaseeChargerClient.slnx -c Release
pwsh tools/Test-Offline.ps1
pwsh tools/Test-Documentation.ps1
pwsh tools/Build-Documentation.ps1
dotnet pack EaseeChargerClient/EaseeChargerClient.csproj -c Release -o artifacts/nuget
pwsh tools/Test-Package.ps1 -Package artifacts/nuget/EaseeChargerClient.0.1.0.nupkg
```

Offline suites exercise every supported route and published schema, transport, cancellation, authentication rotation, streaming subscriptions, encrypted persistence and companion behavior. The normal command excludes live execution. HTML documentation is generated under `docfx_project/_site`; [DOCUMENTATION.md](DOCUMENTATION.md) defines the XML standard and its gate. [PUBLISHING.md](PUBLISHING.md) describes GitHub Pages and NuGet trusted publishing setup.

After securely signing in and saving a selection, close the application and run the read-only live suite explicitly:

```powershell
pwsh tools/Test-Live.ps1 -Profile tests -Framework net10.0-windows
pwsh tools/Test-Live.ps1 -Profile tests -Framework net472
```

The wrapper holds the encrypted profile lease while the blocking test command runs, prepares temporary inputs containing only a short-lived access token and the selected IDs, and deletes them afterwards. Live fixtures require separate opt-in; credentials and responses are not printed. Optional role-specific GET scenarios can be supplied privately through `EASEE_LIVE_SCENARIOS`. Never commit account credentials, resource IDs or live input files.

The separate `tools/Test-StartCharging.ps1 -Profile NAME -AuthorizeSingleStartCommand` executes one start request and observes state. It is excluded from the ordinary live suite and requires explicit authorization for the selected charger. A fully charged vehicle may immediately report charging completed with zero power; HTTP acceptance alone does not prove energy delivery.

## API source and known limits

The [checked-in specification](api/easee-cloud-api.json) was retrieved from [Easee's published OpenAPI endpoint](https://developer.easee.com/openapi/easee-cloud-api.json) on 20 October 2026. [Coverage](api/coverage.json) records its SHA-256, 168 included operations and 12 excluded deprecated operations. The 233 vendor schema definitions include shared types referenced by current endpoints. Generated names preserve vendor prefixes to distinguish different service contracts. `EaseeOperations.All` provides the operation catalogue; `tools/Regenerate.ps1` regenerates and formats the surface.

Account, installer and operator permissions differ. Implementing an operation does not grant permission, and a home account cannot verify every partner-only path. Baseline live tests have passed with the configured account; offline contract coverage is broader than live coverage.

SignalR uses the products hub and `SubscribeWithCurrentState`, as described by the [pinned streaming guide](api/signalr-stream.md). Reconnection uses Microsoft's bounded default policy; initial connection failure surfaces to the caller. The separate paid AMQP service is outside scope.

The vendor omits a success-body contract for `AuthorizeCharging_StartCharging`. Successful status codes are accepted without inventing a body model. Direct OCPP `GetWorkflowStreamEndpoint` defines no event format and documents a 204 response; content is preserved as an owned stream. Supply its `traceparent` through `EaseeRequestOptions.Headers`. Partner workflow behavior still needs validation with appropriate access. This library configures OCPP through REST and does not implement an OCPP server.

Position observations use nullable latitude, longitude, altitude and DOP from a related upstream IoT model. **Compatibility with an actual Easee Position payload is unverified.** The configured charger returned no location observation. No location is inferred or written. The model research and source revisions are recorded in [model-contract-evidence.json](api/model-contract-evidence.json).
