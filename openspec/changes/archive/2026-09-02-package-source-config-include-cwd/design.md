## Context

`PackageProcessor` builds its source repositories from `Settings.LoadDefaultSettings(null)` (PackageProcessor.fs). Per NuGet's docs, the `rootDirectory` argument drives the directory-walk that picks up `nuget.config` files in the tree; passing `null` loads only user-level (`%AppData%\NuGet\NuGet.config`) and machine-wide settings, so a `NuGet.Config` in the CWD/subdirectories is ignored. See proposal.md - Why.

## Goals / Non-Goals

**Goals:**
- Discover a `NuGet.Config` present in the current working directory or any subdirectory.
- Preserve existing user/machine-level config loading and the nuget.org fallback.

**Non-Goals:**
- No change to credential handling, source iteration/fallback logic, or the MCP/CLI surface.
- No explicit path-searching of arbitrary subdirectories beyond what `Settings.LoadDefaultSettings` already does when given a root.

## Decisions

**1. Pass the current working directory as the root to `Settings.LoadDefaultSettings`.**
```fsharp
Settings.LoadDefaultSettings(Environment.CurrentDirectory)
```
`LoadDefaultSettings(root)` walks the tree from `root` upward reading `nuget.config` at each level, then layers user and machine settings on top — exactly the "CWD or any subdirectory" discovery requested, with NuGet's own precedence and merging. `Environment.CurrentDirectory` reflects the process CWD, which is what the CLI and MCP server operate from.

Alternatives considered:
- Manually scanning subdirectories for `NuGet.Config` and merging via `LoadSpecificSettings`: reimplements precedence/merging NuGet already provides; rejected.
- Hardcoding a repo path: wrong, the tool must honor wherever it's invoked.

**2. Keep the empty-sources nuget.org fallback unchanged.**
`LoadDefaultSettings(cwd)` still yields nuget.org as the default when no config disables it, so the existing fallback in the `repositories` binding remains valid and untouched.

## Risks / Trade-offs

- [A `NuGet.Config` in a parent directory above CWD is now picked up] → That is the intended NuGet behavior (walking upward); matches how `dotnet` and NuGet resolve configs, so it is desirable, not a regression.
- [CWD changes between runs] → Sources are resolved at module initialization from the process CWD; both CLI and MCP resolve them from the process start, consistent with existing behavior.

## Migration Plan

Single-line code change; rollback is reverting one argument. No data migration.
