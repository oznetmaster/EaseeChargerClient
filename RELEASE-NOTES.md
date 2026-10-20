# EaseeChargerClient 0.1.0

Initial release candidate. This version has not yet been published.

The library provides typed access to all 168 non-deprecated operations in the checked-in Easee REST contract and real-time SignalR charger updates. It targets .NET 10 and .NET Framework 4.7.2. Companion applications include a .NET 10 WPF workbench and a console with interactive and command-line modes.

Authentication sessions serialize single-use refresh exchanges and persist each replacement before use. The Windows applications store tokens under the current user's DPAPI protection and retain an exclusive profile lease. Passwords are entered interactively and are not saved.

The release includes typed request, response and error contracts, nullable observation models, typed JSON Patch values, XML reference files for both library targets and HTML documentation. Modifying operations are never automatically retried.

## Validation and limits

The offline suites cover every supported REST route and published schema, transport and cancellation, token rotation, SignalR subscription recovery, Windows profile persistence and companion behavior. Baseline read-only live tests passed on both desktop targets with a privately configured account. One separately authorized start request was accepted; the already charged vehicle remained completed at zero power. HTTP acceptance does not establish energy delivery.

Live coverage is limited to resources and roles available to the configured account. Installer, operator and Direct OCPP paths have contract coverage but are not all verified against partner resources. Direct OCPP workflow events are exposed as a stream because the published contract supplies no event model.

Position observations use nullable latitude, longitude, altitude and DOP from a related upstream IoT model. The Easee wire shape is unverified; the configured charger returned no location observation. No location is inferred or written. The separate paid AMQP service and deprecated REST endpoints are outside scope.

GitHub-hosted workflow execution, Pages deployment and NuGet publication must be verified after the repository and trusted-publishing policy have been configured.

See [README.md](README.md) for setup and examples, [CHANGELOG.md](CHANGELOG.md) for changes, and [PUBLISHING.md](PUBLISHING.md) for publication setup.
