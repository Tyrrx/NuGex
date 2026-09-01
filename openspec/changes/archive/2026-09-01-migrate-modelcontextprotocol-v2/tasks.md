## 1. Upgrade dependency

- [x] 1.1 Bump `ModelContextProtocol` from `1.3.0` to `2.2.0` in `NuGex/NuGex.fsproj` and run `dotnet restore` to verify it resolves.
- [x] 1.2 Run `dotnet build` and fix any compile errors in `Mcp.fs` / `Program.fs` surfaced by the bump; verify the build succeeds with no new warnings from `ModelContextProtocol.*` obsoletions.

## 2. Verify

- [x] 2.1 Run `dotnet run --project NuGex -- --mcp`, send `initialize` then `tools/list` over stdin, and verify all three tools (`search_solution`, `search_package`, `get_package_readme`) are listed.
- [x] 2.2 Call `search_package` for a known package (e.g. `Newtonsoft.Json`) over the stdio session and verify it returns results, confirming the MCP round-trip still works end-to-end.
