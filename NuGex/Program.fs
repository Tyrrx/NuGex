open System
open System.IO
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

            let cwd = Directory.GetCurrentDirectory()

            // Discovery runs before the host is built, so it can't resolve an ILogger from DI yet;
            // this bootstrap factory mirrors the console config registered above for the eventual host.
            use discoveryLoggerFactory =
                LoggerFactory.Create(fun lb ->
                    lb.AddConsole(fun options -> options.LogToStandardErrorThreshold <- LogLevel.Trace) |> ignore)
            let discoveryLogger = discoveryLoggerFactory.CreateLogger("SolutionDiscovery")

            let solutionFiles = SolutionDiscovery.findSolutionFiles discoveryLogger cwd
            let solutionPath = SolutionDiscovery.pickSolution solutionFiles

            match solutionFiles with
            | [] ->
                eprintfn "No .sln/.slnx found under %s; search_solution tool will not be available." cwd
            | [ single ] ->
                eprintfn "Using solution: %s" single
            | multiple ->
                eprintfn "Multiple solutions found under %s: %s" cwd (String.Join(", ", multiple))
                eprintfn "Using solution: %s" (Option.get solutionPath)

            builder.Services.AddSingleton<ISearchIndexCache, SearchIndexCache>() |> ignore

            let mcpBuilder =
                builder.Services
                    .AddMcpServer()
                    .WithStdioServerTransport()
                    .WithTools<PackageTools>()

            match solutionPath with
            | Some path ->
                builder.Services.AddSingleton<SolutionContext>({ SolutionPath = path }) |> ignore
                builder.Services.AddSingleton<SolutionIndexWatcher>(fun sp ->
                    new SolutionIndexWatcher(cwd, sp.GetRequiredService<SolutionContext>(), sp.GetRequiredService<ISearchIndexCache>(), sp.GetRequiredService<ILogger<SolutionIndexWatcher>>())) |> ignore
                mcpBuilder.WithTools<SolutionTools>() |> ignore
            | None -> ()

            let host = builder.Build()

            // Force eager construction so the watcher starts immediately and is disposed with the host.
            if solutionPath.IsSome then
                host.Services.GetRequiredService<SolutionIndexWatcher>() |> ignore

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
