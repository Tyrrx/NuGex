## Why

`flake-utils` is effectively unmaintained (no releases since 2024, superseded in the Nix community by `flake-parts`), and its `eachDefaultSystem` helper pattern doesn't scale well if the flake ever needs per-system option overrides or additional modules. `flake-parts` is the actively maintained, more idiomatic replacement recommended by the NixOS wiki (https://wiki.nixos.org/wiki/Flake_Parts) for structuring multi-system flakes.

## What Changes

- **BREAKING**: Remove the `utils` (flake-utils) input from `flake.nix`.
- Add `flake-parts` as a flake input.
- Rewrite `flake.nix` to use `flake-parts.lib.mkFlake` with a `perSystem` block instead of `utils.lib.eachDefaultSystem`.
- Preserve existing `systems` list behavior (default systems) via flake-parts' `systems` option, matching flake-utils' default system set (or via the `flake-parts/systems` input flake-utils already used indirectly).
- Regenerate `flake.lock` to drop `flake-utils`/`systems` nodes tied to it and lock the new `flake-parts` input.
- No changes to package build logic (`buildDotnetModule` derivation), dev shell contents, or NuGex application behavior.

## Capabilities

### New Capabilities
- `nix-flake-build`: Defines the requirement that the repository's Nix flake exposes a buildable default package and dev shell, structured using `flake-parts` instead of `flake-utils`.

### Modified Capabilities
(none — no existing spec capability covers the Nix build tooling)

## Impact

- Affected files: `flake.nix`, `flake.lock`.
- Affected tooling: anyone using `nix build`, `nix develop`, or CI relying on the flake outputs (package name/attribute paths remain `packages.<system>.default` and `devShells.<system>.default`, unchanged).
- No changes to `NuGex/NuGex.fsproj`, `nix/deps.json`, or application source.
- Verification: `nix flake check` and `nix build .#default` must succeed after the change.
