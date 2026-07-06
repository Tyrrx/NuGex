### Requirement: Concurrent requests for the same cache key build the index once
`ISearchIndexCache.GetOrAdd` SHALL ensure that concurrent calls for the same key result in exactly one execution of the build factory, with all callers receiving the result of that single execution.

#### Scenario: Two simultaneous requests for the same uncached solution
- **WHEN** two `search_solution` calls for the same solution path arrive concurrently while no cached index exists for that path
- **THEN** the solution is indexed exactly once, and both calls receive results from that single index

#### Scenario: Two simultaneous requests for the same uncached package
- **WHEN** two `search_package` calls for the same package name and version arrive concurrently while no cached index exists for that key
- **THEN** the package is downloaded and indexed exactly once, and both calls receive results from that single index

### Requirement: Solution index is invalidated when project or solution files change
While a solution is registered, the server SHALL watch its root directory (recursively, excluding `bin`, `obj`, `.git`, `.vs`, and `node_modules`) for changes to `.sln`, `.slnx`, `.csproj`, and `.fsproj` files. When such a file is created, changed, deleted, or renamed, the server SHALL invalidate the cached index for that solution so that the next `search_solution` call rebuilds it.

#### Scenario: A project file is modified after indexing
- **WHEN** `search_solution` has already built and cached an index, and a `.csproj` file within the solution is subsequently saved
- **THEN** the cached index for that solution is invalidated

#### Scenario: A new project file is added
- **WHEN** a new `.fsproj` file is created under the solution's directory tree (outside `bin`/`obj`/`.git`/`.vs`/`node_modules`)
- **THEN** the cached index for that solution is invalidated

#### Scenario: Source file changes do not invalidate the index
- **WHEN** a `.cs` or `.fs` source file within the solution is changed
- **THEN** the cached index for that solution is NOT invalidated by this change alone

#### Scenario: Changes inside build output directories are ignored
- **WHEN** a file inside a `bin` or `obj` directory changes, including files matching watched extensions
- **THEN** the cached index for that solution is NOT invalidated

#### Scenario: Next search after invalidation rebuilds the index
- **WHEN** the cached index for a solution has been invalidated and a subsequent `search_solution` call is made
- **THEN** the server rebuilds the index from the solution's current on-disk state before returning results

### Requirement: Package index cache is not subject to file-based invalidation
Cached indices for `search_package` results SHALL NOT be invalidated by a file watcher, since a resolved `packageName`/`packageVersion` key refers to immutable package content.

#### Scenario: Repeated search_package calls for the same resolved version
- **WHEN** `search_package` is called multiple times for the same package name and the same explicit version
- **THEN** the cached index for that key is reused for the lifetime of the server process
