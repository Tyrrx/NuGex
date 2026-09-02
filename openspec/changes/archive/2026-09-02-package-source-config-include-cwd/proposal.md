## Why

The just-implemented `package-source-config` capability loads the user's NuGet configuration via `Settings.LoadDefaultSettings(null)`, which reads global, machine, and user `NuGet.Config` files — but passing `null` skips NuGet's directory-walking step. A `NuGet.Config` that lives in the current working directory (or any subdirectory of it) is therefore never consulted, so packages and credentials configured at the repo/directory level are missed.

## What Changes

- `PackageProcessor` loads the NuGet configuration rooted at the current working directory instead of passing `null` to `Settings.LoadDefaultSettings`, so any `NuGet.Config` in the CWD or any of its subdirectories is layered into the loaded sources (alongside user and machine configs, which remain unchanged).
- No new dependencies; still uses NuGet's own settings machinery.

## Capabilities

### New Capabilities
- `package-source-config`: Extends the capability that loads package sources from local NuGet configuration to also discover a `NuGet.Config` present in the current working directory or any subdirectory of it.

### Modified Capabilities
<!-- None: the prior change is not yet archived, so package-source-config is treated as a new capability here and both deltas merge on archive. -->

## Impact

- `NuGex/PackageProcessor.fs`: change the `Settings.LoadDefaultSettings(null)` call to pass the current working directory as the root (e.g. `Environment.CurrentDirectory`).
- `NuGex/Cli.fs` and `NuGex/Mcp.fs`: callers unchanged.
- No dependency changes.
