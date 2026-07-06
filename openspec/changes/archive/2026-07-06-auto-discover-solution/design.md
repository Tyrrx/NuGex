## Context

`NuGexTools` (`NuGex/Mcp.fs`) is a single `[<McpServerToolType>]` class exposing `SearchSolution`, `SearchPackage`, and `GetPackageReadme`. `Program.fs` registers all tools in one shot via `WithToolsFromAssembly()`, so every `[<McpServerTool>]` method in the assembly is always listed. The installed `ModelContextProtocol` package is 1.1.0; it already exposes `IMcpServerBuilder.WithTools<T>()`, which registers only the tools declared on type `T`. There is no per-tool "enabled" flag on `McpServerTool`/`IMcpServerPrimitive` in this SDK version (checked against both 1.1.0 and the newer 1.3.0 available locally), so conditional exposure has to happen by choosing which types to register, not by hiding an individual method at runtime.

The MCP server is a stdio process started fresh per session (`nugex mcp`) with the client-provided working directory. That directory is fixed for the process's lifetime, so discovery only needs to happen once at startup — no file-watching or hot re-registration is needed.

## Goals / Non-Goals

**Goals:**
- Resolve a solution file automatically from the server's working directory, with zero required parameters on the MCP tool.
- Only list `search_solution` in the MCP tool set when a solution was actually found.
- Keep the change additive/self-contained to the MCP path; don't touch the CLI's explicit-path workflow.

**Non-Goals:**
- Watching the filesystem for solutions appearing/disappearing after startup.
- Supporting multiple simultaneously-searchable solutions in one server session.
- Changing `search_package` / `get_package_readme` behavior.
- Adding fallback discovery of standalone `.csproj`/`.fsproj` files — explicitly out of scope per the proposal.

## Decisions

**Split `NuGexTools` into `SolutionTools` and `PackageTools`.**
`WithTools<T>()` registers by type, so the tool(s) we want to gate need to live on their own type, separate from the always-available package tools. `SolutionTools` takes the discovered solution path via constructor injection (a small `SolutionContext` record registered as a DI singleton), the same way it already takes `ISearchIndexCache`/`ILogger`. `PackageTools` keeps `SearchPackage`/`GetPackageReadme` and the shared static helpers (`StripXml`/`Truncate`/`FormatDoc`) move to a small shared module since both types need them.
- Alternative considered: keep one type and filter at the `ListToolsHandler` level (custom `McpServerOptions.Capabilities.Tools.ListToolsHandler` that strips `search_solution` from results). Rejected — more moving parts, and doesn't stop a client from calling the tool directly since `CallToolHandler` isn't affected; type-based registration is simpler and actually removes the capability, not just its listing.

**Discovery: recursive scan of cwd for `*.sln`/`*.slnx`, skipping build/vcs noise dirs.**
New `SolutionDiscovery` module with `findSolutionFiles (root: string) : string list`, walking directories and pruning `bin`, `obj`, `.git`, `.vs`, `node_modules` (case-insensitive name match) to avoid slow/irrelevant scans of generated output and dependencies. Project files (`.csproj`/`.fsproj`) are never considered, matching the proposal.
- Alternative considered: use `Directory.EnumerateFiles(root, "*.sln", SearchOption.AllDirectories)` with no pruning. Rejected for repos with large `bin`/`obj`/`node_modules` trees — pruning keeps discovery fast without adding real complexity (still a single recursive walk).

**Picking among multiple matches: shallowest path, then alphabetical.**
`pickSolution (files: string list) : string option` sorts by path segment count then by string, returns the first. The choice (and the full list, if more than one was found) is logged via `ILogger` at startup so the user can see what got picked and override by restructuring/removing files if wrong.
- Alternative considered: error out / require disambiguation when multiple solutions exist. Rejected — no interactive channel exists at MCP startup to ask the user; a deterministic, logged best-effort default is more useful than a hard failure for the common case (one real solution, maybe a sample/test solution nested deeper).

**Bump `ModelContextProtocol`/`ModelContextProtocol.Core` from 1.1.0 to 1.3.0.**
`WithTools<T>()` already exists in 1.1.0, so the bump isn't required to implement conditional registration — but the newer SDK is picked up anyway to track upstream fixes/features (e.g. the request-filter pipeline for `ListTools`/`CallTool`) available for future use. Verified via decompilation that `WithTools<T>()`, `WithTools(IEnumerable<Type>)`, `WithToolsFromAssembly()`, and `AddMcpServer()`/`WithStdioServerTransport()` all keep identical signatures between 1.1.0 and 1.3.0, and 1.3.0 ships a `net10.0` target — so this is a drop-in version change with no code adjustments beyond the `.fsproj` version numbers.
- Alternative considered: stay on 1.1.0 since nothing here strictly needs the newer version. Rejected per explicit preference to keep the MCP SDK current.

**`.slnx` support in `SolutionProcessor`.**
`processSolution` currently branches on `targetPath.EndsWith(".sln")` vs. treating anything else as a project. Change the check to accept both `.sln` and `.slnx` via `OpenSolutionAsync` (Roslyn's `MSBuildWorkspace` handles both extensions the same way); anything else still falls through to `OpenProjectAsync` for the CLI's project-file case, which is unaffected.

**CLI unaffected.**
`search-solution` (`Cli.fs`) keeps its `Mandatory Path` argument. It's an explicit, scriptable interface where the caller already knows the path; auto-discovery is specifically about removing that requirement for MCP clients that inherit a working directory instead of receiving an explicit argument.

## Risks / Trade-offs

- [Wrong solution picked when a repo has multiple `.sln`/`.slnx` files] → Mitigated by deterministic shallowest-then-alphabetical ordering plus a startup log line listing all candidates and the chosen one.
- [Large repositories slow down MCP startup due to recursive scan] → Mitigated by pruning `bin`/`obj`/`.git`/`.vs`/`node_modules`; scan only walks source directories.
- [Breaking change for any existing MCP client passing `solutionPath`] → Called out explicitly as **BREAKING** in the proposal; there's no known external consumer today (single-repo tool), so no migration shim is added.

## Open Questions

None — proposal scope (ignore project files, cwd-rooted recursive search, drop the tool entirely when nothing is found) resolves the ambiguous points.
