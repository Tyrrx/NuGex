# NuGex Agent Guide

This document provides essential information for autonomous agents contributing to the NuGex repository.

## 🛠 Build & Development

NuGex is a .NET 10.0 F# project.

- **Build Project**: `dotnet build`
- **Run MCP Server**: `dotnet run --project NuGex/NuGex.fsproj -- --mcp`
- **Run Demo Mode**: `dotnet run --project NuGex/NuGex.fsproj`
- **Restore Dependencies**: `dotnet restore`
- **Clean Build Artifacts**: `dotnet clean`

### Testing
*(Note: No test project is currently detected. When adding tests, follow these patterns:)*
- **Run All Tests**: `dotnet test`
- **Run Single Test**: `dotnet test --filter "FullyQualifiedName~TestName"`

## 📝 Code Style & Guidelines

### 1. F# Language & Formatting
- **Indentation**: Use 4 spaces for indentation. Rigorously follow F# indentation rules, especially for nested constructs like anonymous records.
- **Anonymous Records**: When using anonymous records (e.g., `{| Name = "NuGex" |}`), ensure properties are aligned and the closing `|}` is correctly placed to avoid `FS0010` and `FS0058` errors.
- **Array Literals**: Use `[| ... |]` for arrays that need to be serialized to JSON, as `System.Text.Json` treats F# lists as enumerable but handles arrays natively with better performance.
- **Task Computation Expressions**: Use `task { ... }` (from `System.Threading.Tasks`) for asynchronous operations to ensure compatibility with standard .NET Task-based APIs.
- **Pipeline Operator**: Use the `|>` operator to chain transformations, keeping one operation per line for complex pipelines.
- **Pattern Matching**: Exhaustively match all cases, using `_` only when truly appropriate. Prefer named patterns over `_` when the value is ignored but has semantic meaning.

### 2. Imports (Open Statements)
- Group `System` namespaces first, followed by third-party libraries, and then internal modules.
- Maintain alphabetical order within groups.
- Avoid unnecessary `open` statements; prefer fully qualified names if it improves clarity in complex logic.

### 3. Naming Conventions
- **Types/Modules**: PascalCase (e.g., `SearchIndex`, `SolutionProcessor`).
- **Functions/Values**: camelCase for local bindings, PascalCase for public members or module-level functions.
- **Interfaces**: Prefix with `I` (e.g., `ISearchProvider`).
- **Generic Parameters**: Prefix with `'` (e.g., `'T`, `'Error`).

### 4. Error Handling
- **Internal Logic**: Use F# `Result<'T, 'Error>` or `Option<'T>` for domain-level error handling. Avoid throwing exceptions for expected failure states.
- **MCP/IO Layer**: Use `try...with` blocks in the `Mcp.fs` loop and `SolutionProcessor` to catch and log environment-specific exceptions (e.g., MSBuild failures, IO errors).
- **Logging**: **CRITICAL**: In MCP mode, all diagnostic or error messages **MUST** be written to `Console.Error`. `stdout` is reserved strictly for JSON-RPC messages.

### 5. Architectural Patterns
- **Separation of Concerns**: Core indexing logic (`SearchService.fs`) and extraction logic (`SolutionProcessor.fs`) must remain independent of the MCP protocol.
- **Token Optimization**:
    - Strip XML tags from documentation in the MCP layer using `Regex`.
    - Truncate strings based on user-provided `maxDocChars`.
- **State Management**: Use `ConcurrentDictionary` for thread-safe caching of search indices in `Mcp.fs`.
- **Immutable Data**: Prefer immutable records and F# collections (`Map`, `List`, `Set`) for internal state representation. Use `Dictionary` or `ConcurrentDictionary` only when performance or thread-safety requires it.

## 📦 Dependency Management
NuGex has specific dependency constraints to avoid MSBuild/NuGet runtime conflicts:
- `Microsoft.Build.Framework` and `NuGet.Frameworks` must be marked with `ExcludeAssets="runtime" PrivateAssets="all"` in the `.fsproj`.
- Always verify that new package additions do not introduce version conflicts with `Microsoft.CodeAnalysis`.

## 🤖 Agent Workflow
1. **Analyze**: Use `glob` and `grep` to find existing patterns.
2. **Plan**: Propose a concise plan before editing.
3. **Build**: Run `dotnet build` after every file modification.
4. **Verify**: If in MCP mode, test tool calls using a HEREDOC script to ensure JSON-RPC compliance.
5. **Security**: Never commit secrets or API keys. Ensure `Console.Error` is used for all non-RPC output.

## 📋 Best Practices for Agents
- **Context Awareness**: Always check `NuGex.fsproj` for compilation order; F# is order-dependent.
- **Incremental Changes**: Make small, testable changes rather than large refactors.
- **Documentation**: Keep the `AGENTS.md` and other documentation updated as the project evolves.
- **Performance**: Be mindful of indexing time for large solutions; use progress reporting to `Console.Error`.
