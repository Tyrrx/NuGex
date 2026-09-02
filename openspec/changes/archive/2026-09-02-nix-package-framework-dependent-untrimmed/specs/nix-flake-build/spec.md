## MODIFIED Requirements

### Requirement: Default package builds successfully
The flake SHALL expose a `packages.<system>.default` output that builds the NuGex binary via `buildDotnetModule`, for at least `x86_64-linux`. The build SHALL be **framework-dependent** (not self-contained) and **untrimmed** (`PublishTrimmed` must not be set in the build/publish path).

#### Scenario: Building the default package succeeds
- **WHEN** `nix build .#default` is run on a supported system
- **THEN** the build completes successfully and produces a `result/bin/nugex` executable

#### Scenario: Published output is framework-dependent
- **WHEN** the built `nugex` binary is inspected
- **THEN** it is a framework-dependent .NET 10 application that requires the .NET 10 runtime at run time, and is not self-contained and not trimmed

#### Scenario: Published output is untrimmed
- **WHEN** the published output directory is inspected
- **THEN** it contains the full, untrimmed set of assemblies (no trimming was applied)

### Requirement: Roslyn-powered commands work on the built binary
The built `nugex` binary SHALL run `search-package` (and `search_solution` via MSBuildWorkspace) without trimming-related reflection errors. No trim-specific workarounds (`roots.xml`, `JsonSerializerIsReflectionEnabledByDefault`) shall be required for the binary to function.

#### Scenario: search-package works on the Nix-built binary
- **WHEN** the Nix-built `nugex` binary runs `search-package <id>` for a valid NuGet package
- **THEN** it downloads, indexes, and returns search results without throwing `MissingMethodException` or `Could not load type 'System.Object'`

#### Scenario: MCP server starts and lists tools on the Nix-built binary
- **WHEN** the Nix-built `nugex` binary runs with `--mcp` and receives `initialize` + `tools/list`
- **THEN** the server starts without reflection errors and returns all three tools (`search_package`, `search_solution`, `get_package_readme`)

### Requirement: BuildHost output is preserved
The default package build SHALL NOT set `PublishSingleFile` to `true`, so that `Microsoft.CodeAnalysis.Workspaces.MSBuild.BuildHost.dll` and its dependencies remain intact loose files under `BuildHost-netcore/` (and `BuildHost-net472/`) alongside the built binary.

#### Scenario: BuildHost DLL is present after build
- **WHEN** `nix build .#default` completes
- **THEN** `result/lib/nugex/BuildHost-netcore/Microsoft.CodeAnalysis.Workspaces.MSBuild.BuildHost.dll` exists on disk

#### Scenario: search_solution works against a real solution
- **WHEN** the built `nugex` binary runs as an MCP server in a working directory containing a `.sln` file, and `search_solution` is invoked
- **THEN** `MSBuildWorkspace` successfully opens the solution instead of throwing "The build host could not be found"