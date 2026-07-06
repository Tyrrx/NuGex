### Requirement: Flake builds via flake-parts
The repository's `flake.nix` SHALL use `flake-parts` to structure its per-system outputs. The flake SHALL NOT depend on `flake-utils`.

#### Scenario: flake-parts is the only multi-system framework input
- **WHEN** `flake.nix` inputs are inspected
- **THEN** `flake-parts` is present as an input and `flake-utils` (aliased `utils`) is absent

### Requirement: Default package builds successfully
The flake SHALL expose a `packages.<system>.default` output that builds the NuGex binary via `buildDotnetModule`, for at least `x86_64-linux`.

#### Scenario: Building the default package succeeds
- **WHEN** `nix build .#default` is run on a supported system
- **THEN** the build completes successfully and produces a `result/bin/nugex` executable

### Requirement: Dev shell remains available
The flake SHALL expose a `devShells.<system>.default` output providing the .NET 10 SDK, `fsautocomplete`, and `fantomas`.

#### Scenario: Entering the dev shell succeeds
- **WHEN** `nix develop` is run on a supported system
- **THEN** the shell activates without error and `dotnet`, `fsautocomplete`, and `fantomas` are available on `PATH`

### Requirement: Published output preserves the MSBuildWorkspace BuildHost
The default package build SHALL NOT set `PublishSingleFile` to `true` (whether via `NuGex.fsproj` project properties or Nix/CLI publish flags), so that `Microsoft.CodeAnalysis.Workspaces.MSBuild.BuildHost.dll` and its dependencies remain intact loose files under `BuildHost-netcore/` (and `BuildHost-net472/`) alongside the built binary, rather than being bundled into a single-file executable and disappearing from disk.

#### Scenario: BuildHost DLL is present after build
- **WHEN** `nix build .#default` completes
- **THEN** `result/lib/nugex/BuildHost-netcore/Microsoft.CodeAnalysis.Workspaces.MSBuild.BuildHost.dll` exists on disk (not just its `.deps.json`/`.runtimeconfig.json` companions)

#### Scenario: search_solution works against a real solution
- **WHEN** the built `nugex` binary runs as an MCP server in a working directory containing a `.sln` file, and `search_solution` is invoked
- **THEN** `MSBuildWorkspace` successfully opens the solution instead of throwing "The build host could not be found"
