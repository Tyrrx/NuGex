## 1. Project file change

- [x] 1.1 Remove `<SelfContained>true</SelfContained>` from `NuGex/NuGex.fsproj` (flake owns it now); keep `<RuntimeIdentifiers>linux-x64</RuntimeIdentifiers>`, `<PublishTrimmed>true</PublishTrimmed>`, `SatelliteResourceLanguages`, `DebugType`, `TrimmerRootDescriptor`/`roots.xml`, `JsonSerializerIsReflectionEnabledByDefault`; verify `dotnet build` succeeds with no new warnings
- [x] 1.2 Update `flake.nix`: `selfContained = true` → `selfContainedBuild = true` (the real `buildDotnetModule` param; the old attr was inert); keep `runtimeId = "linux-x64"`; verify the flake evaluates (`nix flake check`)
- [x] 1.3 Verify the Nix build produces a trimmed, self-contained, working `linux-x64` binary: `nix build`, confirm `result/bin/nugex` exists (53M / 119 DLLs), and run the MCP smoke test (`initialize` → `tools/list` returns all three tools) — this required adding **Argu to `roots.xml`** to fix the `MissingMethodException` the trimmer caused on `ShapeArgumentTemplate<T>`

## 2. Build pipeline

- [x] 2.1 Create `.github/workflows/build.yml` with a `push` trigger filtered to `branches: [main]` and verify the workflow file is valid YAML
- [ ] 2.2 Add a job that runs `dotnet restore` and `dotnet build` (Release configuration) and verify a push to `main` triggers the workflow successfully — **requires a GitHub push to `main`**; command locally verified (`dotnet build -c Release --no-restore` succeeds)

## 3. Release pipeline

- [x] 3.1 Create `.github/workflows/release.yml` with an `on: release` trigger and verify the workflow file is valid YAML
- [x] 3.2 Add a job that checks out, runs `dotnet restore` and `dotnet build`, and verifies the build succeeds
- [x] 3.3 Extract the version from the release tag (strip leading `v`, e.g. `v0.3.0` → `0.3.0`) and verify the version string is derived correctly — implemented in `release.yml` as `VERSION="${TAG#v}"`; verified by `dotnet pack -p:Version=0.3.0` producing `NuGex.0.3.0.nupkg`
- [x] 3.4 Add a `dotnet pack` step producing a framework-dependent .NET tool package, passing `-p:PackAsTool=true -p:SelfContained=false -p:PublishTrimmed=false -p:RuntimeIdentifiers="" -p:Version=<tag version>` (overrides the fsproj's static Nix props off for the tool), and verify the produced `.nupkg` is not platform-bound — verified: `packageType=DotnetTool`, `tools/net10.0/any/`, 85 entries
- [ ] 3.5 Add a `dotnet nuget push` step publishing the package to nuget.org using the `NUGET_API_KEY` secret, and verify a dry-run push (`--skip-duplicate` against nuget.org or a local feed) succeeds without the package being rejected — **requires `NUGET_API_KEY` secret + a GitHub release**
- [x] 3.6 Verify the release workflow does not attach any standalone binary artifacts to the GitHub Release — `release.yml` has no `uses: softprops/action-gh-release` or upload-artifact step

## 4. Verification

- [ ] 4.1 Trigger a real GitHub Release (test tag) and verify: build passes, the tool package publishes to nuget.org, and `dotnet tool install -g nugex --version <tag>` installs and `nugex` runs
- [ ] 4.2 Confirm the build pipeline did NOT run on any non-`main` push during testing
