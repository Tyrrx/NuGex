## MODIFIED Requirements

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
