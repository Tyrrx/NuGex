## 1. Update flake inputs

- [x] 1.1 Remove the `utils` (flake-utils) input from `flake.nix`.
- [x] 1.2 Add `flake-parts` input (`github:hercules-ci/flake-parts`) with `inputs.nixpkgs-lib.follows = "nixpkgs"`.
- [x] 1.3 Add an explicit `systems` input (`github:nix-systems/default`) for the default system list.

## 2. Rewrite outputs with flake-parts

- [x] 2.1 Replace `utils.lib.eachDefaultSystem (system: ...)` with `flake-parts.lib.mkFlake { inherit inputs; } { systems = import inputs.systems; perSystem = { system, ... }: ...; }`.
- [x] 2.2 Move the existing `pkgs`, `dotnet-sdk`, `pname`, `version` `let` bindings and the `packages.default` / `devShells.default` bodies into the `perSystem` block unchanged.

## 3. Regenerate lock file and verify build

- [x] 3.1 Run `nix flake lock` to regenerate `flake.lock`, confirming `flake-utils` and its transitive `systems` node are removed and `flake-parts` (and its transitive inputs) are added.
- [x] 3.2 Run `nix flake check` and confirm it passes.
- [x] 3.3 Run `nix build .#default` and confirm the build succeeds and `result/bin/nugex` exists.
- [x] 3.4 Run `nix flake show` (or `nix develop`) and confirm `devShells.<system>.default` still resolves.
