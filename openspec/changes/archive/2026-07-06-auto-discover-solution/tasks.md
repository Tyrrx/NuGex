## 1. Update MCP SDK dependency

- [x] 1.1 Bump `ModelContextProtocol` in `NuGex/NuGex.fsproj` from `1.1.0` to `1.3.0` and run `dotnet restore`.

## 2. Solution discovery module

- [x] 2.1 Add `SolutionDiscovery.fs` with `findSolutionFiles (root: string) : string list`, recursively walking directories and pruning `bin`, `obj`, `.git`, `.vs`, `node_modules` (case-insensitive), collecting `*.sln`/`*.slnx` files.
- [x] 2.2 Add `pickSolution (files: string list) : string option` sorting by path segment count then alphabetically, returning the first match (or `None` if empty).
- [x] 2.3 Add `SolutionDiscovery.fs` to the `.fsproj` compile order before `Mcp.fs`/`Program.fs`.

## 3. Solution processor `.slnx` support

- [x] 3.1 Update `SolutionProcessor.processSolution` to open the path as a solution when it ends with `.sln` or `.slnx` (case-insensitive), keeping the existing `OpenProjectAsync` fallback for anything else.

## 4. Split MCP tool types

- [x] 4.1 In `Mcp.fs`, extract the shared static helpers (`StripXml`, `Truncate`, `FormatDoc`) into a module usable by multiple tool types.
- [x] 4.2 Add a `SolutionContext` record (`{ SolutionPath: string }`) to carry the discovered path via DI.
- [x] 4.3 Create `SolutionTools` (`[<McpServerToolType>]`) holding `SearchSolution`, constructor-injected with `SolutionContext`, `ISearchIndexCache`, `ILogger`; drop the `solutionPath` parameter and use `SolutionContext.SolutionPath` as the cache key.
- [x] 4.4 Create `PackageTools` (`[<McpServerToolType>]`) holding `SearchPackage` and `GetPackageReadme`, unchanged in behavior.

## 5. Startup wiring

- [x] 5.1 In `Program.fs`, before building the MCP host, call `SolutionDiscovery.findSolutionFiles` on `Directory.GetCurrentDirectory()` and `pickSolution` on the result.
- [x] 5.2 Log the discovered candidates and the selected path (or that none was found) via the configured logger.
- [x] 5.3 Register `PackageTools` unconditionally via `WithTools<PackageTools>()`.
- [x] 5.4 When a solution was found, register `SolutionContext` as a DI singleton and call `WithTools<SolutionTools>()`; skip both when none was found.

## 6. Documentation

- [x] 6.1 Update `README.md`'s `search_solution` tool description to remove the path parameter and describe auto-discovery plus conditional availability.

## 7. Verification

- [x] 7.1 Build the project (`dotnet build`) and confirm it compiles cleanly against `ModelContextProtocol` 1.3.0.
- [x] 7.2 Run `nugex mcp` from a directory with a nested `.sln` and confirm (via the MCP tool list) that `search_solution` is present and works without a path argument.
- [x] 7.3 Run `nugex mcp` from a directory with only project files (no `.sln`/`.slnx`) and confirm `search_solution` is absent from the tool list while `search_package`/`get_package_readme` remain.
- [x] 7.4 Confirm the CLI `search-solution` command still works unchanged with an explicit path argument.
