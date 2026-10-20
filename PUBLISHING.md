# Publication workflow

The public repository is [oznetmaster/EaseeChargerClient](https://github.com/oznetmaster/EaseeChargerClient), with `main` as its default branch. The workflows build HTML documentation and publish releases using NuGet trusted publishing. No long-lived NuGet API key is required.

## Repository setup

The package metadata identifies this repository and the documentation site. Configure branch protection using actual hosted test and documentation checks after successful runs.

In repository **Settings → Pages**, choose **GitHub Actions** as the build source. `.github/workflows/github-pages.yml` builds the XML and HTML documentation on pull requests, and deploys the site from `main`. The documentation site is https://oznetmaster.github.io/EaseeChargerClient/.

The checked-in local tool manifest pins DocFX. Run `pwsh tools/Build-Documentation.ps1` to reproduce the HTML build. It uses the `net472` metadata surface shared by both client targets and also documents the Windows hosting library. Compilation and XML coverage are checked separately on both targets.

## NuGet trusted publishing

The publication workflow is `.github/workflows/dotnet-publish.yml`; the publishing job uses the GitHub environment **nuget**. The environment exists and the repository variable `NUGET_USER` identifies the `oznetmaster` NuGet profile.

The NuGet policy is restricted to package **EaseeChargerClient**, GitHub owner **oznetmaster**, repository **EaseeChargerClient**, workflow filename **dotnet-publish.yml**, and environment **nuget**. It permits new packages and package versions; unlisting is excluded. Verify package-name ownership or availability before the first publication.

`NuGet/login@v1` exchanges the GitHub OIDC identity for a temporary API key immediately before the push. Only the publishing job has `id-token: write`; it downloads the artifacts produced by the validation job. See [NuGet's trusted-publishing documentation](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing) for account policy setup.

Configure `RELEASE_REQUIRED_CHECKS` as a nonempty JSON array of exact check names and GitHub App IDs, including the agreed external processor validation check. Use actual check results from this repository to obtain these values. The reused release-check policy waits for successful results on the exact release revision and rejects missing, failed or newer pending checks. It also requires the hosted unit, API-generation and NUnit policy workflows. The local hardware validation already completed does not establish a hosted check on a future commit.

## Verify authentication without publishing

Run the **Publish .NET Package** workflow manually from `main` with `verify_oidc_only` enabled and the current version supplied. This mode runs only the NuGet login and verifies that a temporary credential was issued; it does not build, push a package or create a release. It uses the same workflow filename, account, environment and package-scoped NuGet policy as publication. The release validation and publishing jobs are excluded in this mode.

Normal publication retains its exact-source release checks. Authentication verification does not establish a processor-test pass or publish a package.

## Prepare a release

Update `Directory.Build.props`, `CHANGELOG.md` and `RELEASE-NOTES.md` together. Replace the candidate/unreleased status when an actual release is approved. Confirm the README's version and runtime requirements. Run:

```powershell
pwsh tools/Test-Offline.ps1
pwsh tools/Test-Documentation.ps1
pwsh tools/Build-Documentation.ps1
pwsh tools/Stage-Release.ps1 -Version 1.0.0
```

Inspect `artifacts/release`: the NuGet package, library DLL/XML archive, desktop and console archives, HTML documentation archive, release documents and SHA-256 manifest. These commands only stage local files. The staging command requires an empty release output directory to prevent mixing previous assets into a new release.

After source and checks are ready, an authorized `vVERSION` tag triggers publication. A manual invocation must select the same tagged source and matching version. The workflow verifies exact-source checks, regenerates the API, runs offline tests and documentation gates, then builds and validates the package and companion archives. It publishes through OIDC and creates a GitHub release using `RELEASE-NOTES.md`.

Verify the first hosted validation run, Pages deployment, NuGet package and downloaded release assets. Confirm both framework assemblies have their XML files and that IntelliSense and HTML reference links work. Creating the repository and policy does not publish a package. End-to-end NuGet publication is verified only by a separately authorized package release.
