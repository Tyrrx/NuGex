## Context

`SolutionDiscovery.walk` (NuGex/SolutionDiscovery.fs) recursively enumerates every directory under the MCP server's working directory to find `.sln`/`.slnx` files. `EnumerateFiles`/`EnumerateDirectories` throw `UnauthorizedAccessException` as soon as a directory can't be read (permission bits, restricted mounts) or `IOException`-derived exceptions if a directory disappears mid-walk. `walk` has no exception handling, and `findSolutionFiles` forces the whole lazy `seq` eagerly via `List.ofSeq`, so the exception surfaces at `Program.fs:30`, before the MCP host is built, and is caught only by the top-level generic handler that prints an error and exits 1. One unreadable directory anywhere in the tree — regardless of where the actual solution lives — prevents the server from starting.

## Goals / Non-Goals

**Goals:**
- Directory enumeration errors during discovery are contained to the directory (and its subtree) that caused them; the walk continues over siblings and the rest of the tree.
- The server still starts and still finds a solution file elsewhere in the tree even when some directories are inaccessible.
- A skipped directory is visible in logs, not silently dropped.

**Non-Goals:**
- No change to which directories are skipped by name (`bin`/`obj`/`.git`/`.vs`/`node_modules` skip list is unrelated and untouched).
- No change to `search_solution`'s tool surface, `SolutionIndexWatcher`, or CLI solution handling.
- No attempt to distinguish *why* a directory is inaccessible (permissions vs. race vs. path length) beyond routing them to the same skip-and-continue behavior.

## Decisions

- **Catch at the recursion level, not at the top-level caller.** `walk` is already recursive (one call per directory). Wrapping the two enumeration loops in that same function in a `try/with` means each recursion level guards only its own directory, so a failure at any depth degrades to "skip this subtree" rather than "abort everything." Catching only in `Program.fs` (around the whole `findSolutionFiles cwd` call) was considered and rejected: it would only tolerate a bad top-level directory, still losing the entire search on any failure below the first level — exactly the current bug, just moved up one frame.
- **Exception types caught: `UnauthorizedAccessException` and `IOException`.** These cover permission denial (the reported symptom) and the delete-mid-walk race (`DirectoryNotFoundException`, `PathTooLongException` both derive from `IOException`). Not catching a bare `exn`/`Exception` on purpose — an unexpected error class (e.g. `OutOfMemoryException`) should still surface rather than be silently swallowed by a directory walk.
- **Skip the whole directory's contents on error, not partial results.** Enumeration failures happen synchronously at the OS boundary and are not easily resumable mid-iteration; treating a failed directory as fully skipped (files and subdirs) is simpler and matches how permission errors actually manifest (the whole directory is unreadable, not some files in it).
- **Log the skip via `ILogger`, not raw stderr writes.** `SolutionIndexWatcher` (Mcp.fs) already logs its own non-fatal failures through `ILogger`; routing the skipped-directory warning the same way keeps discovery diagnostics inside the app's logging pipeline (level filtering, structured fields) rather than an unconditional `eprintfn`, which doesn't compose with that pipeline and isn't what the MCP transport itself relies on. Since `findSolutionFiles` runs in `Program.fs` before `builder.Build()` (its result gates conditional service registration, so it can't be deferred until after the host exists), no DI-resolved `ILogger` is available yet. `Program.fs` builds a small bootstrap `LoggerFactory` mirroring the console config already set on `builder.Logging`, gets an `ILogger` from it, and passes that into `findSolutionFiles`/`walk` (threaded through the recursion) for the duration of discovery only.

## Risks / Trade-offs

- [Swallowing errors could hide a misconfigured root, e.g. `cwd` itself being unreadable] → Mitigation: the existing `[]` (no solutions found) branch in `Program.fs` already reports this case to the user; a root-level failure now degrades to "no solution found" plus a logged skip, rather than a crash — an acceptable, diagnosable outcome.
- [Logging every skipped directory could be noisy on a tree with many restricted mounts] → Mitigation: out of scope for this change; skip logging is a small, bounded improvement over the current total failure, and volume is not expected to be common enough to warrant throttling.
- [A separate bootstrap `LoggerFactory` for the pre-host phase is a second logging pipeline instance, not the same instance the host eventually uses] → Mitigation: it's configured identically (console, stderr) and disposed right after discovery; this is a standard "bootstrap logging" pattern for diagnostics needed before a DI container exists, not a long-lived duplicate.

## Open Questions

None — the earlier judgment call (log vs. silent skip) is resolved above; logging goes through `ILogger` via a bootstrap `LoggerFactory`, not `eprintfn`.
