namespace NuGex

open System
open System.IO

module SolutionDiscovery =

    let private skippedDirNames =
        set [ "bin"; "obj"; ".git"; ".vs"; "node_modules" ]

    let private isSkippedDir (dir: DirectoryInfo) =
        skippedDirNames.Contains(dir.Name.ToLowerInvariant())

    let rec private walk (dir: DirectoryInfo) : string seq =
        seq {
            for file in dir.EnumerateFiles() do
                if file.Extension.Equals(".sln", StringComparison.OrdinalIgnoreCase)
                   || file.Extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase) then
                    yield file.FullName

            for subDir in dir.EnumerateDirectories() do
                if not (isSkippedDir subDir) then
                    yield! walk subDir
        }

    let findSolutionFiles (root: string) : string list =
        walk (DirectoryInfo(root)) |> List.ofSeq

    let pickSolution (files: string list) : string option =
        files
        |> List.sortBy (fun f -> f.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Length, f)
        |> List.tryHead
