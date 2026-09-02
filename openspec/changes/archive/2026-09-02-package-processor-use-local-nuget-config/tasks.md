## 1. Load sources from local NuGet config

- [x] 1.1 Add `open NuGet.Configuration` and build config-driven source repositories in PackageProcessor: replace the single `repository` binding with `Settings.LoadDefaultSettings(null)`, a `PackageSourceProvider` over it, and a `repositories` array of `Repository.Factory.GetCoreV3` per source; seed with nuget.org if the array is empty. Verify by `dotnet build` succeeding and confirming `Repository`/`repository` usages compile against the new array.
- [x] 1.2 Update `getLatestVersion` to iterate the repositories array (per-source try/catch, continue on failure) and return the best stable version from the first source that has the package, or `None` if none do. Verify with `dotnet build` and a manual `search-package` CLI run against a package on nuget.org returning a version as before.

## 2. Download across sources

- [x] 2.1 Update `downloadPackage` to try each repository in the array (per-source try/catch around `GetResourceAsync<FindPackageByIdResource>` + `CopyNupkgToStreamAsync`), returning `None` when every source fails or lacks the package. Verify with `dotnet build` and `get-package-readme` on a nuget.org package returning content.
- [x] 2.2 Verify graceful not-found path: request a nonexistent package via `search-package` CLI and confirm it returns no results / no readme without throwing or crashing the process.

## 3. Regression & verification

- [x] 3.1 Run `dotnet build` clean and confirm no warnings/errors introduced by the PackageProcessor changes.
- [x] 3.2 Confirm existing behavior is unchanged when the user config has only the default nuget.org source: `search-package` and `get-package-readme` still work for a known public package.
