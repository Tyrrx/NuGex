## Why

The Nix-built `nugex` binary is currently published self-contained and trimmed (via `PublishTrimmed=true`), which silently breaks its core Roslyn pipeline: `search-package`/`search_solution` crash with `MissingMethodException` (Argu union-case reflection) and `Could not load type 'System.Object' from 'System.Runtime'` (Roslyn compilation core-assembly resolution). Trimming has proven fundamentally incompatible with NuGex's reflection-heavy Roslyn/Acgu usage across both distribution channels. Building the Nix package framework-dependent and untrimmed — matching the .NET tool channel — fixes these crashes and keeps the flake and tool binaries behaviorally identical.

## What Changes

- **BREAKING (Nix build output)**: The Nix `packages.default` output changes from a self-contained, trimmed binary to a **framework-dependent, untrimmed** binary (requires the .NET 10 runtime).
- `flake.nix`: remove `selfContainedBuild = true` (framework-dependent instead); ensure `PublishTrimmed` is not set anywhere in the publish path; keep `runtimeId = "linux-x64"` for the RID-bound build.
- `NuGex/NuGex.fsproj`: remove trim/self-contained-only properties `<PublishTrimmed>true</PublishTrimmed>`, `<SelfContained>true</SelfContained>` (already removed), `<SatelliteResourceLanguages>en</SatelliteResourceLanguages>`, `<DebugType>none</DebugType>`, the `<TrimmerRootDescriptor Include="roots.xml" />` item, and `JsonSerializerIsReflectionEnabledByDefault` (all trim-specific and inert when not trimming).
- `NuGex/roots.xml`: delete (no longer referenced).
- The Nix build's post-install wiring (`.dll` symlink to `nugex`) is unchanged; the output is a framework-dependent publish (runtime DLLs still present loose, not single-file — `PublishSingleFile` remains off).
- Both distribution channels (Nix flake + .NET tool) now produce framework-dependent, untrimmed, behaviorally-identical binaries.
- The MCP server and CLI (`search-package`, `search_solution`, `get_package_readme`) MUST run without trimming-related reflection errors.

## Capabilities

### New Capabilities
- *(none — no brand-new capability introduced; the flake build capability already exists)*

### Modified Capabilities
- `nix-flake-build`: The default package build changes from self-contained+trimmed to **framework-dependent+untrimmed**. Requirements change: published output must be a framework-dependent build requiring the .NET 10 runtime, must not set `PublishTrimmed`, and `roots.xml` is removed. The BuildHost (MSBuildWorkspace) requirement remains unchanged.

## Impact

- **Code**: `flake.nix` (remove `selfContainedBuild`/trim wiring), `NuGex/NuGex.fsproj` (remove trim/self-contained-only props), `NuGex/roots.xml` (delete).
- **Dependencies**: Nix build now depends on the .NET 10 runtime at run time (framework-dependent); no NuGet changes.
- **Behavior**: `result/bin/nugex` grows (untrimmed, e.g. ~100M+ vs 53M) and requires `dotnet` at run time, but `search-package`/`search_solution` work again. The tool channel is unaffected.
- **CI/release**: The `.NET tool` release pipeline already publishes framework-dependent/untrimmed and is unchanged. The existing `github-build-release-pipelines` change is unaffected.