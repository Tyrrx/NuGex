namespace NuGex

open System
open System.Collections.Concurrent
open System.Collections.Generic
open System.IO
open System.Text.Json
open System.Text.Json.Serialization
open System.Text.RegularExpressions
open System.Threading.Tasks
open Microsoft.CodeAnalysis.MSBuild

module Mcp =

    // --- JSON-RPC Types ---
    type JsonRpcRequest = {
        [<JsonPropertyName("jsonrpc")>] JsonRpc: string
        [<JsonPropertyName("id")>] Id: JsonElement
        [<JsonPropertyName("method")>] Method: string
        [<JsonPropertyName("params")>] Params: JsonElement
    }

    type JsonRpcResponse = {
        [<JsonPropertyName("jsonrpc")>] JsonRpc: string
        [<JsonPropertyName("id")>] Id: JsonElement
        [<JsonPropertyName("result")>] Result: obj
        [<JsonPropertyName("error")>] Error: obj
    }

    // --- MCP Tool Types ---
    type ToolDefinition = {
        name: string
        description: string
        inputSchema: obj
    }

    // --- State ---
    let private indexRegistry = ConcurrentDictionary<string, SearchIndex>()

    // --- Helpers ---
    let private stripXml (xml: string) =
        if String.IsNullOrWhiteSpace(xml) then ""
        else Regex.Replace(xml, "<.*?>", "")

    let private truncate (text: string) (max: int) =
        if String.IsNullOrWhiteSpace(text) then ""
        elif text.Length <= max then text
        else text.Substring(0, max) + "..."

    let private formatDoc (doc: string) (maxChars: int) =
        doc |> stripXml |> (fun s -> truncate s maxChars)

    let private hasProperty (p: JsonElement) (name: string) =
        let mutable prop = JsonElement()
        p.TryGetProperty(name, &prop)

    // --- Tool Implementations ---
    let private handleSearchSolution (p: JsonElement) = task {
        let solutionPath = p.GetProperty("solutionPath").GetString()
        let query = p.GetProperty("query").GetString()
        let scope = p.GetProperty("scope").GetString()
        let limit = if hasProperty p "limit" then p.GetProperty("limit").GetInt32() else 5
        let maxDocChars = if hasProperty p "maxDocChars" then p.GetProperty("maxDocChars").GetInt32() else 1000

        let! (index: SearchIndex) = 
            match indexRegistry.TryGetValue(solutionPath) with
            | true, idx -> Task.FromResult(idx)
            | _ -> task {
                Console.Error.WriteLine($"Indexing solution: {solutionPath}")
                use workspace = MSBuildWorkspace.Create()
                let! model = SolutionProcessor.processSolution workspace solutionPath
                let idx = SearchIndex(model)
                indexRegistry.TryAdd(solutionPath, idx) |> ignore
                return idx
            }

        if scope.Equals("Type", StringComparison.OrdinalIgnoreCase) then
            let results = index.SearchTypes(query, limit)
            let mapped = 
                results 
                |> List.map (fun r -> 
                    let doc = formatDoc r.Type.Documentation maxDocChars
                    {| FullName = r.FullName; Score = r.Score; Documentation = doc |})
            return (mapped :> obj)
        else
            let results = index.SearchMembers(query, limit)
            let mapped = 
                results 
                |> List.map (fun r -> 
                    let rawDoc = 
                        match r.Member with
                        | Choice1Of2 m -> m.Documentation
                        | Choice2Of2 p -> p.Documentation
                    let doc = formatDoc rawDoc maxDocChars
                    {| FullName = r.FullName; ParentType = r.ParentTypeName; Score = r.Score; Documentation = doc |})
            return (mapped :> obj)
    }

    let private handleSearchPackage (p: JsonElement) = task {
        let packageName = p.GetProperty("packageName").GetString()
        let query = p.GetProperty("query").GetString()
        let scope = p.GetProperty("scope").GetString()
        let version = if hasProperty p "packageVersion" then Some(p.GetProperty("packageVersion").GetString()) else None
        let limit = if hasProperty p "limit" then p.GetProperty("limit").GetInt32() else 5
        let maxDocChars = if hasProperty p "maxDocChars" then p.GetProperty("maxDocChars").GetInt32() else 1000

        let key = match version with Some v -> $"{packageName}:{v}" | None -> packageName
        let! (index: SearchIndex) = 
            match indexRegistry.TryGetValue(key) with
            | true, idx -> Task.FromResult(idx)
            | _ -> task {
                Console.Error.WriteLine($"Indexing package: {key}")
                let! model = PackageProcessor.processPackage packageName version
                let idx = SearchIndex(model)
                indexRegistry.TryAdd(key, idx) |> ignore
                return idx
            }

        if scope.Equals("Type", StringComparison.OrdinalIgnoreCase) then
            let results = index.SearchTypes(query, limit)
            let mapped = 
                results 
                |> List.map (fun r -> 
                    let doc = formatDoc r.Type.Documentation maxDocChars
                    {| FullName = r.FullName; Score = r.Score; Documentation = doc |})
            return (mapped :> obj)
        else
            let results = index.SearchMembers(query, limit)
            let mapped = 
                results 
                |> List.map (fun r -> 
                    let rawDoc = 
                        match r.Member with
                        | Choice1Of2 m -> m.Documentation
                        | Choice2Of2 p -> p.Documentation
                    let doc = formatDoc rawDoc maxDocChars
                    {| FullName = r.FullName; ParentType = r.ParentTypeName; Score = r.Score; Documentation = doc |})
            return (mapped :> obj)
    }

    // --- Main Loop ---
    let start () = task {
        let options = JsonSerializerOptions(WriteIndented = false)
        let mutable exit = false

        while not exit do
            let line = Console.ReadLine()
            if line = null then 
                exit <- true
            else
                try
                    let request = JsonSerializer.Deserialize<JsonRpcRequest>(line)
                    let! (result: obj) = 
                        match request.Method with
                        | "initialize" -> 
                            let res = 
                                {| protocolVersion = "2024-11-05"
                                   capabilities = {| tools = obj() |}
                                   serverInfo = {| name = "NuGex"; version = "1.0.0" |} |}
                            Task.FromResult(res :> obj)
                        | "list_tools" -> 
                            let t1 = 
                                { name = "search_solution"
                                  description = "Fuzzy search types or members in a .NET solution"
                                  inputSchema = 
                                    {| ``type`` = "object"
                                       properties = 
                                        {| solutionPath = {| ``type`` = "string" |}
                                           query = {| ``type`` = "string" |}
                                           scope = {| ``type`` = "string"; enum = [| "Type"; "Member" |] |}
                                           limit = {| ``type`` = "integer" |}
                                           maxDocChars = {| ``type`` = "integer" |} |}
                                       required = [| "solutionPath"; "query"; "scope" |] |} }
                            let t2 = 
                                { name = "search_package"
                                  description = "Fuzzy search types or members in a NuGet package"
                                  inputSchema = 
                                    {| ``type`` = "object"
                                       properties = 
                                        {| packageName = {| ``type`` = "string" |}
                                           packageVersion = {| ``type`` = "string" |}
                                           query = {| ``type`` = "string" |}
                                           scope = {| ``type`` = "string"; enum = [| "Type"; "Member" |] |}
                                           limit = {| ``type`` = "integer" |}
                                           maxDocChars = {| ``type`` = "integer" |} |}
                                       required = [| "packageName"; "query"; "scope" |] |} }
                            let res = {| tools = [| t1; t2 |] |}
                            Task.FromResult(res :> obj)
                        | "call_tool" -> 
                            let toolName = request.Params.GetProperty("name").GetString()
                            let toolParams = request.Params.GetProperty("arguments")
                            if toolName = "search_solution" then handleSearchSolution toolParams
                            elif toolName = "search_package" then handleSearchPackage toolParams
                            else Task.FromResult(null :> obj)
                        | _ -> Task.FromResult(null :> obj)

                    let response = { JsonRpc = "2.0"; Id = request.Id; Result = result; Error = null }
                    Console.WriteLine(JsonSerializer.Serialize(response, options))
                with ex ->
                    Console.Error.WriteLine($"Error processing request: {ex.Message}")
    }
