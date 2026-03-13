namespace NuSkill

open System
open System.Collections.Generic
open Microsoft.CodeAnalysis
open Microsoft.CodeAnalysis.MSBuild
open System.Threading.Tasks

module SolutionProcessor =

    let rec private visitType (typeSymbol: INamedTypeSymbol) =
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
                        let displayString = memberSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
                        Console.WriteLine($"  Member: {displayString}")
                        Console.WriteLine($"  Doc: {memberDoc.Trim()}")
            
            // Visit nested types
            for nestedType in typeSymbol.GetTypeMembers() do
                visitType nestedType

    let rec private visitNamespace (ns: INamespaceSymbol) =
        for typeSymbol in ns.GetTypeMembers() do
            visitType typeSymbol
        
        for childNs in ns.GetNamespaceMembers() do
            visitNamespace childNs

    let processSolution (workspace: MSBuildWorkspace) (targetPath: string) = task {
        let! solution = workspace.OpenSolutionAsync(targetPath)
        let processedAssemblies = HashSet<string>()

        for project in solution.Projects do
            let! compilation = project.GetCompilationAsync()
            
            if compilation <> null then
                for reference in compilation.References do
                    let symbol = compilation.GetAssemblyOrModuleSymbol(reference)
                    match symbol with
                    | :? IAssemblySymbol as assemblySymbol ->
                        if processedAssemblies.Add(assemblySymbol.Identity.ToString()) then
                            Console.WriteLine($"--- Assembly: {assemblySymbol.Name} ({assemblySymbol.Identity.Version}) ---")
                            visitNamespace assemblySymbol.GlobalNamespace
                    | _ -> ()
    }
