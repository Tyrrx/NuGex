## 1. Concurrency-safe cache

- [x] 1.1 Change `SearchIndexCache`'s backing store from `ConcurrentDictionary<string, SearchIndex>` to `ConcurrentDictionary<string, Lazy<Task<SearchIndex>>>`
- [x] 1.2 Implement `GetOrAdd` via the dictionary's own `GetOrAdd(key, _ => new Lazy<Task<SearchIndex>>(factory, LazyThreadSafetyMode.ExecutionAndPublication)).Value`
- [x] 1.3 Add an `Invalidate(key: string)` member to `ISearchIndexCache`/`SearchIndexCache` that removes the entry for a key
- [x] 1.4 Verify existing `SearchSolution`/`SearchPackage` call sites in `Mcp.fs` still compile unchanged against the new cache shape

## 2. Solution file watcher

- [x] 2.1 Add a component that creates a `FileSystemWatcher` rooted at the discovered solution's directory when `SolutionContext` is registered (`Program.fs`), with `IncludeSubdirectories = true`
- [x] 2.2 Filter watcher events: ignore paths containing `bin`, `obj`, `.git`, `.vs`, or `node_modules` path segments (reuse `SolutionDiscovery`'s skip-dir set), and ignore extensions other than `.sln`, `.slnx`, `.csproj`, `.fsproj`
- [x] 2.3 On a matching Changed/Created/Deleted/Renamed event, call `cache.Invalidate(context.SolutionPath)`
- [x] 2.4 Register the watcher's lifetime with the host so it is disposed on shutdown
- [x] 2.5 Handle watcher creation failure (e.g. path missing) by logging and continuing without invalidation, matching the existing degrade-gracefully behavior in `Program.fs`

## 3. Package framework selection fix

- [x] 3.1 Replace `Seq.sortByDescending (fun g -> g.TargetFramework.DotNetFrameworkName)` in `PackageProcessor.processPackage` with a comparison over `NuGetFramework` values using `NuGet.Frameworks`
- [x] 3.2 Confirm the exact `NuGet.Frameworks` API used (e.g. `NuGetFrameworkSorter`, `FrameworkReducer`, or direct `Version`/`Framework` comparison) against the installed package version

## 4. Verification

- [x] 4.1 Manual test: start the MCP server against a local solution, call `search_solution`, edit a `.csproj` (e.g. add a package reference), call `search_solution` again, and confirm the new reference's types are searchable without a restart
- [x] 4.2 Manual test: edit only a `.cs`/`.fs` file and confirm the cached index is not invalidated by the watcher
- [x] 4.3 Manual or unit test: fire concurrent `search_package` calls for the same uncached package/version and confirm only one download+index occurs (e.g. via logging)
- [x] 4.4 Manual test: run `search_package` against a package known to ship both a `netstandard2.0` and a `net8.0`+ lib group and confirm the modern target's members are returned
