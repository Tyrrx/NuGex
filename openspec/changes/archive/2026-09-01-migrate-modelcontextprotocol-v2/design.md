## Context

NuGex's MCP surface is three files: `NuGex.fsproj` (the `PackageReference`), `Mcp.fs` (`open
ModelContextProtocol` / `ModelContextProtocol.Server`, `[<McpServerToolType>]` / `[<McpServerTool>]`
on `SolutionTools` and `PackageTools`), and `Program.fs` (`AddMcpServer().WithStdioServerTransport()`,
`WithTools<PackageTools>()`, `WithTools<SolutionTools>()`). None of these touch the APIs the SDK's
`docs/list-of-diagnostics.md` marks obsolete in 2.x (EnumSchema helpers, legacy filter extensions,
Roots/Sampling/Logging, SSE transport, stateful Streamable HTTP). See proposal.md - Why.

## Goals / Non-Goals

**Goals:**
- Move to `ModelContextProtocol` 2.2.0 with the smallest possible diff.
- Confirm the three registered tools still start and respond over stdio.

**Non-Goals:**
- Adopting any new 2.x feature (Tasks extension, stateless HTTP transport, new schema types). None
  of that is needed by NuGex's stdio-only, tool-call-only usage.

## Decisions

- **Target 2.2.0, not an intermediate 1.4.x.** 1.4.x is a dead end (2.0.0 GA already shipped);
  jumping straight to the latest stable avoids a second bump later.
- **No transport/config changes.** `WithStdioServerTransport()` is unaffected by the 2.x stateless
  HTTP / stateful-options obsoletions, which only apply to the HTTP transport NuGex doesn't use.
- **Verify by running the built exe with `--mcp` and a manual stdio smoke test** (initialize +
  `tools/list`), rather than adding an automated integration test — there's no existing MCP
  integration-test harness in this repo, and this is a version bump, not new behavior.

## Risks / Trade-offs

- [NuGet may have split `ModelContextProtocol` into `ModelContextProtocol` + `ModelContextProtocol.Core`
  packages by 2.x, changing what's implicitly pulled in] → `dotnet restore` / `dotnet build` will
  surface this immediately as a missing-type compile error; add the split package if so.
- [Unknown transitive dependency bump breaks something unrelated] → `dotnet build` + the stdio smoke
  test catch this before merge; rollback is reverting the single version-number edit.

## Migration Plan

1. Bump the version, restore, build.
2. Fix any compile errors surfaced (none expected per Decisions above).
3. Run `nugex --mcp`, send `initialize` then `tools/list` over stdin, confirm all three tools are
   listed and one (`search_package`) round-trips successfully.
4. Rollback: revert the version-number edit if the smoke test fails and the cause isn't a quick fix.
