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

#### Build & Run
```bash
# Build the standalone binary (outputs to ./result/bin/nugex)
nix build

# Run the local build
./result/bin/nugex --mcp

# Run directly from GitHub
nix run github:Tyrrx/NuGex -- --mcp
```

#### Updating Dependencies
If you've modified `NuGex.fsproj` (e.g., added a NuGet package), you must update the Nix dependency lock file:

1.  **Generate the update script**:
```bash
nix build .#default.passthru.fetch-deps && ./result nix/deps.json && rm result
```

*Note: The `fetch-deps` script requires the `dotnet` CLI to be available in your environment.*


### .NET 10 SDK

If you have the .NET 10 SDK installed:

```bash
# Clone and run
dotnet run --project NuGex/NuGex.fsproj -- --mcp
```

## 🛠 Usage

### 1. Configure as an MCP Server

### 2. Available Tools

- **`search_solution`**: Indexes and searches the .NET solution (`.sln`/`.slnx`) found under the MCP server's current working directory. Only available when a solution is found there; project files (`.csproj`/`.fsproj`) alone don't count.
    - `query`: The type or member name to find.
- **`search_package`**: Downloads and indexes a NuGet package.
    - `packageName`: e.g., "FunicularSwitch".
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
