open System
open Microsoft.Build.Locator
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open ModelContextProtocol.Server
open NuGex
open Argu

[<EntryPoint>]
let main argv =
    let parser = ArgumentParser.Create<Arguments>(programName = "nugex")
    
    try
        let results = parser.Parse(argv)
        
        // Register MSBuild before any indexing logic
        if not (MSBuildLocator.IsRegistered) then
            MSBuildLocator.RegisterDefaults() |> ignore

        if results.Contains(Mcp) then
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
            match results.GetSubCommand() with
            | Search_Solution res -> 
                (CliHandler.handleSearchSolution res).GetAwaiter().GetResult()
            | Search_Package res -> 
                (CliHandler.handleSearchPackage res).GetAwaiter().GetResult()
            | Get_Package_Readme res ->
                (CliHandler.handleGetPackageReadme res).GetAwaiter().GetResult()
            | _ -> 
                printfn "%s" (parser.PrintUsage())
                0
    with 
    | :? ArguParseException as ex -> 
        printfn "%s" ex.Message
        1
    | ex ->
        printfn "Error: %s" ex.Message
        1
