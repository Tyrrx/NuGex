## Context

See proposal.md - Why. The Nix-built `nugex` is currently self-contained and trimmed. Trimming breaks NuGex's Roslyn/Argu reflection paths (`search-package` crashes with `MissingMethodException` on Argu union-case metadata and `Could not load type 'System.Object' from 'System.Runtime'`). The .NET tool channel is already framework-dependent and untrimmed, and works. This change makes the Nix channel match the tool channel.

## Goals / Non-Goals

**Goals:**
- Make the Nix `packages.default` build framework-dependent and untrimmed.
- Remove all trim/self-contained-only configuration from `flake.nix` and `NuGex.fsproj`.
- Ensure `search-package`, `search_solution`, and the MCP server work on the Nix-built binary.
- Keep `PublishSingleFile` off (BuildHost must stay loose files).

**Non-Goals:**
- Changing the .NET tool release pipeline (already framework-dependent/untrimmed).
- Re-introducing trimming under any flag or mode (deemed incompatible with Roslyn/Argu).
- RID-cross-compilation for other platforms (linux-x64 only, as today).

## Decisions

### D1: Framework-dependent + untrimmed via `buildDotnetModule` defaults
Set the flake build to framework-dependent by NOT setting `selfContainedBuild = true` (its default is `false`), and ensure `PublishTrimmed` is not present in the fsproj or any publish flags. `buildDotnetModule` with `selfContainedBuild` unset publishes `--no-self-contained`, and with no `PublishTrimmed` flag the publish is untrimmed.
- **Rationale**: This is exactly the config the working .NET tool channel uses. It eliminates the entire class of trimming reflection bugs without workarounds.
- **Alternatives considered**: Keep trimming but add more `roots.xml` entries (Argu, NuGex, Roslyn core). Rejected: empirically insufficient — Roslyn's core-assembly resolution (`Could not load type 'System.Object'`) cannot be fixed by rooting app/Argu assemblies; trimming is fundamentally incompatible here.

### D2: Remove all trim/self-contained-only fsproj properties
Remove `<PublishTrimmed>true</PublishTrimmed>`, `<SatelliteResourceLanguages>en</SatelliteResourceLanguages>`, `<DebugType>none</DebugType>`, `<TrimmerRootDescriptor Include="roots.xml" />`, and `JsonSerializerIsReflectionEnabledByDefault` from `NuGex.fsproj`; delete `NuGex/roots.xml`. These are only meaningful when trimming/self-contained and are inert (or harmful) otherwise.
- **Rationale**: Keeps the project honest about not trimming; prevents accidental re-enablement.
- **Alternatives considered**: Leaving them inert. Rejected: dead config invites confusion and accidental trim re-enablement.

### D3: Keep `runtimeId = "linux-x64"` and `dotnet-runtime`
Keep `runtimeId = "linux-x64"` so the framework-dependent publish is RID-specific to linux-x64, and keep `dotnet-runtime` (the framework-dependent app still ships alongside the runtime DLLs; `dotnet-runtime` is the default runtime provider for `buildDotnetModule`).
- **Rationale**: Matches current behavior (linux-x64 only), keeps `postInstall` symlink logic unchanged.
- **Alternatives considered**: Dropping `runtimeId` for a fully portable (RID-less) publish. Rejected: `search_solution`'s MSBuildWorkspace path is verified against linux-x64; narrowing the change to framework-dependent-only reduces risk.

### D4: Keep `PublishSingleFile` off
`PublishSingleFile` remains off (never set), preserving `BuildHost-netcore/` loose files for MSBuildWorkspace. This requirement already exists in the `nix-flake-build` spec and is retained.

## Risks / Trade-offs

- **Nix binary grows significantly** (untrimmed self-contained was ~53M trimmed; framework-dependent untrimmed will be larger, roughly ~100M+ with runtime DLLs). → Mitigation: accepted trade-off; the binary now works. The tool channel is identical in size profile.
- **Framework-dependent Nix binary requires .NET 10 runtime at run time** → Mitigation: `buildDotnetModule` bundles `dotnet-runtime` in the closure and the wrapper provides it; matches how framework-dependent apps are expected to run on Nix.
- **The existing `github-build-release-pipelines` change's tool pack overrides** (`-p:SelfContained=false -p:PublishTrimmed=false`) become redundant-but-harmless once the fsproj no longer sets those props. → Mitigation: leave them; they're defensive and idempotent. No change needed to that workflow.

## Migration Plan

1. Edit `flake.nix`: remove `selfContainedBuild = true`.
2. Edit `NuGex.fsproj`: remove trim/self-contained-only props and the `TrimmerRootDescriptor` item.
3. Delete `NuGex/roots.xml`.
4. Verify: `nix flake check`, `nix build`, then run `result/bin/nugex search-package <id>`, `result/bin/nugex --mcp` (initialize + tools/list), and confirm BuildHost DLL exists.
5. Rollback: revert the three file changes (git).

## Open Questions

None.