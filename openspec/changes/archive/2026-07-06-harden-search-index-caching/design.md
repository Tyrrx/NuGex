## Context

`SearchIndexCache` (`NuGex/Mcp.fs`) backs both `search_solution` and `search_package`. It stores a plain `SearchIndex` per key in a `ConcurrentDictionary`, built via an explicit `TryGetValue` → factory → `TryAdd` sequence. This has two problems:

1. **No invalidation.** Once a solution is indexed, the entry lives for the process lifetime. The primary user of `search_solution` is an LLM coding agent actively editing the same solution it's searching, so stale results are the common case, not an edge case.
2. **No mutual exclusion.** Two concurrent calls for the same uncached key both see a miss and both run the factory (a full Roslyn compile or NuGet download + compile), wasting work. The result is harmless (last write wins) but costly.

Separately, `PackageProcessor.processPackage` (`NuGex/PackageProcessor.fs`) picks which `FrameworkSpecificGroup` to extract from a package by `Seq.sortByDescending (fun g -> g.TargetFramework.DotNetFrameworkName)` — an ordinary string comparison over framework monikers, which does not reflect actual framework precedence.

## Goals / Non-Goals

**Goals:**
- Solution index reflects the current state of `.sln`/`.slnx`/`.csproj`/`.fsproj` files without requiring a server restart.
- Concurrent lookups for the same cache key never run the expensive build more than once.
- Package framework-group selection reflects real framework precedence, not moniker string order.

**Non-Goals:**
- Reacting to `.cs`/`.fs` source file edits (explicitly out of scope per the proposal — only project/solution files are watched).
- Handling FileSystemWatcher buffer overflow or Linux inotify watch-count exhaustion under heavy file churn (deferred).
- Any change to package (`search_package`) cache invalidation — nupkgs are immutable per resolved version, so no watcher is needed there.
- Any change to public MCP tool signatures or CLI arguments.

## Decisions

### 1. Invalidation is push-based (FileSystemWatcher), not pull-based (signature check)
A `FileSystemWatcher` is created for the discovered solution's root directory when a solution exists (`SolutionContext` is registered), with `IncludeSubdirectories = true`. Its event handler:
- Ignores any path containing a segment from `SolutionDiscovery`'s existing skip set (`bin`, `obj`, `.git`, `.vs`, `node_modules`).
- Ignores any path whose extension is not `.sln`, `.slnx`, `.csproj`, or `.fsproj`.
- On a match (Changed, Created, Deleted, or Renamed), calls `cache.Invalidate(context.SolutionPath)`.

Rebuilding happens lazily on the next `search_solution` call via the existing `GetOrAdd` path — the watcher's only job is removing the stale entry. This avoids needing debounce logic: a burst of events (e.g. a multi-file save) produces redundant no-op `TryRemove` calls, and the next search naturally triggers exactly one rebuild.

**Alternative considered**: compute a cheap signature (e.g. max mtime across watched files) on every `search_solution` call and rebuild on mismatch. Rejected because it adds a filesystem walk to every call's hot path, whereas the watcher only does work when something actually changes.

### 2. Concurrency-safe cache via `Lazy<Task<SearchIndex>>`
`SearchIndexCache`'s backing store becomes `ConcurrentDictionary<string, Lazy<Task<SearchIndex>>>`, and `GetOrAdd` is implemented in terms of the dictionary's own `GetOrAdd(key, _ => new Lazy<Task<SearchIndex>>(factory, LazyThreadSafetyMode.ExecutionAndPublication))`. Concurrent callers may each construct a `Lazy` wrapper, but only one is ever stored, and `ExecutionAndPublication` guarantees the wrapped factory itself runs at most once for whichever `Lazy` wins.

Invalidation is a plain `TryRemove(key)` on the same dictionary — no special handling needed since removing a `Lazy` that's mid-build only affects the next lookup, not any caller already holding a reference to the in-flight `Task`.

**Alternative considered**: per-key `SemaphoreSlim`. Rejected as unnecessary complexity — `Lazy<Task<T>>` gives the same single-build guarantee with less code and no explicit lock management.

**Accepted residual race**: if a watcher event invalidates a key at the same instant a rebuild is already in flight for the old entry, the in-flight build completes and is stored, then is immediately visible as "current" until the next change is detected. This only matters if a file changes again within that narrow window, in which case the result is simply one extra rebuild on the following call — not a correctness issue.

### 3. Framework selection via `NuGetFramework` comparison
Replace the string-based sort with a comparison over the parsed `NuGetFramework` values already present on each `FrameworkSpecificGroup.TargetFramework`, using `NuGet.Frameworks`' own comparison/reducer facilities (already a transitive dependency via `NuGet.Packaging`/`NuGet.Protocol`) rather than hand-rolling framework precedence rules.

## Risks / Trade-offs

- [Watcher setup fails or throws (e.g. path deleted between discovery and watcher creation)] → Log and continue without invalidation for that session, matching the existing "best effort" posture of solution discovery (`Program.fs` already logs and degrades gracefully when no solution is found).
- [Deferred: inotify buffer overflow / watch-count limits under heavy build churn] → Documented as a known ceiling in code, not handled; revisit if observed in practice.
- [`Lazy<Task<T>>` wrapping a faulted task] → A failed build's exception is cached in the `Lazy` until invalidated; a subsequent watcher-triggered invalidation (or manual retry path, if any exists) is required to clear a persistently failing entry. Acceptable since the prior implementation had no retry-after-failure story either.

## Migration Plan

No data migration. This changes in-process cache implementation and startup wiring only. Rollout is a normal deploy of the updated binary; no rollback concerns beyond reverting the change.

## Open Questions

None outstanding — exact `NuGet.Frameworks` API surface for comparison (e.g. `NuGetFrameworkSorter` vs `FrameworkReducer`) to be confirmed during implementation.
