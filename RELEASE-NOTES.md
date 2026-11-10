# EaseeChargerClient 1.0.1

This patch corrects the audited reliability issues while retaining the existing public API and supported frameworks.

- The streaming client detects closure during subscription setup and leaves the connection restartable. Repeated or overlapping DisposeAsync calls share one shutdown task. Stop/start/subscription operations reject disposal instead of reusing closed resources. The separate EaseeSession contract still requires pending session operations to finish before its synchronous Dispose.

## Validation

- Offline workstation tests passed in Debug and Release on .NET 10 and .NET Framework 4.7.2: Easee net10.0: 453; Easee net472: 453. No failures or skips.
- 12 selected tests passed in the shared 374-case processor run on Mono 6.12.0.107 (32-bit), with no failures or skips. The merged host's resolved dependency graph was tested; these are not eight independent release-package tests.
- Fake transports and credentials exercised ordering, cancellation, shutdown and failure recovery. No new household-device or cloud control was performed. Pairing storage is unchanged.
- Strict XML, formatting and style checks passed. Package and companion-app preparation is verified separately by the shared release pipeline.

See [the changelog](https://github.com/oznetmaster/EaseeChargerClient/blob/v1.0.1/CHANGELOG.md) and [validation provenance](https://github.com/oznetmaster/EaseeChargerClient/blob/v1.0.1/release-validation/v1.0.1.json).
