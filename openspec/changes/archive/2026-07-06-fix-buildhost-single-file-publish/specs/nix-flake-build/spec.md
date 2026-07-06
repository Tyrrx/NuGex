## ADDED Requirements

### Requirement: Published output preserves the MSBuildWorkspace BuildHost
The default package build SHALL NOT set `PublishSingleFile` to `true` (whether via `NuGex.fsproj` project properties or Nix/CLI publish flags), so that `Microsoft.CodeAnalysis.Workspaces.MSBuild.BuildHost.dll` and its dependencies remain intact loose files under `BuildHost-netcore/` (and `BuildHost-net472/`) alongside the built binary, rather than being bundled into a single-file executable and disappearing from disk.

#### Scenario: BuildHost DLL is present after build
- **WHEN** `nix build .#default` completes
- **THEN** `result/lib/nugex/BuildHost-netcore/Microsoft.CodeAnalysis.Workspaces.MSBuild.BuildHost.dll` exists on disk (not just its `.deps.json`/`.runtimeconfig.json` companions)

#### Scenario: search_solution works against a real solution
- **WHEN** the built `nugex` binary runs as an MCP server in a working directory containing a `.sln` file, and `search_solution` is invoked
- **THEN** `MSBuildWorkspace` successfully opens the solution instead of throwing "The build host could not be found"
