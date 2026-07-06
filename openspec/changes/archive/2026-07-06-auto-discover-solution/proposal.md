## Why

The MCP `search_solution` tool requires an absolute path to a `.sln`/`.csproj`/`.fsproj` file, but an MCP client (e.g. an AI coding agent) already runs with the server's working directory set to the project it's operating on. Forcing the agent to guess or supply a path is redundant and error-prone. The server should discover the solution itself, and the tool should simply not appear when there's nothing to search.

## What Changes

- The MCP `search_solution` tool drops its `solutionPath` parameter. **BREAKING** for any MCP client currently passing a path.
- On MCP server startup, recursively scan the server's current working directory for `.sln`/`.slnx` files (skipping `bin`, `obj`, `.git`, `node_modules`, `.vs`). Standalone `.csproj`/`.fsproj` files are ignored for discovery purposes.
- If no `.sln`/`.slnx` is found, the `search_solution` tool is not registered/exposed at all — it won't appear in the MCP tool list for that session.
- If one or more solution files are found, the shallowest (fewest path segments), alphabetically-first one is selected automatically and logged; `search_solution` is registered against that path.
- The CLI `search-solution` command is unaffected — it keeps its existing explicit `Path` argument for interactive/scripted use outside the MCP server.

## Capabilities

### New Capabilities
- `solution-discovery`: Startup-time discovery of a .NET solution file under the MCP server's working directory, and conditional exposure of the `search_solution` MCP tool based on that discovery.

### Modified Capabilities
- (none — no pre-existing spec covers `search_solution`'s current parameter-based behavior)

## Impact

- `NuGex/Mcp.fs`: split `SearchSolution` out of `NuGexTools` into its own tool type taking a discovered path via DI instead of a method parameter; drop the `solutionPath` parameter.
- `NuGex/Program.fs`: add solution discovery at MCP startup; conditionally call `WithTools<...>` for the solution tool type only when a solution is found; always register the package tools.
- `NuGex/SolutionProcessor.fs`: accept `.slnx` in addition to `.sln` when opening a solution.
- New module for directory-walking discovery logic (e.g. `SolutionDiscovery.fs`).
- `README.md`: update `search_solution` tool documentation to reflect the removed parameter and discovery behavior.
- `NuGex/NuGex.fsproj`: bump `ModelContextProtocol` from 1.1.0 to 1.3.0 (the `WithTools<T>()` API needed for conditional registration exists in both; the bump tracks the current SDK release).
