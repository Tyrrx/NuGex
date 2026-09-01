## Why

`NuGex` pins `ModelContextProtocol` at `1.3.0`. The current stable is `2.2.0`. The 2.x SDK
implements a newer MCP spec revision while staying wire-compatible with `2025-11-25`-and-earlier
peers, and per the SDK's versioning docs, non-deprecated 1.x APIs (everything NuGex uses:
`AddMcpServer`, `WithStdioServerTransport`, `WithTools<T>`, `McpServerToolType`/`McpServerTool`)
continue to work unmodified. Staying on 1.3.0 means missing spec/security fixes for no benefit.

## What Changes

- Bump the `ModelContextProtocol` PackageReference in `NuGex.fsproj` from `1.3.0` to `2.2.0`.
- Fix any compile fallout from the bump (expected: none, based on the APIs NuGex uses — see design.md).
- Re-verify the MCP server still starts and serves `search_solution`, `search_package`, and
  `get_package_readme` over stdio.

No tool behavior, tool schemas, or CLI surface changes for users.

## Capabilities

### New Capabilities
None.

### Modified Capabilities
None — this is a dependency version bump with no spec-level behavior change (`skip_specs: true`).

## Impact

- `NuGex/NuGex.fsproj`: `ModelContextProtocol` version bump.
- `NuGex/Mcp.fs`, `NuGex/Program.fs`: only files referencing `ModelContextProtocol.*` namespaces; no
  API used there is deprecated in 2.x, so no source changes expected beyond what the build reveals.
- Transitive: `ModelContextProtocol.Core` (or equivalent split package, if any) resolved by NuGet.
