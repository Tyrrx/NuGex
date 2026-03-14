open System
open Microsoft.Build.Locator
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open ModelContextProtocol.Server
open NuGex

[<EntryPoint>]
let main argv =
    let isMcpMode = argv |> Array.exists (fun a -> a = "--mcp")

    if isMcpMode then
        MSBuildLocator.RegisterDefaults() |> ignore
        
        let builder = Host.CreateApplicationBuilder(argv)
        
        builder.Logging.AddConsole(fun options ->
            options.LogToStandardErrorThreshold <- LogLevel.Trace
        ) |> ignore

        builder.Services
            .AddSingleton<ISearchIndexCache, SearchIndexCache>()
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly()
            |> ignore

        let host = builder.Build()
        host.RunAsync().GetAwaiter().GetResult()
        0
    else
        // Demo Mode
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
            Console.WriteLine($"[Score: {result.Score}] {result.FullName} (in {result.ParentTypeName})")
            let doc = 
                match result.Member with
                | Choice1Of2 m -> m.Documentation
                | Choice2Of2 p -> p.Documentation
            if not (String.IsNullOrEmpty(doc)) then
                Console.WriteLine($"  Doc: {doc.Substring(0, Math.Min(100, doc.Length))}...")

        0
