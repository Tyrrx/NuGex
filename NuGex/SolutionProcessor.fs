namespace NuGex

open System
open System.Collections.Generic
open Microsoft.CodeAnalysis
open Microsoft.CodeAnalysis.MSBuild
open System.Threading.Tasks

module SolutionProcessor =

    let processSolution (workspace: MSBuildWorkspace) (targetPath: string) = task {
        let! (projects: IEnumerable<Project>) = 
            task {
                if targetPath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase)
                   || targetPath.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase) then
                    let! solution = workspace.OpenSolutionAsync(targetPath)
                    return solution.Projects
                else
                    let! project = workspace.OpenProjectAsync(targetPath)
                    return [| project |] :> IEnumerable<Project>
            }
        
        let model = { Assemblies = Dictionary<string, ApiAssembly>() }
        let processedAssemblies = HashSet<string>()

        for project in projects do
            let! compilation = project.GetCompilationAsync()
            
            if compilation <> null then
                for reference in compilation.References do
                    let symbol = compilation.GetAssemblyOrModuleSymbol(reference)
                    match symbol with
                    | :? IAssemblySymbol as assemblySymbol ->
                        let identity = assemblySymbol.Identity.ToString()
                        if processedAssemblies.Add(identity) then
                            let apiTypes = Dictionary<string, ApiType>()
                            RoslynApiVisitor.visitNamespace assemblySymbol.GlobalNamespace apiTypes
                            
                            model.Assemblies.[identity] <- {
                                Name = assemblySymbol.Name
                                Version = assemblySymbol.Identity.Version.ToString()
                                Types = apiTypes
                            }
                    | _ -> ()
        return model
    }
