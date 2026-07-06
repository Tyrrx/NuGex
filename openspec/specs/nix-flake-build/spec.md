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
