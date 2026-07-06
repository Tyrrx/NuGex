## Context

`flake.nix` currently uses `flake-utils.lib.eachDefaultSystem` to generate per-system `packages.<system>.default` and `devShells.<system>.default` outputs from a single `system:` function. `flake-utils` is unmaintained upstream; `flake-parts` (https://wiki.nixos.org/wiki/Flake_Parts) is the actively maintained, module-based replacement recommended for new flakes. The migration only needs to touch the flake plumbing — the `buildDotnetModule` derivation and dev shell contents are unaffected.

## Goals / Non-Goals

**Goals:**
- Replace `flake-utils` with `flake-parts` as the multi-system flake framework.
- Keep the same public output attribute paths: `packages.<system>.default`, `devShells.<system>.default`.
- Keep the same default systems flake-utils previously targeted.
- Confirm `nix build .#default` and `nix flake check` succeed after the change.

**Non-Goals:**
- No change to the .NET build (`buildDotnetModule`, `nix/deps.json`, publish flags).
- No change to dev shell packages or shell hook behavior.
- No introduction of flake-parts modules beyond the minimal `perSystem` block (no `flake-parts-modules` ecosystem features like `easyOverlay`, `mkTransitionPackage`, etc. — YAGNI for this single-package flake).

## Decisions

- **Use `flake-parts.lib.mkFlake { inherit inputs; } { ... }` with a single `perSystem` block.**
  Alternative considered: keep manual `eachSystem`-style attribute-merging without flake-parts. Rejected — that's re-implementing what flake-parts already provides, and defeats the purpose of the migration.

- **Systems list: reuse the existing `nix-systems/default` input explicitly (`inputs.systems.url = "github:nix-systems/default"`, `systems = import inputs.systems;`).**
  `flake.lock` already pulls this repo transitively (as flake-utils' own `systems` input) — making it a direct input keeps the exact same default system set (`x86_64-linux`, `aarch64-linux`, `x86_64-darwin`, `aarch64-darwin`) with no behavior change. Alternative considered: hardcode `systems = [ "x86_64-linux" ]` since the package is Linux-only (`meta.platforms = platforms.linux`) — rejected as an unrelated scope creep; this change should be a mechanical framework swap, not a platform-support change.

- **Keep `pkgs = import nixpkgs { inherit system; }` inside `perSystem`, computed manually rather than via `flake-parts` nixpkgs module (`inputs.flake-parts.flakeModules.*`).**
  flake-parts ships an optional module for injecting `pkgs` globally, but adding it is an unrequested abstraction for a flake with one `perSystem` block. Plain `import nixpkgs { inherit system; }` is the same one-liner the current flake already uses.

- **Pin `flake-parts` input with `inputs.flake-parts.inputs.nixpkgs-lib.follows = "nixpkgs"`.**
  Avoids flake-parts fetching its own pinned copy of `nixpkgs-lib` as a separate lock node; standard practice per the flake-parts docs.

## Risks / Trade-offs

- [Regenerating `flake.lock` could pick up an unrelated `nixpkgs` update if `nix flake lock` is run broadly] → Mitigation: only update/add the `flake-parts` (and its `systems`/`nixpkgs-lib` follows) inputs; leave the existing `nixpkgs` pin untouched, then verify with `nix build`.
- [Attribute path drift if `perSystem` output names don't match flake-utils' generated shape exactly] → Mitigation: verify with `nix flake show` that `packages.<system>.default` and `devShells.<system>.default` still resolve, plus `nix build .#default`.

## Migration Plan

1. Edit `flake.nix`: replace `utils` input with `flake-parts` (+ explicit `systems` input), rewrite `outputs` to use `flake-parts.lib.mkFlake`.
2. Run `nix flake lock` to regenerate `flake.lock` (drops the old `utils`/`systems` nodes tied to flake-utils, adds `flake-parts` and its own transitive inputs).
3. Run `nix flake check` and `nix build .#default` to confirm the package still builds.
4. Run `nix develop` (or at least `nix flake show`) to confirm the dev shell output still resolves.

Rollback: revert `flake.nix` and `flake.lock` via git if the build fails and the fix isn't quick.

## Open Questions

None — this is a scoped, mechanical framework swap with no behavioral ambiguity.
