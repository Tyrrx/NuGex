## 1. Nix flake change

- [x] 1.1 Remove `selfContainedBuild = true` from `flake.nix` so the build is framework-dependent; keep `runtimeId = "linux-x64"` and `dotnet-runtime`; verify `nix flake check` passes
- [x] 1.2 Update/remove the flake comment about self-contained being flake-owned; verify `nix build` succeeds and produces a framework-dependent publish

## 2. fsproj cleanup

- [x] 2.1 Remove `<PublishTrimmed>true</PublishTrimmed>`, `<SatelliteResourceLanguages>en</SatelliteResourceLanguages>`, `<DebugType>none</DebugType>`, `JsonSerializerIsReflectionEnabledByDefault`, and the `<TrimmerRootDescriptor Include="roots.xml" />` item from `NuGex/NuGex.fsproj`; verify `dotnet build` succeeds
- [x] 2.2 Delete `NuGex/roots.xml` and verify nothing references it (`grep` for `roots.xml` returns only git history)

## 3. Functional verification

- [x] 3.1 Run `nix build`, confirm `result/bin/nugex` exists, and confirm the output is framework-dependent (no trimmed/single-file: `BuildHost-netcore/...BuildHost.dll` present, runtime DLLs loose)
- [x] 3.2 Run `result/bin/nugex search-package FunicularSwitch -q Result` and verify it returns search results without `MissingMethodException` or `Could not load type 'System.Object'`
- [x] 3.3 Run the MCP smoke test (`initialize` → `tools/list`) against `result/bin/nugex --mcp` and verify all three tools are listed
- [x] 3.4 Verify `search_solution` still works: run the MCP server in a directory with a `.sln`/`.slnx` and confirm no "build host could not be found" error

## 4. Release pipeline sanity

- [x] 4.1 Confirm the `.NET tool` release pipeline (`github-build-release-pipelines`) is unaffected — its pack overrides (`-p:SelfContained=false -p:PublishTrimmed=false`) remain idempotent with the cleaned fsproj; verify `dotnet pack -p:PackAsTool=true` still produces a valid tool package