## Why

`search_solution` caches its Roslyn-built index for the life of the MCP server with no invalidation, so an agent editing the very solution it's exploring gets stale results after the first index build. Separately, `ISearchIndexCache.GetOrAdd` has a check-then-act race that lets concurrent requests for the same uncached key redundantly rebuild the same index, and `PackageProcessor.processPackage` selects a package's "best" target framework by comparing framework moniker strings, which picks the wrong framework whenever alphabetical order disagrees with actual framework precedence (e.g. `.NETStandard,Version=v2.0` outranks `.NETCoreApp,Version=v8.0`).

## What Changes

- Add a `FileSystemWatcher` on the discovered solution's root directory (recursive, skipping `bin`/`obj`/`.git`/`.vs`/`node_modules`) that watches only `.sln`/`.slnx`/`.csproj`/`.fsproj` files. On any Changed/Created/Deleted/Renamed event for a watched file, the cached index for that solution is invalidated so the next `search_solution` call rebuilds it. Source files (`.cs`/`.fs`) are explicitly not watched.
- Replace `SearchIndexCache`'s check-then-act dictionary access with `ConcurrentDictionary<string, Lazy<Task<SearchIndex>>>` (`LazyThreadSafetyMode.ExecutionAndPublication`) so concurrent requests for the same key share one in-flight build instead of each running the indexing pipeline independently.
- Add an invalidation path (`TryRemove`) to the cache so the file watcher can drop a stale entry without needing to know about indexing internals.
- Fix `PackageProcessor.processPackage`'s target-framework selection to compare `NuGetFramework` values (via `NuGet.Frameworks`, already a transitive dependency) instead of sorting `DotNetFrameworkName` strings.

Out of scope: inotify buffer-overflow and OS watch-count limits under heavy file churn (e.g. during a full build) — deferred to a follow-up if observed in practice.

## Capabilities

### New Capabilities
- `search-index-caching`: Cache lifecycle for indexed solutions and packages — concurrency-safe build-once-per-key behavior, and file-watcher-driven invalidation of the solution index when its `.sln`/`.slnx`/`.csproj`/`.fsproj` files change.
- `package-framework-selection`: Correct selection of a NuGet package's target framework group when multiple are present, using framework precedence rather than string comparison.

### Modified Capabilities
(none — `solution-discovery` requirements are unchanged; this change affects index caching and package processing behavior only)

## Impact

- `NuGex/Mcp.fs`: `ISearchIndexCache`/`SearchIndexCache` gain concurrency-safe get-or-build and an invalidation method; a new file-watcher component is wired up alongside `SolutionContext` at startup and disposed with the host.
- `NuGex/PackageProcessor.fs`: target-framework group selection logic changes from string sort to `NuGetFramework` comparison.
- `NuGex/Program.fs`: startup wiring to create and register the solution file watcher when a solution is found.
- No changes to public tool signatures (`search_solution`, `search_package`, `get_package_readme` keep their existing parameters and behavior contracts).
