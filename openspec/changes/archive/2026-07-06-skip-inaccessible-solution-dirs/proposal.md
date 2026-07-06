## Why

The MCP server crashes on startup if any directory under the current working directory is unreadable (e.g. a `chmod 000` folder, an unmounted/permission-restricted mount point). Solution discovery walks the entire tree eagerly and unguarded, so a single inaccessible directory anywhere — even far from the actual solution — aborts the whole search and prevents the server from starting at all.

## What Changes

- `SolutionDiscovery.walk` catches `UnauthorizedAccessException` and `IOException` (covers `DirectoryNotFoundException`/`PathTooLongException`) when enumerating a directory's files/subdirectories, skips that directory/subtree, and continues walking the rest of the tree instead of propagating the error.
- A skipped directory is logged via `ILogger` (consistent with `SolutionIndexWatcher`'s logging in `Mcp.fs`) so the omission is visible rather than silent, without writing directly to the console.
- `Program.fs` gains a small bootstrap `LoggerFactory` to supply that `ILogger` during discovery, since discovery runs before the DI host is built. `Mcp.fs` and the `search_solution` tool's behavior/API are otherwise unchanged.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `solution-discovery`: adds a requirement that directory enumeration errors during the recursive search are tolerated (skip and continue) rather than aborting discovery.

## Impact

- Affected code: `NuGex/SolutionDiscovery.fs` (the `walk`/`findSolutionFiles` functions, now threading an `ILogger`) and `NuGex/Program.fs` (constructs the bootstrap logger and passes it in). `findSolutionFiles`' return value and call shape are otherwise unchanged.
- No API or tool-surface changes; purely a resilience fix to startup behavior.
