open System
open Microsoft.Build.Locator
open Microsoft.CodeAnalysis.MSBuild
open NuSkill

[<EntryPoint>]
let main argv =
    let isMcpMode = argv |> Array.exists (fun a -> a = "--mcp")

    if isMcpMode then
        // MCP Mode: JSON-RPC over stdio
        MSBuildLocator.RegisterDefaults() |> ignore
        Mcp.start().Wait()
        0
    else
        // Demo Mode: BouncyCastle example
        let packageName = "BouncyCastle.NetCore"

        MSBuildLocator.RegisterDefaults() |> ignore
        
        let task = PackageProcessor.processPackage packageName None
        let model = task.Result
        
        let index = SearchIndex(model)

        Console.WriteLine("--- Fuzzy Type Search: 'AesEngine' ---")
        let typeResults = index.SearchTypes("AesEngine", 3)
        for result in typeResults do
            Console.WriteLine($"[Score: {result.Score}] {result.FullName}")
            if not (String.IsNullOrEmpty(result.Type.Documentation)) then
                Console.WriteLine($"  Doc: {result.Type.Documentation.Substring(0, Math.Min(100, result.Type.Documentation.Length))}...")

        Console.WriteLine("\n--- Fuzzy Member Search: 'Init' ---")
        let memberResults = index.SearchMembers("Init", 5)
        for result in memberResults do
            let name = 
                match result.Member with
                | Choice1Of2 m -> m.Name
                | Choice2Of2 p -> p.Name
            Console.WriteLine($"[Score: {result.Score}] {result.FullName} (in {result.ParentTypeName})")
            let doc = 
                match result.Member with
                | Choice1Of2 m -> m.Documentation
                | Choice2Of2 p -> p.Documentation
            if not (String.IsNullOrEmpty(doc)) then
                Console.WriteLine($"  Doc: {doc.Substring(0, Math.Min(100, doc.Length))}...")

        0
