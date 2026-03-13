namespace NuSkill

open System
open System.IO
open System.Linq
open System.Threading
open System.Threading.Tasks
open NuGet.Common
open NuGet.Protocol
open NuGet.Protocol.Core.Types
open NuGet.Versioning
open NuGet.Packaging
open Microsoft.CodeAnalysis
open System.Collections.Generic
open System.IO.Compression

module PackageProcessor =

    let private logger = NullLogger.Instance
    let private cache = new SourceCacheContext()
    let private repository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json")

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

    let private getLatestVersion (packageName: string) = task {
        let! resource = repository.GetResourceAsync<MetadataResource>()
        let! versions = resource.GetVersions(packageName, cache, logger, CancellationToken.None)
        return versions 
               |> Seq.filter (fun v -> not v.IsPrerelease)
               |> Seq.sortDescending
               |> Seq.tryHead
    }

    let processPackage (packageName: string) (version: string option) = task {
        let! nugetVersion = 
            match version with
            | Some v -> Task.FromResult(Some (NuGetVersion.Parse(v)))
            | None -> getLatestVersion packageName

        match nugetVersion with
        | None -> Console.WriteLine($"Could not find version for package {packageName}")
        | Some v ->
            Console.WriteLine($"Processing package {packageName} version {v}...")
            let! downloadResource = repository.GetResourceAsync<FindPackageByIdResource>()
            let tempFolder = Path.Combine(Path.GetTempPath(), "NuSkill", $"{packageName}.{v}")
            if Directory.Exists(tempFolder) then Directory.Delete(tempFolder, true)
            Directory.CreateDirectory(tempFolder) |> ignore

            let nupkgPath = Path.Combine(tempFolder, $"{packageName}.{v}.nupkg")
            using (new FileStream(nupkgPath, FileMode.Create)) (fun fs ->
                downloadResource.CopyNupkgToStreamAsync(packageName, v, fs, cache, logger, CancellationToken.None).Wait()
            )

            use packageReader = new PackageArchiveReader(nupkgPath)
            let libFiles = packageReader.GetLibItems() |> Seq.toList
            
            let bestGroup = libFiles |> Seq.sortByDescending (fun g -> g.TargetFramework.DotNetFrameworkName) |> Seq.tryHead
            
            match bestGroup with
            | None -> Console.WriteLine("No library files found in package.")
            | Some group ->
                let extractPath = Path.Combine(tempFolder, "lib")
                Directory.CreateDirectory(extractPath) |> ignore
                
                let dlls = new List<string>()
                let xmls = new Dictionary<string, string>()

                for item in group.Items do
                    let entry = packageReader.GetEntry(item)
                    let targetFile = Path.Combine(extractPath, Path.GetFileName(item))
                    entry.ExtractToFile(targetFile, true)
                    if targetFile.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) then
                        dlls.Add(targetFile)
                    elif targetFile.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) then
                        xmls.[Path.GetFileNameWithoutExtension(targetFile)] <- targetFile

                for dll in dlls do
                    let assemblyName = Path.GetFileNameWithoutExtension(dll)
                    let xmlPath = if xmls.ContainsKey(assemblyName) then xmls.[assemblyName] else null
                    
                    let documentationProvider = 
                        if not (String.IsNullOrEmpty(xmlPath)) then
                            XmlDocumentationProvider.CreateFromFile(xmlPath)
                        else
                            null

                    let reference = MetadataReference.CreateFromFile(dll, documentation = documentationProvider)
                    let compilation = Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create(assemblyName)
                                        .AddReferences(reference)
                    
                    let assemblySymbol = compilation.GetAssemblyOrModuleSymbol(reference) :?> IAssemblySymbol
                    Console.WriteLine($"--- Package Assembly: {assemblySymbol.Name} ({assemblySymbol.Identity.Version}) ---")
                    visitNamespace assemblySymbol.GlobalNamespace
    }
