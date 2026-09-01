## Why

The nix-built `nugex` package is a plain self-contained publish with no trimming: 114M of DLLs,
including ~15 satellite-resource-language folders (`cs`, `de`, `es`, `fr`, `it`, `ja`, `ko`, ...)
for a tool that has no localized strings of its own, plus the full .NET BCL (`System.Net.*`,
`System.Security.Cryptography.*`, `System.ServiceModel.Web.dll`, `WindowsBase.dll`, ...) whether
or not `nugex` ever touches it. This was never tightened because trimming was assumed unsafe:
`Microsoft.CodeAnalysis.Workspaces.MSBuild` and Roslyn's C# language support are discovered via
MEF at runtime (by scanning DLLs in the output folder), not by any direct call site — so the
default trimmer silently deletes `Microsoft.CodeAnalysis.CSharp.Workspaces.dll` with no warning,
which would quietly break `search_solution` for any C# project. A spike (see design.md) confirmed
this exact failure and found the fix: an ILLink root descriptor that explicitly protects the small
set of MEF-discovered assemblies, leaving the trimmer free to cut everything else.

## What Changes

- Enable `PublishTrimmed=true` (default `TrimMode=partial`) in the nix-built package, protecting
  `Microsoft.CodeAnalysis.CSharp.Workspaces` and `Microsoft.CodeAnalysis.Workspaces.MSBuild` via an
  ILLink root descriptor XML so MSBuildWorkspace's MEF-discovered C# support is never dropped.
- Set `SatelliteResourceLanguages` to exclude all non-English satellite resource assemblies from
  the published output (nugex has no localized strings; the satellites are 100% dead weight).
- Strip debug symbols from the published output (`DebugType=none`) — not needed in a distributed
  binary.
- Fix the stale `RuntimeIdentifiers` in `NuGex.fsproj` (`linux-musl-x64`, unused since the flake
  switched to glibc long ago) to `linux-x64`, matching what the nix build actually produces.
- Regenerate `nix/deps.json` for whatever the trimmer/publish step needs.

No change to CLI behavior, MCP tool surface, or supported inputs — this only changes what ships in
the published binary's footprint.

## Capabilities

### New Capabilities
None.

### Modified Capabilities
None — this only changes build/publish configuration, not observable tool behavior
(`skip_specs: true`).

## Impact

- `flake.nix`: add trim/publish flags (`PublishTrimmed`, `SatelliteResourceLanguages`, `DebugType`)
  and reference the new root descriptor file.
- `NuGex/NuGex.fsproj`: fix `RuntimeIdentifiers`; may need `TrimmerRootDescriptor` wiring if kept
  in-project rather than passed via `extraPublishFlags`.
- New file: an ILLink root descriptor (e.g. `NuGex/roots.xml`) rooting the MEF-discovered
  Roslyn/MSBuild assemblies.
- `nix/deps.json`: regenerated via `fetch-deps` for the new publish configuration.
- Risk surface: `search_solution` against a real C#-project solution is the one path this change
  can silently break if the root descriptor misses an assembly MSBuildWorkspace needs — this repo
  is F#-only, so that path can't be self-tested and needs a manual check against an external
  solution with `.csproj` files (see tasks.md).
