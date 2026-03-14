# NuGex MCP Workflow Skill

This skill provides the recommended workflow for using the NuGex MCP tools (`search_solution` and `search_package`) to explore and interact with .NET codebases.

## 🚀 Workflow Pattern

### 1. Initial Exploration
When starting a task in an unfamiliar .NET solution, first identify the main project files and solutions.
- **Action**: Use `glob` or `grep` to find `.sln` and `.fsproj`/`.csproj`.
- **NuGex Action**: Call `search_solution` with `scope = "Type"` and a query based on the task (e.g., "Auth", "Database", "Service").

### 2. Identifying Members
Once a relevant Type (Class/Interface) is found, explore its API surface to understand HOW to use it.
- **Action**: Call `search_solution` with `scope = "Member"` and the query of the Type or intended action.
- **Analysis**: Review the XML documentation returned in the results to understand method signatures and expected behavior.

### 3. External Library Research
Before adding a new NuGet package or when using an existing one without local documentation.
- **NuGex Action**: Call `search_package` with `packageName = "Package.Name"` and `scope = "Type"`.
- **Usage**: Search for core entry points (e.g., "Client", "Builder", "Factory").

### 4. Implementation Loop
Use the information gathered to write code.
- **Verification**: If the implementation fails to compile, use `search_solution` to double-check the exact member names or property types.
- **Refinement**: If the XML documentation is truncated, increase `maxDocChars` in the search call.

## 🛡 Security & Best Practices
- **Absolute Paths**: Always use absolute paths for `solutionPath`.
- **Fuzzy Search Nuance**: Fuzzy results are ranked. If the first result isn't perfect, look at the top 3-5.
- **Error Logs**: If the tool fails, check the server's `stderr` for MSBuild or indexing errors.
