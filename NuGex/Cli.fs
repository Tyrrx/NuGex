namespace NuGex

open System
open System.Threading.Tasks
open Argu
open Microsoft.CodeAnalysis.MSBuild

type SearchScope =
    | Type
    | Member

type SolutionArgs =
    | [<MainCommand; Mandatory>] Path of string
    | [<AltCommandLine("-q")>] Query of string
    | [<AltCommandLine("-s")>] Scope of SearchScope
    | [<AltCommandLine("-l")>] Limit of int
    | [<AltCommandLine("-m")>] Max_Doc_Chars of int
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Path _ -> "Absolute path to the .sln or .fsproj/.csproj file."
            | Query _ -> "The name of the type or member to search for."
            | Scope _ -> "Whether to search for Type definitions or specific Members (default: Member)."
            | Limit _ -> "Maximum number of results to return (default: 5)."
            | Max_Doc_Chars _ -> "Maximum characters of XML documentation to return per result (default: 1000)."

type PackageArgs =
    | [<MainCommand; Mandatory>] Package_Id of string
    | [<AltCommandLine("-q")>] Query of string
    | [<AltCommandLine("-v")>] Version of string
    | [<AltCommandLine("-s")>] Scope of SearchScope
    | [<AltCommandLine("-l")>] Limit of int
    | [<AltCommandLine("-m")>] Max_Doc_Chars of int
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Package_Id _ -> "The NuGet package ID (e.g. 'Newtonsoft.Json')."
            | Query _ -> "The name of the type or member to search for."
            | Version _ -> "Optional version string. If omitted, the latest stable version is used."
            | Scope _ -> "Search for Type definitions or Members (default: Member)."
            | Limit _ -> "Maximum number of results to return (default: 5)."
            | Max_Doc_Chars _ -> "Maximum characters of XML documentation to return per result (default: 1000)."

type PackageReadmeArgs =
    | [<MainCommand; Mandatory>] Package_Id of string
    | [<AltCommandLine("-v")>] Version of string
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Package_Id _ -> "The NuGet package ID (e.g. 'Newtonsoft.Json')."
            | Version _ -> "Optional version string. If omitted, the latest stable version is used."

type Arguments =
    | [<CliPrefix(CliPrefix.None)>] Search_Solution of ParseResults<SolutionArgs>
    | [<CliPrefix(CliPrefix.None)>] Search_Package of ParseResults<PackageArgs>
    | [<CliPrefix(CliPrefix.None)>] Get_Package_Readme of ParseResults<PackageReadmeArgs>
    | [<AltCommandLine("-m")>] Mcp
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Search_Solution _ -> "Indexes and fuzzy searches a local .NET solution."
            | Search_Package _ -> "Downloads and indexes a NuGet package to search its public API."
            | Get_Package_Readme _ -> "Retrieves the README content of a NuGet package."
            | Mcp -> "Starts the Model Context Protocol (MCP) server."

module CliHandler =

    let private printResults (results: obj array) =
        if results.Length = 0 then
            printfn "No results found."
        else
            for item in results do
                match item with
                | :? {| FullName: string; Score: int; Documentation: string |} as t ->
                    printfn "[Score: %d] %s" t.Score t.FullName
                    if not (String.IsNullOrWhiteSpace t.Documentation) then
                        printfn "  Doc: %s" t.Documentation
                | :? {| FullName: string; ParentType: string; Score: int; Documentation: string |} as m ->
                    printfn "[Score: %d] %s (in %s)" m.Score m.FullName m.ParentType
                    if not (String.IsNullOrWhiteSpace m.Documentation) then
                        printfn "  Doc: %s" m.Documentation
                | _ -> printfn "%A" item
            printfn ""

    let handleSearchSolution (res: ParseResults<SolutionArgs>) = task {
        let solutionPath = res.GetResult(SolutionArgs.Path)
        let query = res.GetResult(SolutionArgs.Query, defaultValue = "")
        let scope = res.GetResult(SolutionArgs.Scope, defaultValue = SearchScope.Member)
        let limit = res.GetResult(SolutionArgs.Limit, defaultValue = 5)
        let maxDocChars = res.GetResult(SolutionArgs.Max_Doc_Chars, defaultValue = 1000)

        printfn "Indexing solution: %s..." solutionPath
        use workspace = MSBuildWorkspace.Create()
        let! model = SolutionProcessor.processSolution workspace solutionPath
        let index = SearchIndex(model)

        let scopeStr = match scope with SearchScope.Type -> "Type" | SearchScope.Member -> "Member"
        
        // Use the same logic as Mcp.fs but local
        let results = 
            if scopeStr.Equals("Type", StringComparison.OrdinalIgnoreCase) then
                let searchResults = index.SearchTypes(query, limit)
                searchResults |> List.map (fun r -> 
                    let doc = NuGexTools.Truncate r.Type.Documentation maxDocChars
                    {| FullName = r.FullName; Score = r.Score; Documentation = doc |} :> obj) |> List.toArray
            else
                let searchResults = index.SearchMembers(query, limit)
                searchResults |> List.map (fun r -> 
                    let rawDoc = 
                        match r.Member with
                        | Choice1Of2 m -> m.Documentation
                        | Choice2Of2 p -> p.Documentation
                    let doc = NuGexTools.Truncate rawDoc maxDocChars
                    {| FullName = r.FullName; ParentType = r.ParentTypeName; Score = r.Score; Documentation = doc |} :> obj) |> List.toArray
        
        printResults results
        return 0
    }

    let handleSearchPackage (res: ParseResults<PackageArgs>) = task {
        let packageName = res.GetResult(PackageArgs.Package_Id)
        let query = res.GetResult(PackageArgs.Query, defaultValue = "")
        let version = res.TryGetResult(PackageArgs.Version)
        let scope = res.GetResult(PackageArgs.Scope, defaultValue = SearchScope.Member)
        let limit = res.GetResult(PackageArgs.Limit, defaultValue = 5)
        let maxDocChars = res.GetResult(PackageArgs.Max_Doc_Chars, defaultValue = 1000)

        printfn "Indexing package: %s%s..." packageName (match version with Some v -> " v" + v | None -> "")
        let! model = PackageProcessor.processPackage packageName version
        let index = SearchIndex(model)

        let scopeStr = match scope with SearchScope.Type -> "Type" | SearchScope.Member -> "Member"

        let results = 
            if scopeStr.Equals("Type", StringComparison.OrdinalIgnoreCase) then
                let searchResults = index.SearchTypes(query, limit)
                searchResults |> List.map (fun r -> 
                    let doc = NuGexTools.Truncate r.Type.Documentation maxDocChars
                    {| FullName = r.FullName; Score = r.Score; Documentation = doc |} :> obj) |> List.toArray
            else
                let searchResults = index.SearchMembers(query, limit)
                searchResults |> List.map (fun r -> 
                    let rawDoc = 
                        match r.Member with
                        | Choice1Of2 m -> m.Documentation
                        | Choice2Of2 p -> p.Documentation
                    let doc = NuGexTools.Truncate rawDoc maxDocChars
                    {| FullName = r.FullName; ParentType = r.ParentTypeName; Score = r.Score; Documentation = doc |} :> obj) |> List.toArray
        
        printResults results
        return 0
    }

    let handleGetPackageReadme (res: ParseResults<PackageReadmeArgs>) = task {
        let packageName = res.GetResult(PackageReadmeArgs.Package_Id)
        let version = res.TryGetResult(PackageReadmeArgs.Version)
        
        let! readme = PackageProcessor.getPackageReadme packageName version
        printfn "%s" readme
        return 0
    }
