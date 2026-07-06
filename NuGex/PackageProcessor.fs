namespace NuGex

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
open NuGet.Frameworks
open Microsoft.CodeAnalysis
open System.Collections.Generic
open System.IO.Compression
open System.Reflection
open System.Runtime.Versioning

module PackageProcessor =

    let private logger = NullLogger.Instance
    let private cache = new SourceCacheContext()
    let private repository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json")
    let private frameworkReducer = FrameworkReducer()

    /// The framework NuGex itself targets, used as the reference point for selecting
    /// the "best" lib group from a package that ships multiple target frameworks.
    let private currentFramework =
        let attr = Assembly.GetEntryAssembly().GetCustomAttribute<TargetFrameworkAttribute>()
        NuGetFramework.ParseFrameworkName(attr.FrameworkName, DefaultFrameworkNameProvider.Instance)

    let rec private visitType (typeSymbol: INamedTypeSymbol) (types: Dictionary<string, ApiType>) =
        if typeSymbol.DeclaredAccessibility = Accessibility.Public then
            let typeDoc = typeSymbol.GetDocumentationCommentXml()
            let fullName = typeSymbol.ToDisplayString()
            
            let members = Dictionary<string, ApiMember>()
            let properties = Dictionary<string, ApiProperty>()
            
            // Visit members and properties
            for memberSymbol in typeSymbol.GetMembers() do
                if memberSymbol.DeclaredAccessibility = Accessibility.Public then
                    let memberDoc = memberSymbol.GetDocumentationCommentXml()
                    let memberFullName = memberSymbol.ToDisplayString()
                    let doc = if String.IsNullOrWhiteSpace(memberDoc) then "" else memberDoc.Trim()

                    match memberSymbol with
                    | :? IPropertySymbol as propertySymbol ->
                        properties.[memberFullName] <- {
                            Name = propertySymbol.Name
                            Documentation = doc
                            Type = propertySymbol.Type.ToDisplayString()
                        }
                    | _ ->
                        let displayString = memberSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
                        members.[memberFullName] <- { 
                            Name = memberSymbol.Name; 
                            Documentation = doc; 
                            DisplayString = displayString 
                        }
            
            types.[fullName] <- { 
                Name = typeSymbol.Name; 
                Documentation = if String.IsNullOrWhiteSpace(typeDoc) then "" else typeDoc.Trim(); 
                Members = members
                Properties = properties
            }
            
            // Visit nested types
            for nestedType in typeSymbol.GetTypeMembers() do
                visitType nestedType types

    let rec private visitNamespace (ns: INamespaceSymbol) (types: Dictionary<string, ApiType>) =
        for typeSymbol in ns.GetTypeMembers() do
            visitType typeSymbol types
        
        for childNs in ns.GetNamespaceMembers() do
            visitNamespace childNs types

    let private getLatestVersion (packageName: string) = task {
        let! resource = repository.GetResourceAsync<MetadataResource>()
        let! versions = resource.GetVersions(packageName, cache, logger, CancellationToken.None)
        return versions 
               |> Seq.filter (fun v -> not v.IsPrerelease)
               |> Seq.sortDescending
               |> Seq.tryHead
    }

    let private downloadPackage (packageName: string) (version: string option) = task {
        let! nugetVersion = 
            match version with
            | Some v -> Task.FromResult(Some (NuGetVersion.Parse(v)))
            | None -> getLatestVersion packageName

        match nugetVersion with
        | None -> return None
        | Some v ->
            let tempFolder = Path.Combine(Path.GetTempPath(), "NuGex", $"{packageName}.{v}")
            if not (Directory.Exists(tempFolder)) then
                Directory.CreateDirectory(tempFolder) |> ignore

            let nupkgPath = Path.Combine(tempFolder, $"{packageName}.{v}.nupkg")
            if not (File.Exists(nupkgPath)) then
                let! downloadResource = repository.GetResourceAsync<FindPackageByIdResource>()
                use fs = new FileStream(nupkgPath, FileMode.Create)
                let! _ = downloadResource.CopyNupkgToStreamAsync(packageName, v, fs, cache, logger, CancellationToken.None)
                ()
            
            return Some (nupkgPath, tempFolder)
    }

    let processPackage (packageName: string) (version: string option) = task {
        let! packageInfo = downloadPackage packageName version
        let model = { Assemblies = Dictionary<string, ApiAssembly>() }

        match packageInfo with
        | None -> return model
        | Some (nupkgPath, tempFolder) ->
            use packageReader = new PackageArchiveReader(nupkgPath)
            let libFiles = packageReader.GetLibItems() |> Seq.toList
            
            let bestGroup =
                match frameworkReducer.GetNearest(currentFramework, libFiles |> Seq.map (fun g -> g.TargetFramework)) with
                | null -> libFiles |> Seq.tryHead
                | nearest -> libFiles |> Seq.tryFind (fun g -> g.TargetFramework.Equals(nearest))
            
            match bestGroup with
            | None -> ()
            | Some group ->
                let extractPath = Path.Combine(tempFolder, "lib")
                if not (Directory.Exists(extractPath)) then
                    Directory.CreateDirectory(extractPath) |> ignore
                
                let dlls = new List<string>()
                let xmls = new Dictionary<string, string>()

                for item in group.Items do
                    let targetFile = Path.Combine(extractPath, Path.GetFileName(item))
                    if not (File.Exists(targetFile)) then
                        let entry = packageReader.GetEntry(item)
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
                    let identity = assemblySymbol.Identity.ToString()
                    let apiTypes = Dictionary<string, ApiType>()
                    visitNamespace assemblySymbol.GlobalNamespace apiTypes
                    
                    model.Assemblies.[identity] <- {
                        Name = assemblySymbol.Name
                        Version = assemblySymbol.Identity.Version.ToString()
                        Types = apiTypes
                    }
            return model
    }

    let getPackageReadme (packageName: string) (version: string option) = task {
        let! packageInfo = downloadPackage packageName version
        match packageInfo with
        | None -> return "Package not found."
        | Some (nupkgPath, _) ->
            use packageReader = new PackageArchiveReader(nupkgPath)
            let nuspec = packageReader.NuspecReader
            let readmePath = nuspec.GetReadme()
            
            let entry = 
                if not (String.IsNullOrWhiteSpace(readmePath)) then
                    packageReader.GetEntry(readmePath)
                else
                    // Fallback: search for files named readme.md or readme.txt in the root
                    // GetFiles returns all files in the package
                    packageReader.GetFiles()
                    |> Seq.tryFind (fun f -> 
                        let name = Path.GetFileName(f).ToLowerInvariant()
                        name.StartsWith("readme") && (name.EndsWith(".md") || name.EndsWith(".txt")))
                    |> Option.map packageReader.GetEntry
                    |> Option.toObj

            if isNull entry then
                return "No README file found in the package."
            else
                use stream = entry.Open()
                use reader = new StreamReader(stream)
                return! reader.ReadToEndAsync()
    }
