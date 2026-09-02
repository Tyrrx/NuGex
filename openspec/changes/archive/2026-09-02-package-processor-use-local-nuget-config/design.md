## Context

`PackageProcessor` currently binds a single hardcoded `SourceRepository` to `https://api.nuget.org/v3/index.json` (PackageProcessor.fs:24). Four functions flow through it: `getLatestVersion` (via `MetadataResource`), `downloadPackage` (via `FindPackageByIdResource`), and transitively `processPackage` and `getPackageReadme`. The project already depends on `NuGet.Protocol`/`NuGet.Packaging` 7.3.0, which bring `NuGet.Configuration` transitively — no new dependency is needed.

## Goals / Non-Goals

**Goals:**
- Load configured package sources and their credentials from the user's local NuGet config.
- Use those sources for version resolution and package download.
- Fall back across sources so a package on any configured feed is reachable.

**Non-Goals:**
- No per-call override of sources (no new CLI/MCP args).
- No source caching/multi-versioned config beyond what NuGet's settings machinery gives by default.
- No change to framework selection, search indexing, or the MCP protocol.

## Decisions

**1. Use `NuGet.Configuration` settings machinery instead of parsing XML manually.**
`Settings.LoadDefaultSettings(null)` reads the global, machine, and user `NuGet.Config` files with the same precedence NuGet uses. A `PackageSourceProvider` over that settings instance yields `PackageSource` values that already carry resolved credentials (including decrypted `encryptedpassword` entries). Rolling our own XML parser would duplicate this and likely miss credential handling.

**2. Build one `SourceRepository` per source; try them in order.**
```fsharp
let private settings = Settings.LoadDefaultSettings(null)
let private sources =
    PackageSourceProvider(settings).LoadPackageSources()
    |> Seq.map (fun s -> Repository.Factory.GetCoreV3(s.Source))
    |> Seq.toArray
```
`Repository.Factory.GetCoreV3(source)` creates a `SourceRepository` whose `PackageSource.Credentials` flow into `GetResourceAsync<...>`, so authenticated feeds work without extra code. `downloadPackage`/`getLatestVersion` iterate the array, catching per-source failures and continuing to the next. If none succeed, return `None` (not-found) — matching the existing graceful path.

Alternatives considered:
- Keep only the first enabled source: simpler but breaks the "package on a later feed" requirement.
- Use `Repository.Factory.GetCoreV3` only after filtering disabled sources — `LoadPackageSources()` already returns only enabled sources, so no extra filter needed.

**3. If no sources load (empty config edge case), fall back to nuget.org.**
`LoadPackageSources()` normally includes the default nuget.org source even with no user config. As a safety net, if the array is empty we seed it with the current hardcoded nuget.org URL so existing behavior never regresses to "no source at all".

## Risks / Trade-offs

- [Unreachable/unauthenticated source slows resolution] → Each source is tried in order with a per-source try/catch; failure on one moves to the next rather than aborting.
- [Credential prompts on some source types] → `PackageSourceProvider` resolves stored credentials only; it does not interactively prompt. Any source without stored credentials simply fails auth and is skipped.
- [Behavior difference from hardcoded nuget.org when user has exotic config] → Expected and desired; nuget.org remains the fallback when no other source works.
