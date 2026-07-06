## 1. Build configuration fix

- [x] 1.1 In `NuGex/NuGex.fsproj`, remove `<PublishSingleFile>true</PublishSingleFile>` from the `PropertyGroup` — this is the functional fix. (Corrected during implementation: the originally planned `flake.nix` `extraPublishFlags` edit turned out to be dead code — `extraPublishFlags`/`selfContained` aren't real `buildDotnetModule` parameters, so they were silently ignored. Confirmed by removing the flag there first and rebuilding: the DLL was still missing.)
- [x] 1.2 In `flake.nix`, remove the now-redundant `-p:PublishSingleFile=true` entry from `extraPublishFlags` as cleanup (it was already inert, but leaving it in place misleadingly suggests it's load-bearing).

## 2. Verification

- [x] 2.1 Run `nix build .#default` and confirm `result/lib/nugex/BuildHost-netcore/Microsoft.CodeAnalysis.Workspaces.MSBuild.BuildHost.dll` exists (not just its `.deps.json`/`.runtimeconfig.json`) — confirmed present at 440KB alongside its two dependency DLLs.
- [x] 2.2 Run the built `result/bin/nugex search-solution <path>` against a real solution and confirm it indexes successfully instead of failing with "The build host could not be found." Verified via CLI (`nugex search-solution`) rather than the MCP stdio protocol, since it exercises the same `MSBuildWorkspace.OpenSolutionAsync` path: a wrapping `.slnx` around a minimal C# class library indexed successfully and returned real symbol matches. (A same-repo F# `.fsproj`/`.slnx` test also got past the BuildHost stage but hit a separate, unrelated "extension not associated with a language" limitation for standalone F# project opening — out of scope for this fix.)
- [x] 2.3 Confirm `result/bin/nugex` still exists and is executable (the `postInstall` symlink logic is unaffected by this flag change) — confirmed via `nugex --help`.
