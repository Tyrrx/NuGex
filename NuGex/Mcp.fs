namespace NuGex

open System
open System.Collections.Concurrent
open System.ComponentModel
open System.Text.RegularExpressions
open System.Threading.Tasks
open Microsoft.CodeAnalysis.MSBuild
open ModelContextProtocol
open ModelContextProtocol.Server
open Microsoft.Extensions.Logging

/// <summary>
/// Singleton service for caching search indices during the MCP session.
/// </summary>
type ISearchIndexCache =
    abstract member GetOrAdd: key: string * factory: (unit -> Task<SearchIndex>) -> Task<SearchIndex>

type SearchIndexCache() =
    let registry = ConcurrentDictionary<string, SearchIndex>()
    interface ISearchIndexCache with
        member _.GetOrAdd(key, factory) = task {
            match registry.TryGetValue(key) with
            | true, index -> return index
            | _ ->
                let! index = factory()
                registry.TryAdd(key, index) |> ignore
                return index
        }

/// <summary>
/// The solution discovered at MCP server startup, if any. Registered as a DI singleton
/// only when a solution was found, gating whether <see cref="SolutionTools"/> is registered.
/// </summary>
type SolutionContext = { SolutionPath: string }

module DocFormatting =

    let stripXml (xml: string) =
        if String.IsNullOrWhiteSpace(xml) then ""
        else Regex.Replace(xml, "<.*?>", "")

    let truncate (text: string) (max: int) =
        if String.IsNullOrWhiteSpace(text) then ""
        elif text.Length <= max then text
        else text.Substring(0, max) + "..."

    let formatDoc (doc: string) (maxChars: int) =
        doc |> stripXml |> (fun s -> truncate s maxChars)

[<McpServerToolType>]
type SolutionTools(context: SolutionContext, cache: ISearchIndexCache, logger: ILogger<SolutionTools>) =

    [<McpServerTool; Description("Indexes and fuzzy searches the public API (Types, Methods, Properties) of the .NET solution discovered in the current working directory. Use this to understand the structure and available members of the codebase you are currently working in.")>]
    member _.SearchSolution
        (
            [<Description("The name of the type or member to search for (e.g. 'JsonConvert' or 'Serialize').")>] query: string,
            [<Description("Whether to search for Type definitions or specific Members (methods/properties).")>] scope: string,
            [<Description("Maximum number of results to return (default: 5).")>] limit: Nullable<int>,
            [<Description("Maximum characters of XML documentation to return per result (default: 1000).")>] maxDocChars: Nullable<int>
        ) = task {

        let limitVal = if limit.HasValue then limit.Value else 5
        let maxDocCharsVal = if maxDocChars.HasValue then maxDocChars.Value else 1000

        let! index =
            try
                cache.GetOrAdd(context.SolutionPath, fun () -> task {
                    logger.LogInformation("Indexing solution: {SolutionPath}", context.SolutionPath)
                    use workspace = MSBuildWorkspace.Create()
                    let! model = SolutionProcessor.processSolution workspace context.SolutionPath
                    return SearchIndex(model)
                })
            with ex ->
                logger.LogError(ex, "Error indexing solution: {SolutionPath}", context.SolutionPath)
                reraise()

        if scope.Equals("Type", StringComparison.OrdinalIgnoreCase) then
            let results = index.SearchTypes(query, limitVal)
            return results |> List.map (fun r ->
                let doc = DocFormatting.formatDoc r.Type.Documentation maxDocCharsVal
                {| FullName = r.FullName; Score = r.Score; Documentation = doc |} :> obj) |> List.toArray
        else
            let results = index.SearchMembers(query, limitVal)
            return results |> List.map (fun r ->
                let rawDoc =
                    match r.Member with
                    | Choice1Of2 m -> m.Documentation
                    | Choice2Of2 p -> p.Documentation
                let doc = DocFormatting.formatDoc rawDoc maxDocCharsVal
                {| FullName = r.FullName; ParentType = r.ParentTypeName; Score = r.Score; Documentation = doc |} :> obj) |> List.toArray
    }

[<McpServerToolType>]
type PackageTools(cache: ISearchIndexCache, logger: ILogger<PackageTools>) =

    [<McpServerTool; Description("Downloads and indexes a NuGet package to search its public API. Use this to explore external libraries before writing code that consumes them.")>]
    member _.SearchPackage
        (
            [<Description("The NuGet package ID (e.g. 'Newtonsoft.Json').")>] packageName: string,
            [<Description("The name of the type or member to search for.")>] query: string,
            [<Description("Search for Type definitions or Members.")>] scope: string,
            [<Description("Optional version string. If omitted, the latest stable version is used.")>] packageVersion: string,
            [<Description("Maximum number of results to return (default: 5).")>] limit: Nullable<int>,
            [<Description("Maximum characters of XML documentation to return per result (default: 1000).")>] maxDocChars: Nullable<int>
        ) = task {

        let limitVal = if limit.HasValue then limit.Value else 5
        let maxDocCharsVal = if maxDocChars.HasValue then maxDocChars.Value else 1000
        let version = if String.IsNullOrWhiteSpace(packageVersion) then None else Some packageVersion

        let key = match version with Some v -> $"{packageName}:{v}" | None -> packageName

        let! index = cache.GetOrAdd(key, fun () -> task {
            logger.LogInformation("Indexing package: {PackageKey}", key)
            let! model = PackageProcessor.processPackage packageName version
            return SearchIndex(model)
        })

        if scope.Equals("Type", StringComparison.OrdinalIgnoreCase) then
            let results = index.SearchTypes(query, limitVal)
            return results |> List.map (fun r ->
                let doc = DocFormatting.formatDoc r.Type.Documentation maxDocCharsVal
                {| FullName = r.FullName; Score = r.Score; Documentation = doc |} :> obj) |> List.toArray
        else
            let results = index.SearchMembers(query, limitVal)
            return results |> List.map (fun r ->
                let rawDoc =
                    match r.Member with
                    | Choice1Of2 m -> m.Documentation
                    | Choice2Of2 p -> p.Documentation
                let doc = DocFormatting.formatDoc rawDoc maxDocCharsVal
                {| FullName = r.FullName; ParentType = r.ParentTypeName; Score = r.Score; Documentation = doc |} :> obj) |> List.toArray
    }

    [<McpServerTool; Description("Retrieves the README content of a NuGet package.")>]
    member _.GetPackageReadme
        (
            [<Description("The NuGet package ID (e.g. 'FunicularSwitch').")>] packageName: string,
            [<Description("Optional version string. If omitted, the latest stable version is used.")>] packageVersion: string
        ) = task {
        let version = if String.IsNullOrWhiteSpace(packageVersion) then None else Some packageVersion
        let! readme = PackageProcessor.getPackageReadme packageName version
        return readme
    }
