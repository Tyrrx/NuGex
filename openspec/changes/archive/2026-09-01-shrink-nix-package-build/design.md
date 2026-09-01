## Context

The nix package (`flake.nix`) publishes `NuGex.fsproj` self-contained for `linux-x64` with
`PublishTrimmed=false`. A spike (interactive, not committed) established the concrete numbers and
failure mode this design works around:

- Untrimmed self-contained output: 114M / 231 DLLs, including ~15 satellite-language folders
  (`cs/`, `de/`, `es/`, ... each holding `*.resources.dll` for Roslyn/MSBuild strings) that nugex
  never uses (no `CultureInfo` switching, no localized user-facing strings).
- `PublishTrimmed=true` with the default `TrimMode=partial` (the SDK never fully-trims unless
  `TrimMode=full` is set) drops untrimmable/unreferenced code but is not scoped per-assembly by
  hand — it removes `Microsoft.CodeAnalysis.CSharp.Workspaces.dll` entirely, silently, with zero
  warning. Confirmed by `grep`: nothing in NuGex's F# source calls into that assembly directly —
  `PackageProcessor.fs:115` calls `Microsoft.CodeAnalysis.CSharp.CSharpCompilation` (a different,
  directly-referenced assembly, `Microsoft.CodeAnalysis.CSharp`, which survives trimming fine).
  `Microsoft.CodeAnalysis.CSharp.Workspaces` exists solely so `MSBuildWorkspace` can discover C#
  project-language support via MEF (`[ExportLanguageService]`) by scanning the output folder at
  runtime — a use the linker's static reachability analysis cannot see.
- Confirmed fix: an ILLink root descriptor (`<assembly fullname="..."><type fullname="*"/></assembly>`)
  naming that assembly (and `Microsoft.CodeAnalysis.Workspaces.MSBuild`, same MEF pattern) keeps
  both fully intact while the trimmer still removes ~115 unrelated DLLs (`System.Net.*`,
  `System.Security.Cryptography.*`, `WindowsBase.dll`, `System.ServiceModel.Web.dll`, VB support,
  etc.) → 61M / 119 DLLs, a 46% reduction, with `BuildHost-netcore`'s own DLLs verified
  byte-identical before/after (it's an MSBuild `Content` item, never linker input).
- `NuGex/NuGex.fsproj`'s `<RuntimeIdentifiers>linux-musl-x64</RuntimeIdentifiers>` has been stale
  since the very first commit; `flake.nix` has published `linux-x64` (glibc) for most of the
  project's history. musl and Native AOT were both explored as ways to get a fully static,
  zero-`ldd`-output binary and rejected — see proposal context and Risks below.

## Goals / Non-Goals

**Goals:**
- Cut the published closure to what nugex actually uses: trim reachable-but-unused BCL code, drop
  all non-English satellite resources, drop debug symbols.
- Do this without silently breaking `search_solution` against real C#-project solutions — the one
  failure mode this change can introduce, and the one this repo's own (F#-only) solution can't
  self-test.
- Resolve the fsproj/flake RID drift (`linux-musl-x64` declared, `linux-x64` shipped) while we're
  touching this file, since leaving it would keep confusing future readers about build intent.

**Non-Goals:**
- True static/zero-dependency linking (musl, Native AOT). Already explored and rejected: musl's
  self-contained publish hardcodes an Alpine-only interpreter path (`/lib/ld-musl-x86_64.so.1`),
  it's not actually static; Native AOT can't coexist with MSBuildWorkspace's MEF-based plugin
  discovery at all (same blind spot as trimming, but total rather than partial — AOT has no
  fallback "just don't trim this DLL" escape hatch the way ILLink's partial mode does). This
  change stays within self-contained CoreCLR publish, glibc RID.
- `TrimMode=full`. Far more aggressive, requires annotating/rooting the entire dependency graph;
  not worth the risk for the size this CLI tool is at.

## Decisions

- **`PublishTrimmed=true`, leave `TrimMode` at its default (`partial`).** Partial mode only trims
  assemblies that either opt in via `IsTrimmable` metadata or are explicitly rooted — everything
  else (Roslyn, MSBuild, NuGet.*, FuzzySharp, Argu, the app itself) is copied through untouched
  unless we say otherwise. This is what makes the "root the two MEF assemblies" fix precise and
  small, instead of needing to annotate a large safe-list.
- **Root descriptor XML, not `TrimmerRootAssembly` items.** Both can pin a whole assembly against
  trimming; the root descriptor (`<assembly fullname="X"><type fullname="*"/></assembly>`) is the
  one actually spiked and confirmed working. `TrimmerRootAssembly` would likely work too and needs
  one less file, but wasn't build-verified — not worth trading a confirmed mechanism for an
  unverified one to save a five-line XML file. File lives at `NuGex/roots.xml`, wired via
  `<TrimmerRootDescriptor Include="roots.xml" />` in the fsproj (this is an Item, not a flat
  property — it must live in the project file, not `flake.nix`'s `extraPublishFlags`).
- **`SatelliteResourceLanguages` set to `en` (or empty) in the fsproj, not the flake.** Same
  reasoning as trimming config generally: it's a property of what the app *is*, not how nix
  packages it — keeping it in the fsproj means `dotnet publish` outside of nix also gets the
  slimmed-down output, and it's one property next to the other publish-shape properties already
  there (`PublishTrimmed`, `InvariantGlobalization`).
- **`DebugType=none`** to drop `.pdb` generation entirely rather than `DebugType=embedded` (which
  would bloat the DLLs back up) — nugex ships no crash-reporting/symbolication story that would
  want the PDBs.
- **Fix `RuntimeIdentifiers` to `linux-x64`** in the same fsproj edit, since it's directly adjacent
  to the properties being touched and leaving contradictory RIDs in place only invites someone to
  "fix" it back to musl later without knowing why that was rejected.
- **Verify the C#-project path with a throwaway fixture, not by trusting the mechanism alone.**
  This repo has no `.csproj` to exercise `MSBuildWorkspace`'s C#-language MEF discovery. tasks.md
  includes generating a minimal scratch solution (one `.csproj`, one `.cs` file) and running
  `search_solution` against it through the same stdio smoke-test pattern used for the MCP version
  bump, specifically to catch a regression the root descriptor might have missed.

## Findings During Implementation

- **Trimming broke MCP server startup entirely, via a different mechanism than the one this design
  anticipated.** The ModelContextProtocol SDK builds each tool's parameter JSON schema at runtime
  via `System.Text.Json` reflection (NuGex's tool methods return untyped `obj`/`obj[]`, so there's
  no source-generated `JsonTypeInfo` for them to use instead). `PublishTrimmed=true` disables
  System.Text.Json's reflection-based fallback by default (a documented trimming feature switch),
  so building the schema for `System.Object[]` throws `NotSupportedException` and the host fails to
  start — before `search_solution`/MSBuildWorkspace ever enters the picture. Fixed with
  `<JsonSerializerIsReflectionEnabledByDefault>true</JsonSerializerIsReflectionEnabledByDefault>`
  in `NuGex.fsproj`, which re-enables just that fallback while leaving the rest of the trim
  configuration (and its size savings) untouched. Confirmed via the stdio smoke test: `initialize`,
  `tools/list` (all 3 tools), and `tools/call search_package` all succeed post-fix.
- **`search_solution` doesn't index a project's own declared types — only what it references.**
  Confirmed by reading `SolutionProcessor.fs:26-44`: it walks `compilation.References`
  (`IAssemblySymbol` per referenced assembly) and never touches `compilation.Assembly`. A scratch
  C#-fixture test that searched for its own `Widget` type returned unrelated BCL fuzzy-matches
  instead — on *both* the trimmed and an untrimmed baseline build, byte-for-byte identical output.
  Not a trimming regression; this is `search_solution`'s existing, by-design behavior (it surfaces
  the API surface a solution *depends on*, not the solution's own source). The real signal from
  this test is that `MSBuildWorkspace.OpenSolutionAsync`/`GetCompilationAsync` on a real `.csproj`
  — the exact path `Microsoft.CodeAnalysis.CSharp.Workspaces`'s MEF registration enables — produced
  identical reference-assembly results before and after trimming, which is what confirms the root
  descriptor fix actually works end-to-end.
- Actual measured result with the full property set (`PublishTrimmed`, `SatelliteResourceLanguages`,
  `DebugType=none`) applied together: 114M/231 DLLs → 28M/59 DLLs — better than the standalone
  trimming spike's 61M, since satellite-resource and PDB removal stack with it.

## Risks / Trade-offs

- [The root descriptor's two-assembly list is incomplete — some other MEF-discovered assembly
  gets silently dropped too, breaking a code path not covered by the scratch-fixture smoke test]
  → Mitigation is empirical, not exhaustive: the scratch-fixture test in tasks.md is the actual
  safety net. If it's not enough, the fallback is reverting to `PublishTrimmed=false` for this
  build (one property), not chasing a fully-exhaustive assembly list up front.
- [A future Roslyn/MSBuildWorkspace upgrade changes which assemblies are MEF-discovered, silently
  reintroducing the same failure mode we just fixed] → No automated guard against this exists or is
  proposed; flagged here so a future "why is search_solution broken again" debugging session finds
  this document instead of starting from zero. Out of scope to build tooling for this now.
- [`SatelliteResourceLanguages=en` also suppresses English-culture-neutral resources some
  dependency might expect at runtime] → Low risk: `en` is the neutral culture already; this
  property only prevents *additional* satellite cultures from being copied, it doesn't touch the
  main assemblies' embedded neutral resources.

## Migration Plan

1. Edit `NuGex/NuGex.fsproj`: fix `RuntimeIdentifiers`, add `PublishTrimmed=true`,
   `SatelliteResourceLanguages`, `DebugType=none`, and the `TrimmerRootDescriptor` item.
2. Add `NuGex/roots.xml` rooting `Microsoft.CodeAnalysis.CSharp.Workspaces` and
   `Microsoft.CodeAnalysis.Workspaces.MSBuild`.
3. Update `flake.nix` if any nix-side publish flags need to change (most of this lives in the
   fsproj per the decisions above, so this may be a no-op).
4. Regenerate `nix/deps.json` via the package's `passthru.fetch-deps` script.
5. `nix build .#default`; compare `du -sh result` before/after; confirm no satellite-language
   folders remain; confirm no `.pdb` files in the output.
6. Run the existing stdio MCP smoke test (initialize/tools-list/tools-call) against the built
   binary, plus the new scratch C#-solution smoke test for `search_solution`.
7. Rollback: revert the fsproj/flake edits (all localized to a handful of properties + one small
   XML file) if the C# smoke test fails and isn't a quick root-descriptor fix.
