## 1. Core change

- [x] 1.1 Change `Settings.LoadDefaultSettings(null)` to `Settings.LoadDefaultSettings(Environment.CurrentDirectory)` in the `repositories` binding of PackageProcessor (PackageProcessor.fs), so a `NuGet.Config` in the CWD or its subdirectories is included. Verify by `dotnet build` succeeding with no new warnings.

## 2. Verification

- [x] 2.1 Confirm no regression when no repo-local config exists: run `search-package` for a known public package (e.g. Newtonsoft.Json) from the repo root and verify results still return as before.
- [x] 2.2 Confirm directory-level config discovery: create a temporary directory with a `NuGet.Config` pointing at a non-default feed (or nuget.org), run `search-package` from that directory, and verify the processor consults that config (no crash, sources loaded). Clean up the temporary directory afterwards.
