# Build and test

This repository uses [the shared Cake build component](https://github.com/oznetmaster/BuildAutomation/tree/c0a215e202dc1e05a37707720ab12032be610518) pinned in `component.json`. It is build tooling only; product dependencies and versions are unchanged.

The CI workflow retains its jobs, framework matrix, filters, prerequisites and artifact upload rules. Repository-owned recipes preserve the build/test/package commands. Publication, validation reuse, approvals, runner selection and hardware reservations remain in GitHub Actions.

Run the same task locally with .NET 10, Git and PowerShell:

```powershell
./build/Invoke.ps1 -Task TASK_NAME
```

For a matrix task, set its `BUILD_INPUT_*` environment values as shown in the workflow before invoking it. These are project/framework selections, not credentials. The first local invocation downloads the immutable component into ignored `artifacts/build-components/`.

Available tasks:

- `unit-tests-test-build-and-run-all-offline-nunit-5-suites`
- `unit-tests-test-verify-the-library-package`
- `unit-tests-test-verify-packaged-xml-and-release-documents`
- `api-contract-generated-3`
- `api-contract-generated-4`
- `api-contract-generated-5`

The shared component rejects missing/stale/empty/failed declared TRX results and writes one receipt per invocation beneath `artifacts/build-automation/`. It does not publish packages or invoke live hardware tests. Existing specialized scripts remain authoritative.

The unchanged solution-wide offline command also creates two empty reports for the excluded live-only assembly. `Verify-OfflineResults.ps1` requires fresh, successful results for all seven offline project/framework combinations and moves those reports into `TestResults/OfflineVerified/` for the shared zero-test guard. Empty reports are accepted only when they explicitly identify the excluded LiveTests assembly and filter, retained separately, and never counted as passed tests.

## Pages documentation

`documentation` builds the repository site and checks [the common Pages standard](https://github.com/oznetmaster/BuildAutomation/blob/c0a215e202dc1e05a37707720ab12032be610518/DOCUMENTATION-STANDARD.md): Home, Guides, API Reference, GitHub and NuGet navigation, modern theme, consistent title/footer, searchable content and every API output group. DocFX is locally pinned to 2.81.0. Existing project-specific generation and coverage checks remain in the recipe. GitHub Actions uploads/deploys the verified site; local builds publish nothing.
