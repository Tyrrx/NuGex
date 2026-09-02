## Why

NuGex currently has no CI/CD. Every change is verified only by local `dotnet build`, and the only distribution channel is the Nix flake consumed from source (`nix run github:Tyrrx/NuGex`). There is no automated validation on push to `main` and no way for non-Nix users (Windows/macOS/Linux without Nix) to install NuGex. A GitHub Actions pipeline closes both gaps: a build pipeline that validates every `main` push, and a release pipeline that publishes NuGex as a .NET tool to nuget.org.

## What Changes

- Add a **build pipeline** (GitHub Actions) that runs on push to `main` only: `dotnet restore`, `dotnet build`, and a smoke test.
- Add a **release pipeline** (GitHub Actions) that runs when a GitHub Release is created: builds, packs NuGex as a .NET tool package, and publishes it to nuget.org.
- Change `NuGex.fsproj` to remove `<RuntimeIdentifiers>linux-x64</RuntimeIdentifiers>`, `<SelfContained>true</SelfContained>`, and `<PublishTrimmed>true</PublishTrimmed>`. The Nix flake owns the RID, self-contained, and trimming at build time (`runtimeId = "linux-x64"` is already set; `selfContained` is fixed to the real `selfContainedBuild` param; trimming is added via `dotnetBuildFlags`). The fsproj keeps the trim-support wiring (`roots.xml`, `JsonSerializerIsReflectionEnabledByDefault`), which is inert in the untrimmed tool build and picked up automatically by the flake's trimmed build.
- The tool package is **framework-dependent** (runs on any platform with a .NET runtime), untrimmed, and platform-neutral by default — no pack-time overrides needed because `SelfContained`/`PublishTrimmed`/`RuntimeIdentifiers` no longer exist in the fsproj.
- No Nix binary is attached to releases; Nix users continue to consume from source. No per-RID binary matrix is produced.
- Release version is derived from the release tag (`vX.Y.Z`). Tag version may drift from the flake's hard-coded version — acceptable, no reconciliation.

## Capabilities

### New Capabilities
- `github-ci-pipelines`: Automated build validation on push to `main`, and release automation that publishes NuGex as a .NET tool to nuget.org.

### Modified Capabilities
- `nix-flake-build`: The Nix build is functionally unchanged, but `SelfContained`/`PublishTrimmed`/`RuntimeIdentifiers` move from the fsproj into the flake (fixing the inert `selfContained` attr to `selfContainedBuild` and adding trim via `dotnetBuildFlags`). No requirement change; kept for reference only. No spec delta required.

## Impact

- **Code**: `NuGex/NuGex.fsproj` — remove `<RuntimeIdentifiers>`, `<SelfContained>`, `<PublishTrimmed>`; keep `roots.xml` and `JsonSerializerIsReflectionEnabledByDefault`. `flake.nix` — fix `selfContained` → `selfContainedBuild`, add trim flags via `dotnetBuildFlags`.
- **New files**: `.github/workflows/build.yml`, `.github/workflows/release.yml` (and any shared workflow or scripts).
- **Dependencies**: GitHub Actions infrastructure (no runtime code changes to NuGex itself). Publishing to nuget.org requires an `NUGET_API_KEY` (or trusted publisher) secret.
- **Distribution**: Adds a new `.NET tool` install channel alongside the existing Nix flake channel.
