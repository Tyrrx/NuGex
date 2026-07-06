## 1. Discovery walk hardening

- [x] 1.1 In `NuGex/SolutionDiscovery.fs`, wrap the `EnumerateFiles`/`EnumerateDirectories` loops inside `walk` in a `try/with` catching `UnauthorizedAccessException` and `IOException`, skipping the current directory's contents on failure instead of propagating.
- [x] 1.2 On catching an error, log a warning naming the skipped directory via `ILogger` (not raw stderr writes), consistent with `SolutionIndexWatcher`'s logging in `Mcp.fs`. Since discovery runs before the DI host is built, `Program.fs` constructs a small bootstrap `LoggerFactory` (mirroring the console config already registered on `builder.Logging`) and passes an `ILogger` into `findSolutionFiles`.
- [x] 1.3 Confirm recursion still applies the same guard when descending into subdirectories (i.e. a subdirectory that is itself unreadable is skipped without a separate code path).

## 2. Verification

- [x] 2.1 Manually verify: create a `chmod 000` subdirectory inside a tree that also contains a valid `.sln` elsewhere, run discovery from that root, and confirm it still finds the solution (verified via `dotnet fsi` against the built module rather than a full MCP client, since the fix is isolated to `SolutionDiscovery.findSolutionFiles`).
- [x] 2.2 Confirm existing scenarios in `specs/solution-discovery/spec.md` (solution in subdirectory, project-files-only, skipped build dirs) still hold — no regression to unaffected paths (verified via the same harness against a tree covering all three cases).
