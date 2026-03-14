# .NET/F# Expert Skill

This skill provides deep technical guidance for developing and refactoring .NET applications, specifically focusing on F# idiomatic patterns.

## 🛠 Core Principles
- **Favor Immutability**: Use `let` bindings and records by default. Avoid `mutable` and `Ref` unless strictly necessary for performance or protocol constraints.
- **Type Safety**: Leverage F# type inference. Define domain models using Discriminated Unions and Records before writing logic.
- **Functional Pipelines**: Chain operations using the `|>` operator. Keep each step clear and single-purpose.

## 📝 F# Specifics
- **Computation Expressions**: Use `task { ... }` for I/O and `async { ... }` for CPU-bound or legacy .NET async logic.
- **Pattern Matching**: Always aim for exhaustive matches. Use `match x with | Some v -> ... | None -> ...` instead of checking `.IsSome`.
- **JSON Serialization**: When using `System.Text.Json`, prefer `[| ... |]` (Arrays) over `List` for serialization performance and predictable behavior with JS consumers.

## 🧪 Testing & Verification
- **Test Discovery**: Use `dotnet test --list-tests` to see available tests.
- **Specific Execution**: Use `--filter "FullyQualifiedName~YourNamespace.YourTest"` for fast iteration.
- **Build Checks**: Run `dotnet build` after ANY change to F# files, as errors are caught at compile-time that would be runtime bugs in other languages.
