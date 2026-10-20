# XML documentation standard

This is the repository standard for the client and the Windows hosting library. It follows the XML and DocFX model used by the related client libraries and adds an automated gate. The checker can be copied into another .NET 10 SDK repository without a Roslyn NuGet dependency.

## Required coverage

Every externally visible public or protected declaration, including generated models and enum values, must have meaningful XML documentation. A member inside an internal type is outside public API coverage. Implementation comments should explain token durability, ownership and unusual protocol decisions where the code alone is insufficient.

Required tags are:

- A nonempty `summary` describing the observable contract.
- One `param` per parameter, including primary constructor parameters, and one `typeparam` per type parameter.
- A `returns` tag for each non-void method, including asynchronous methods. Explain what task completion means.
- A `value` tag for every property or indexer. Explain null, default or calculated values when relevant.
- `exception` tags for material validation, transport, protocol and state failures. Document cancellation, dependency ownership, secret handling and persistence requirements where applicable.

State units only where a source establishes them. If Easee supplies no field description, identify the wire field and the documentation gap rather than inventing its meaning. Unverified supplements must be labelled explicitly. Do not imply that HTTP acceptance proves physical charging or that offline coverage establishes account permission.

`inheritdoc` may refer explicitly to a documented contract, or inherit an overridden member's documentation. It must not conceal a missing standalone contract. Generated declarations meet the same coverage requirements; change `tools/generate.py`, then regenerate.

## Automated gate

Run from the repository root:

```powershell
pwsh tools/Test-Documentation.ps1
dotnet pack EaseeChargerClient/EaseeChargerClient.csproj -c Release -o artifacts/nuget
pwsh tools/Test-Package.ps1 -Package artifacts/nuget/EaseeChargerClient.0.1.0.nupkg
pwsh tools/Build-Documentation.ps1
```

The first command rejects `CS1591` suppression, rebuilds both libraries on both targets with warnings treated as errors, runs the checker and its negative controls, and verifies the generated XML files. The checker validates tag presence, parameter names and XML structure; the compiler validates references. Semantic accuracy, useful descriptions, relevant exceptions and source-backed units still require review. Do not add warning suppression to pass this gate.

`tools/DocumentationAudit` uses the C# parser supplied with the selected .NET SDK. Its source scan is a structural check of the repository's declarations; it does not replace compilation for each target or attempt a general analysis of every possible preprocessor configuration. Keep target-specific declarations covered when adding conditional source.

The package gate verifies assembly/XML pairs for `net10.0` and `net472`, bundled README, changelog, release notes and licence, and the absence of test dependencies in the production package. DocFX builds the HTML reference with warnings treated as errors. The Pages and publication workflows run these gates before uploading or publishing their outputs.
