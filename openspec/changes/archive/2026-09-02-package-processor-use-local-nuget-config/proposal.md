## Why

`PackageProcessor` hardcodes `https://api.nuget.org/v3/index.json` as its single package source. This ignores the user's local NuGet configuration, so private/authenticated feeds configured in `NuGet.Config` are never consulted, and credentials for those feeds are never used. The package-search tool therefore cannot resolve packages that live only on a user's configured private feed.

## What Changes

- `PackageProcessor` loads the user's NuGet configuration (global, machine, and user `NuGet.Config` files) via NuGet's own settings machinery instead of hardcoding a single source.
- The processor builds a `SourceRepository` per configured package source, carrying each source's credentials so authenticated private feeds work automatically.
- When downloading or resolving versions, the processor tries the configured sources in order, falling back across sources when a package is absent or a source is unreachable.
- No new package dependencies: `NuGet.Configuration` is already available transitively via `NuGet.Protocol`.

## Capabilities

### New Capabilities
- `package-source-config`: Covers loading package sources and their credentials from the user's local NuGet configuration and using them for package download and version resolution.

### Modified Capabilities
<!-- None: package-framework-selection and search-index-caching are unaffected at the behavior level. -->

## Impact

- `NuGex/PackageProcessor.fs`: replace the single hardcoded `repository` binding with config-driven source repositories; update `getLatestVersion`, `downloadPackage`, `processPackage`, and `getPackageReadme` to use them.
- `NuGex/Cli.fs` and `NuGex/Mcp.fs`: callers unchanged — they already call `processPackage`/`getPackageReadme`.
- No new dependencies; relies on `NuGet.Configuration` already present in the dependency graph.
- Fallback behavior: if no sources are configured or a source is unreachable, behavior should not crash the CLI/MCP tool.
