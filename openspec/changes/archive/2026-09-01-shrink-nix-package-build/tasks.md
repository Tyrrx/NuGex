## 1. Project configuration

- [x] 1.1 In `NuGex/NuGex.fsproj`, fix `RuntimeIdentifiers` from `linux-musl-x64` to `linux-x64`, and add `PublishTrimmed=true`, `SatelliteResourceLanguages=en`, `DebugType=none`; verify `dotnet build` still succeeds with no new warnings.
- [x] 1.2 Add `NuGex/roots.xml` rooting `Microsoft.CodeAnalysis.CSharp.Workspaces` and `Microsoft.CodeAnalysis.Workspaces.MSBuild` (`<type fullname="*"/>` each), and wire it via `<TrimmerRootDescriptor Include="roots.xml" />` in `NuGex.fsproj`; verify the file is picked up (no MSBuild warning about a missing/unused item).

## 2. Nix build

- [x] 2.1 Update `flake.nix` if any publish flags still need setting there (most now live in the fsproj); verify `nix build .#default.passthru.fetch-deps` still evaluates.
- [x] 2.2 Regenerate `nix/deps.json` via the fetch-deps script and verify `nix build .#default` succeeds.

## 3. Verify the shrink

- [x] 3.1 Compare `du -sh` and DLL count of the nix build's `result/lib/nugex` before and after; verify no `cs/`, `de/`, `es/`, `fr/`, `it/`, `ja/`, `ko/`, etc. satellite-language folders remain, and no `.pdb` files are present. (114M/231 dlls → 28M/59 dlls; 0 satellite folders; 0 .pdb files)
- [x] 3.2 Verify `Microsoft.CodeAnalysis.CSharp.Workspaces.dll` and `Microsoft.CodeAnalysis.Workspaces.MSBuild.dll` are still present in the output.

## 4. Verify nothing broke

- [x] 4.1 Run the stdio MCP smoke test (`initialize` → `tools/list` → `tools/call search_package`) against `result/bin/nugex --mcp`, same as the ModelContextProtocol migration's smoke test, and verify all three tools list and `search_package` round-trips. (Found and fixed a real regression: trimming disables System.Text.Json's reflection fallback, which the MCP SDK needs for NuGex's untyped `obj`/`obj[]` tool return types — crashed server startup. Fixed with `JsonSerializerIsReflectionEnabledByDefault=true`; see design.md - Findings During Implementation.)
- [x] 4.2 Create a scratch solution with one `.csproj` and one `.cs` file (outside the repo), point `search_solution` at it via the built binary, and verify it indexes and returns results — this is the one path the trimming change can silently break and this repo's own (F#-only) solution can't exercise. (Confirmed by comparison, not raw output: `search_solution` indexes only `compilation.References`, not the project's own types — see design.md - Findings During Implementation. The trimmed build's output against the fixture's `.csproj` is byte-identical to an untrimmed baseline build, proving MSBuildWorkspace's C#-project loading — the path the root descriptor protects — still works.)
