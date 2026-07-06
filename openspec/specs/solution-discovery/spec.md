### Requirement: Solution discovery on MCP startup
When the MCP server starts, it SHALL recursively search its current working directory for `.sln` and `.slnx` files, ignoring `.csproj`/`.fsproj` files and skipping `bin`, `obj`, `.git`, `.vs`, and `node_modules` directories. If a directory cannot be enumerated (e.g. permission denied, or it is removed mid-search), discovery SHALL skip that directory and its subtree, log that it was skipped, and continue searching the rest of the tree rather than aborting.

#### Scenario: Solution found in a subdirectory
- **WHEN** the MCP server starts with a working directory that contains `src/MyApp.sln` and no `.sln`/`.slnx` at the root
- **THEN** discovery finds `src/MyApp.sln`

#### Scenario: Only project files present
- **WHEN** the working directory tree contains `.csproj`/`.fsproj` files but no `.sln`/`.slnx` file
- **THEN** discovery finds no solution

#### Scenario: Build output directories are skipped
- **WHEN** a `.sln` file exists only inside a `bin` or `obj` directory
- **THEN** discovery does not consider it a match

#### Scenario: An inaccessible directory is skipped
- **WHEN** the working directory tree contains a directory the process cannot read (e.g. permission denied), located anywhere in the tree relative to a valid `.sln`/`.slnx` file
- **THEN** discovery skips that directory and its subtree, logs that it was skipped, and still finds the solution file elsewhere in the tree without the server failing to start

### Requirement: Deterministic selection among multiple solutions
When discovery finds more than one `.sln`/`.slnx` file, the server SHALL deterministically select exactly one: the file with the fewest path segments from the working directory, breaking ties alphabetically by full path. The server SHALL log the selected path and the full list of candidates.

#### Scenario: One solution is shallower than another
- **WHEN** discovery finds both `MyApp.sln` (at the root) and `sample/Sample.sln` (nested)
- **THEN** the server selects `MyApp.sln` and logs both candidates

#### Scenario: Two solutions at the same depth
- **WHEN** discovery finds both `a/A.sln` and `b/B.sln` at the same depth
- **THEN** the server selects `a/A.sln` (alphabetically first) and logs both candidates

### Requirement: Conditional availability of the search_solution tool
The MCP `search_solution` tool SHALL be registered and listed only when solution discovery finds at least one `.sln`/`.slnx` file. When no solution is found, `search_solution` SHALL NOT appear in the MCP tool list for that session. Availability of the `search_package` and `get_package_readme` tools SHALL NOT depend on solution discovery.

#### Scenario: Solution found
- **WHEN** the MCP server starts and discovery finds a solution file
- **THEN** `search_solution` appears in the tool list alongside `search_package` and `get_package_readme`

#### Scenario: No solution found
- **WHEN** the MCP server starts and discovery finds no solution file
- **THEN** `search_solution` does not appear in the tool list, while `search_package` and `get_package_readme` still do

### Requirement: search_solution takes no path parameter
The MCP `search_solution` tool SHALL search the solution resolved by startup discovery and SHALL NOT accept a solution path parameter from the caller. It SHALL still accept `query`, `scope`, `limit`, and `maxDocChars` parameters as before.

#### Scenario: Tool invoked without a path argument
- **WHEN** an MCP client calls `search_solution` with only `query` (and optionally `scope`/`limit`/`maxDocChars`)
- **THEN** the server indexes and searches the solution discovered at startup and returns matching results

### Requirement: .slnx solution files are supported when opening a solution
Solution processing SHALL open both `.sln` and `.slnx` files as solutions (via `MSBuildWorkspace.OpenSolutionAsync`); any other path SHALL continue to be treated as a single project file.

#### Scenario: Opening a .slnx file
- **WHEN** solution processing is given a path ending in `.slnx`
- **THEN** it opens the path as a solution rather than as a single project
