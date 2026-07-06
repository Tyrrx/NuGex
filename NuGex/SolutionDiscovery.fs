namespace NuGex

open System
open System.IO
open Microsoft.Extensions.Logging

module SolutionDiscovery =

    let skippedDirNames =
        set [ "bin"; "obj"; ".git"; ".vs"; "node_modules" ]

    let private isSkippedDir (dir: DirectoryInfo) =
        skippedDirNames.Contains(dir.Name.ToLowerInvariant())

    let rec private walk (logger: ILogger) (dir: DirectoryInfo) : string seq =
        seq {
            try
                for file in dir.EnumerateFiles() do
                    if file.Extension.Equals(".sln", StringComparison.OrdinalIgnoreCase)
                       || file.Extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase) then
                        yield file.FullName

                for subDir in dir.EnumerateDirectories() do
                    if not (isSkippedDir subDir) then
                        yield! walk logger subDir
            with
            | (:? UnauthorizedAccessException | :? IOException) as ex ->
                logger.LogWarning(ex, "Skipping inaccessible directory: {Directory}", dir.FullName)
        }

    let findSolutionFiles (logger: ILogger) (root: string) : string list =
        walk logger (DirectoryInfo(root)) |> List.ofSeq

    let pickSolution (files: string list) : string option =
        files
        |> List.sortBy (fun f -> f.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Length, f)
        |> List.tryHead
