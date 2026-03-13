open System
open System.Collections.Generic
open Microsoft.Build.Locator
open Microsoft.CodeAnalysis.MSBuild
open Microsoft.CodeAnalysis

[<EntryPoint>]
let main argv =
    let targetPath = @"/home/drtz/repos/bluehands/WebFinger-Server-OidcDiscovery/src/WebFinger.Server.OidcDiscovery/WebFinger.Server.OidcDiscovery.sln"    

    MSBuildLocator.RegisterDefaults() |> ignore

    use workspace = MSBuildWorkspace.Create()
    
    // Track processed assemblies to avoid duplicates
    let processedAssemblies = HashSet<string>()

    let processSolution = task {
        let! solution = workspace.OpenSolutionAsync(targetPath)

        for project in solution.Projects do
            let! compilation = project.GetCompilationAsync()
            
            if compilation <> null then
                for reference in compilation.References do
                    let symbol = compilation.GetAssemblyOrModuleSymbol(reference)
                    match symbol with
                    | :? IAssemblySymbol as assemblySymbol ->
                        if processedAssemblies.Add(assemblySymbol.Identity.ToString()) then
                            Console.WriteLine($"--- Assembly: {assemblySymbol.Name} ({assemblySymbol.Identity.Version}) ---")
                            
                            let rec visitType (typeSymbol: INamedTypeSymbol) =
                                if typeSymbol.DeclaredAccessibility = Accessibility.Public then
                                    let typeDoc = typeSymbol.GetDocumentationCommentXml()
                                    if not (String.IsNullOrWhiteSpace(typeDoc)) then
                                        Console.WriteLine($"Type: {typeSymbol.ToDisplayString()}")
                                        Console.WriteLine($"Doc: {typeDoc.Trim()}")
                                    
                                    // Visit members
                                    for memberSymbol in typeSymbol.GetMembers() do
                                        if memberSymbol.DeclaredAccessibility = Accessibility.Public then
                                            let memberDoc = memberSymbol.GetDocumentationCommentXml()
                                            if not (String.IsNullOrWhiteSpace(memberDoc)) then
                                                Console.WriteLine($"  Member: {memberSymbol.ToDisplayString()}")
                                                Console.WriteLine($"  Doc: {memberDoc.Trim()}")
                                    
                                    // Visit nested types
                                    for nestedType in typeSymbol.GetTypeMembers() do
                                        visitType nestedType

                            let rec visitNamespace (ns: INamespaceSymbol) =
                                for typeSymbol in ns.GetTypeMembers() do
                                    visitType typeSymbol
                                
                                for childNs in ns.GetNamespaceMembers() do
                                    visitNamespace childNs
                            
                            visitNamespace assemblySymbol.GlobalNamespace
                    | _ -> ()
    }

    processSolution.Wait()
    0