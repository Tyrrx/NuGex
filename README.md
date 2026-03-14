# NuGex

NuGex is a high-performance .NET analysis tool and [Model Context Protocol (MCP)](https://modelcontextprotocol.io/) server. It enables Large Language Models (LLMs) to understand the API surface area of local .NET solutions and NuGet packages without needing pre-existing documentation.

By extracting types, members, and XML documentation and exposing them via a fuzzy search index, NuGex allows agents to "explore" a library's capabilities on-demand.

## 🚀 Features

- **Solution Analysis**: Recursively extracts the public API (Types, Methods, Properties) from local `.sln` and `.fsproj`/`.csproj` files.
- **NuGet Integration**: Automatically downloads NuGet packages, extracts DLLs, and parses accompanying XML documentation.
- **Fuzzy Search**: Uses `FuzzySharp` for ranked search results, intelligently splitting queries between Types and Members.
- **MCP Server**: Implements the JSON-RPC MCP protocol for seamless integration with Claude Desktop, IDEs, and other LLM hosts.
- **Zero Dependencies**: Can be built as a standalone `musl`-linked binary for Linux.
- **In-Memory Caching**: Maintains indices in memory for sub-second search performance across multiple requests.

## 📦 Installation

### Nix (Recommended)

NuGex provides a Nix flake for reproducible builds on NixOS or any system with Nix installed.

```bash
# Build the standalone binary
nix build

# Run directly
nix run github:Tyrrx/NuGex -- --mcp

# Or with ssh
nix run git+ssh://git@github.com/Tyrrx/NuGex.git -- --mcp
```

*Note: If building from source, ensure you generate the dependency lock via `nix build .#default.passthru.fetch-deps` first.*

### .NET 10 SDK

If you have the .NET 10 SDK installed:

```bash
# Clone and run
dotnet run --project NuGex/NuGex.fsproj -- --mcp
```

## 🛠 Usage

### 1. Configure as an MCP Server

### 2. Available Tools

- **`search_solution`**: Indexes a local .NET solution and searches for specific APIs.
    - `solutionPath`: Absolute path to the `.sln` file.
    - `query`: The type or member name to find.
- **`search_package`**: Downloads and indexes a NuGet package.
    - `packageName`: e.g., "Newtonsoft.Json".
    - `packageVersion`: (Optional) Specific version.
    - `query`: The type or member name to find.

### 3. Demo Mode

Run NuGex without flags to see it in action analyzing a sample package:

```bash
./NuGex
```

## 🏗 Development

- **Build**: `dotnet build`
- **Clean**: `dotnet clean`
- **Lint**: Follow the guidelines in [AGENTS.md](./AGENTS.md).

NuGex is written in **F#** and leverages **Roslyn** (Microsoft.CodeAnalysis) for deep assembly and source code inspection.

## 📄 License

MIT
