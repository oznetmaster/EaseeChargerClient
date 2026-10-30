# Changelog

Changes to the independent Easee client library and its companion applications are recorded here. Versions follow Semantic Versioning.

## [1.0.0](https://github.com/oznetmaster/EaseeChargerClient/releases/tag/v1.0.0) — Unreleased

### Added

- Typed asynchronous methods for all 168 non-deprecated operations in the checked-in Easee REST specification, with operation discovery, cancellation, per-request headers and owned stream responses.
- Published request, response, enum and error models with System.Text.Json attributes. Nullable REST observation models use the published discriminator; site JSON Patch values retain their concrete scalar, model, collection or null type.
- A single-owner authentication session with serialized token rotation, durable pending-exchange markers and persistence of replacement tokens before use.
- SignalR current-state and update subscriptions with subscription restoration after reconnection.
- A Windows WPF workbench with secure sign-in, site and charger pickers, labelled charger serial numbers, real-time updates and access to every supported REST operation.
- An interactive and command-line test console on .NET 10 and .NET Framework 4.7.2, sharing encrypted Windows profiles with the desktop application.
- NUnit 5 offline suites and separately enabled read-only live suites, plus a separately authorized single-start control test.
- XML API documentation, a reusable documentation coverage gate, DocFX HTML documentation and prepared GitHub Pages and NuGet trusted-publishing workflows.

### Compatibility notes

- The library targets `net10.0` and `net472`; the WPF application targets `net10.0-windows`. All source uses the latest C# language supported by the selected SDK, with compatibility packages for .NET Framework.
- Deprecated REST endpoints and the separate paid AMQP service are excluded. Endpoint permission remains controlled by Easee.
- Position fields are nullable and derived from an upstream IoT model. Compatibility with an actual Easee Position payload is unverified.
- Arbitrary JSON is confined to explicitly open-ended Problem Details extensions. Unknown observation discriminators and unknown site patch paths are rejected.
