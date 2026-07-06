## Why

Every MCP tool call that touches `MSBuildWorkspace` (`search_solution`, and any future solution/project processing) fails unconditionally in the Nix-built package, regardless of which solution or path is used. Roslyn's `MSBuildWorkspace` loads projects via an out-of-process "BuildHost", which it locates as a physical DLL at `lib/nugex/BuildHost-netcore/Microsoft.CodeAnalysis.Workspaces.MSBuild.BuildHost.dll` next to the app. `dotnet publish` with `PublishSingleFile=true` bundles that DLL (and its own dependencies, `Microsoft.Build.Locator.dll` and `System.Collections.Immutable.dll`) into the single-file bundle instead of leaving them as loose files — so only their `.deps.json`/`.runtimeconfig.json` companions remain on disk, and `MSBuildWorkspace.OpenSolutionAsync` always throws `The build host could not be found at '...BuildHost.dll'`.

**Correction discovered during implementation:** the true source of `PublishSingleFile=true` is `<PublishSingleFile>true</PublishSingleFile>` in `NuGex/NuGex.fsproj`'s `PropertyGroup` — not `flake.nix`. `flake.nix`'s `extraPublishFlags` and `selfContained` attributes are not real `buildDotnetModule` parameters (checked against this repo's exact pinned nixpkgs revision, `75690239f08f885ca9b0267580101f60d10fbe62`: the real parameter names are `dotnetInstallFlags`/`dotnetBuildFlags` and `selfContainedBuild`). Nix's `...` catch-all silently absorbs unrecognized attributes with no error, so both were complete no-ops — removing `-p:PublishSingleFile=true` from `extraPublishFlags` and rebuilding left the BuildHost DLL still missing, proving the flake flags were never wired into any `dotnet` invocation. The project file's own `<PublishSingleFile>true</PublishSingleFile>` was the actual, load-bearing setting all along, applying on every build (Nix or local) regardless of any CLI/Nix flag. This was reproduced with a plain `dotnet publish` outside Nix using the same effective flag, and disproven-then-confirmed by editing each file in turn and rebuilding.

The MCP error handler also currently swallows this underlying exception and returns a generic "An error occurred invoking 'search_solution'" message, so the real cause is only visible via `nugex --mcp`'s stderr.

## What Changes

- `NuGex/NuGex.fsproj` drops `<PublishSingleFile>true</PublishSingleFile>`, so `dotnet build`/`publish` leaves `BuildHost-netcore/` (and `BuildHost-net472/`) as intact loose-file directories, matching how working installs (e.g. the VS Code C# extension) ship this dependency.
- `flake.nix`'s `extraPublishFlags` also drops the same (dead) `-p:PublishSingleFile=true` entry as cleanup, since it never had any effect and would otherwise mislead future readers into thinking it's load-bearing. This is not the functional fix — the `.fsproj` edit is.
- No change to Nix's overall distribution shape: `$out/bin/nugex` remains a single wrapped entry point regardless of how many files live under `$out/lib/nugex/`.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `nix-flake-build`: the default package build must produce a working `MSBuildWorkspace` BuildHost alongside the binary, not just a binary that builds and links.

## Impact

- Affected files: `NuGex/NuGex.fsproj` (the functional fix), plus `flake.nix` (`extraPublishFlags`, dead-code cleanup only).
- Affected behavior: `search_solution` (and any other MSBuildWorkspace-based tool) becomes usable in Nix-built installs; currently it fails on every invocation.
- No API changes; this is a packaging/build-configuration fix.
