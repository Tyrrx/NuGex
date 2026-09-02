## Context

NuGex is a .NET 10 F# project with two distribution concerns. Today the only channel is the Nix flake (`nix run github:Tyrrx/NuGex`), consumed from source. There is no CI. The fsproj currently bakes in Nix-specific publish properties (`<RuntimeIdentifiers>linux-x64</RuntimeIdentifiers>`, `<SelfContained>true</SelfContained>`, `<PublishTrimmed>true</PublishTrimmed>`) plus trim-support props (`roots.xml`, `JsonSerializerIsReflectionEnabledByDefault`). See proposal.md - Why.

An earlier archived change (`2026-07-06-fix-buildhost-single-file-publish`) established a critical constraint: the flake's `selfContained`/`extraPublishFlags` attributes are **inert** (not real `buildDotnetModule` params — the real ones are `selfContainedBuild` and `dotnetBuildFlags`/`dotnetInstallFlags`), silently absorbed by Nix's `...`. The load-bearing settings live in the fsproj. However, the flake's `runtimeId = "linux-x64"` **is** a real, working `buildDotnetModule` parameter that injects the RID at build time.

## Goals / Non-Goals

**Goals:**
- Add a build pipeline validating every `main` push (restore + build).
- Add a release pipeline triggered by GitHub Release creation that publishes NuGex as a .NET tool to nuget.org.
- Make the tool package framework-dependent and portable (any platform).
- Keep the Nix channel's trimmed, self-contained Linux build exactly as it is.
- Keep the change minimal — no per-RID binary matrix, no Nix-in-CI.

**Non-Goals:**
- Attaching Nix or any standalone binary artifacts to releases.
- Running the Nix build inside CI.
- Reconciling flake version (`0.2.0`) with release tags (accepted drift).

## Decisions

### D1: Self-contained moves to the flake; trim props and RID stay in the fsproj
Remove `<SelfContained>true</SelfContained>` from `NuGex.fsproj`; the flake owns self-contained via the real `selfContainedBuild = true` param (the old flake `selfContained = true` was inert). Keep `<RuntimeIdentifiers>linux-x64</RuntimeIdentifiers>`, `<PublishTrimmed>true</PublishTrimmed>`, `<SatelliteResourceLanguages>en</SatelliteResourceLanguages>`, `<DebugType>none</DebugType>`, `<TrimmerRootDescriptor Include="roots.xml" />`, and `JsonSerializerIsReflectionEnabledByDefault` in the fsproj.

The tool pack overrides the static props off: `dotnet pack -p:PackAsTool=true -p:SelfContained=false -p:PublishTrimmed=false -p:RuntimeIdentifiers=""`.

- **Rationale (empirical)**: `selfContainedBuild = true` (flake) + `PublishTrimmed=true` (fsproj static) produces a **53M / 119 DLLs** trimmed binary. This required adding **Argu to `roots.xml`**: the trimmer cuts `Argu.Utils.ShapeArgumentTemplate<T>`'s reflection path (instantiated at startup by `ArgumentParser.Create<Arguments>`), causing `MissingMethodException`. Rooting Argu preserves it; the binary then passes the MCP smoke test (`initialize` → `tools/list` returns all three tools).
- **Alternatives considered and rejected (empirically)**:
  - Moving `PublishTrimmed`/`SatelliteResourceLanguages`/`DebugType` to the flake via `dotnetInstallFlags`: **not trimmed** — `buildDotnetModule` publishes with `--no-build`, and command-line trim flags are silently ignored (114M untrimmed).
  - Flake-injected `Directory.Build.props`: **not trimmed** — the file is imported (verified) and `PublishTrimmed=true` evaluates, but the ILLink trimmer is never invoked under `--no-build` (106M).
  - Moving `RuntimeIdentifiers` out of the fsproj (leaving it open): **breaks `search_solution`/Nix reproducibility** — the RID must be a static project property for the build to be RID-aware; the flake's `runtimeId` alone doesn't cover all MSBuild paths. (Kept `RuntimeIdentifiers` static.)

### D2: Two pipelines, not one conditional workflow
Separate `build.yml` (push to `main`) and `release.yml` (on release creation). Keeps each trigger and job set obvious and independently debuggable.

### D3: Version from the release tag
Release pipeline extracts the version from the GitHub release tag (`vX.Y.Z` → `X.Y.Z`, stripping the leading `v`). Passed to `dotnet pack -p:Version=...`. Flake's hard-coded version drifts freely.
- **Rationale**: matches proposal decision "version from tag" and the user's "flake and tag may differ."

### D4: nuget.org as the registry
Publish to nuget.org (public, default `dotnet tool install` source) rather than GitHub Packages (needs a `nuget.config` source configured on the user side).
- **Alternatives considered**: GitHub Packages — rejected for install friction.

### D5: Tool packaging
`dotnet pack` with `<PackAsTool>true</PackAsTool>`. This can be set inline via `-p:PackAsTool=true` (keeps the fsproj unchanged apart from removing the RID) — simplest. A declared `PackAsTool` property in the fsproj is the alternative if we want the project to self-describe as a tool.

## Risks / Trade-offs

- **NuGet publish credentials** → Release workflow needs a `NUGET_API_KEY` secret (or a NuGet trusted publisher / OIDC setup). Without it, the publish step fails. Mitigation: store as a GitHub Actions secret named `NUGET_API_KEY`; document it in tasks. Trusted publisher (OIDC, no key) is the more secure long-term option.
- **Untrimmed tool is larger than the Nix binary** → Expected; framework-dependent tools don't trim. The tool relies on the user's .NET runtime. This is inherent to the chosen distribution model, not a regression.
- **The tool's `search_solution` path uses MSBuildWorkspace out-of-process** → In a framework-dependent (untrimmed) tool this behaves like a normal build — no trimming blind spot, so `roots.xml` is irrelevant here. Verified behavior only differs from Nix because Nix trims. Low risk.
- **`selfContainedBuild = true` changes the trimmer's behavior vs the fsproj static `<SelfContained>`** → It cuts Argu's `ShapeArgumentTemplate<T>` reflection path. Mitigation: Argu is rooted in `roots.xml` (empirically verified to restore startup). Any future reflection-heavy dependency added to the CLI may need a similar root.
- **Release pipeline publishes on every release** → Fine by design; each release is an explicit publish intent.

## Migration Plan

1. Update `NuGex.fsproj`: remove `<SelfContained>true</SelfContained>` (flake owns it via `selfContainedBuild`); keep `RuntimeIdentifiers`, `PublishTrimmed`, `SatelliteResourceLanguages`, `DebugType`, `roots.xml`, `JsonSerializerIsReflectionEnabledByDefault`. Update `flake.nix`: `selfContained = true` → `selfContainedBuild = true`. Add Argu to `roots.xml`. Verify `dotnet build` and `nix build` both succeed, and the trimmed binary passes the MCP smoke test (`initialize` → `tools/list`).
2. Add `build.yml`; push to `main`, confirm it runs.
3. Add `release.yml`; create a test release (or run `act`/manual dispatch) to confirm the pack and (dry-run) publish.
4. Configure `NUGET_API_KEY` secret; create the first real release.

## Open Questions

None — all decisions required to shape the approach and task breakdown have been resolved.
