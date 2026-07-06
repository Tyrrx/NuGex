## Context

`dotnet build`/`publish` with `PublishSingleFile=true` bundles the app's own assembly and its managed dependencies into a single executable blob; assemblies that are meant to be loaded from a physical path at runtime by a separate process — as Roslyn's `Microsoft.CodeAnalysis.Workspaces.MSBuild.BuildHost.dll` is — get swept into that bundle instead of staying as loose files under `BuildHost-netcore/`. Only the `.deps.json`/`.runtimeconfig.json` companions (which single-file publish always emits alongside satellite runtime configs) survive on disk. `MSBuildWorkspace.OpenSolutionAsync` then fails every time with "The build host could not be found," because it `Process.Start`s a dotnet host pointed at that now-missing DLL path.

This was confirmed independent of Nix: running `dotnet publish NuGex/NuGex.fsproj -r linux-x64 --self-contained true -p:PublishSingleFile=true ...` directly reproduces the missing DLL; the same command with `PublishSingleFile=false` produces an intact `BuildHost-netcore/` directory (450KB `BuildHost.dll` plus its two dependency DLLs).

**Where the flag actually lives (found during implementation, corrected from the original plan below).** `flake.nix`'s `packages.default` derivation passes `selfContained = true` and `extraPublishFlags = [ "-p:PublishSingleFile=true" ... ]` to `buildDotnetModule`. Neither attribute name matches `buildDotnetModule`'s real parameters — checked against this repo's exact pinned nixpkgs revision (`75690239f08f885ca9b0267580101f60d10fbe62`, from `flake.lock`): the real names are `selfContainedBuild` and `dotnetInstallFlags`/`dotnetBuildFlags`. `buildDotnetModule`'s argument function ends in `...`, so Nix silently absorbs unrecognized attributes into the derivation's environment with no error — they're never read by `dotnet-build-hook.sh` or `dotnet-install-hook.sh`. Proof: removing `-p:PublishSingleFile=true` from `extraPublishFlags` and rebuilding via `nix build .#default` left `BuildHost-netcore/` still missing the `.dll`. The actual, load-bearing setting is `<PublishSingleFile>true</PublishSingleFile>` in `NuGex/NuGex.fsproj`'s `PropertyGroup` — an MSBuild project property applies on every build regardless of which CLI/Nix flags are (or aren't) passed. Removing it there and rebuilding produced an intact `BuildHost-netcore/` (DLL present, 440KB) and a working `search_solution` end-to-end against a real C# solution.

## Goals / Non-Goals

**Goals:**
- The Nix-built `nugex` package's `MSBuildWorkspace`-backed tools (`search_solution`) work correctly against a real solution, not just "the build succeeds."
- The fix is a build-configuration change, not a workaround that copies files around after the fact.

**Non-Goals:**
- No change to `search_solution`'s tool surface or to `SolutionProcessor`/`Mcp.fs` source.
- No attempt to preserve single-file distribution by selectively excluding assemblies from the bundle (see Decisions below for why this was rejected).
- Not addressing the MCP error handler swallowing exception detail (noted during investigation as a separate, secondary rough edge — worth its own follow-up, out of scope here).

## Decisions

- **Fix the `.fsproj` property, not the flake attributes.** The flake's `selfContained`/`extraPublishFlags` are inert (wrong names, silently ignored), so editing them alone cannot fix anything — verified by doing exactly that first and observing no change on rebuild. The `.fsproj`'s `<PublishSingleFile>true</PublishSingleFile>` is the only place this setting is actually read.
- **Drop `PublishSingleFile` entirely, rather than exclude specific assemblies from the bundle.** MSBuild does support per-item `<ExcludeFromSingleFile>true</ExcludeFromSingleFile>` metadata to keep specific assemblies as loose files while still bundling everything else. This was considered and rejected: the set of assemblies Roslyn's BuildHost needs (`BuildHost.dll` itself plus its own transitive dependencies — currently `Microsoft.Build.Locator.dll` and `System.Collections.Immutable.dll`, but not pinned by any contract) can change across Roslyn/SDK versions, silently reintroducing this exact failure on a future upgrade with no compile-time signal. Disabling single-file entirely removes the whole failure class permanently rather than chasing an assembly list.
- **Clean up the flake's dead `extraPublishFlags`/`-p:PublishSingleFile=true` entry too, but don't rename it to the real parameter (`dotnetInstallFlags`).** Once the `.fsproj` no longer sets `PublishSingleFile`, there's nothing left needing that flag from either side, so removing the dead reference (rather than "fixing" it into a working flag with the same value) avoids reintroducing the exact bundling behavior via a second path. The other dead attribute, `selfContained = true`, is left as-is out of scope — the build already produces a correct self-contained, RID-specific output via the `.fsproj`'s own `<SelfContained>true</SelfContained>`/`<RuntimeIdentifiers>`, so that particular no-op attribute isn't causing any observed defect; renaming it is a separate, non-blocking cleanup.
- **No user-facing distribution benefit is being given up.** Nix already produces a single stable entry point (`$out/bin/nugex`, a symlink into `$out/lib/nugex/NuGex`) regardless of how many files live in `$out/lib/nugex/`.
- **Keep `PublishTrimmed=false` and `InvariantGlobalization=true` unchanged.** Neither is implicated in the reproduction; changing them is out of scope.

## Risks / Trade-offs

- [Removing `PublishSingleFile` increases the number of files under `$out/lib/nugex/` (typical self-contained non-single-file publish output for a Roslyn-using app)] → Mitigation: this is the normal, supported shape for .NET apps that host out-of-process satellite assemblies; Nix store paths are already directory trees, so this has no practical cost for a CLI/MCP-server package.
- [The flake still has one other dead attribute (`selfContained`, should be `selfContainedBuild`) after this change] → Mitigation: confirmed non-blocking — the `.fsproj` already drives correct self-contained output on its own — and left as a known, separately-trackable cleanup rather than bundled into this fix.
- [If a future dependency upgrade reintroduces a similar out-of-process-DLL pattern, single-file publish could break it again in the same way] → Mitigation: out of scope for this change, but the reproduction steps in this design (plain `dotnet publish` with/without the flag) are a fast, repo-independent way to re-diagnose this exact failure mode if it recurs.

## Migration Plan

- Edit `NuGex/NuGex.fsproj` to drop `<PublishSingleFile>true</PublishSingleFile>` (the functional fix).
- Edit `flake.nix` to drop the now-redundant, always-dead `-p:PublishSingleFile=true` entry from `extraPublishFlags` (cleanup).
- Rebuild via `nix build .#default` and verify `result/lib/nugex/BuildHost-netcore/Microsoft.CodeAnalysis.Workspaces.MSBuild.BuildHost.dll` exists, then run `search_solution` against a real solution to confirm it works end-to-end.
- No rollback complexity: reverting the `.fsproj` line restores prior (broken) behavior if ever needed.

## Open Questions

None.
