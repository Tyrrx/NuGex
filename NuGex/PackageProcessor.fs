namespace NuGex

open System
open System.IO
open System.Linq
open System.Threading
open System.Threading.Tasks
open NuGet.Common
open NuGet.Configuration
open NuGet.Credentials
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
    let private frameworkReducer = FrameworkReducer()

    /// Returns the credentials configured for a package source (packageSourceCredentials in NuGet.Config).
    /// Without a wired ICredentialService, HttpSourceAuthenticationHandler short-circuits on 401 and never
    /// retries with the credentials already attached to the PackageSource, so authenticated feeds always fail.
    type private SettingsCredentialProvider(sources: PackageSource list) =
        let credsByUri =
            sources
            |> List.choose (fun s ->
                if isNull s.Credentials || not (s.Credentials.IsValid()) then None
                else Some (s.SourceUri, s.Credentials.ToICredentials()))
            |> dict

        interface ICredentialProvider with
            member _.Id = "NuGex.SettingsCredentialProvider"
            member _.GetAsync(uri: Uri, _proxy, _type, _message, _isRetry, _nonInteractive, _ct) =
                match credsByUri.TryGetValue uri with
                | true, creds -> Task.FromResult(CredentialResponse(creds))
                | _ -> Task.FromResult(CredentialResponse(CredentialStatus.ProviderNotApplicable))

    let private repositories =
        let sources =
            Settings.LoadDefaultSettings(Environment.CurrentDirectory)
            |> PackageSourceProvider
            |> fun p -> p.LoadPackageSources() |> Seq.toList

        let enabledSources = sources |> List.filter (fun s -> s.IsEnabled)

        // Wire the settings-backed credentials into the HTTP layer so authenticated feeds work.
        let providers =
            AsyncLazy<IEnumerable<ICredentialProvider>>(fun () ->
                Task.FromResult([ SettingsCredentialProvider(enabledSources) :> ICredentialProvider ] :> IEnumerable<ICredentialProvider>))
        HttpHandlerResourceV3.CredentialService <-
            Lazy<ICredentialService>(fun () ->
                CredentialService(providers, nonInteractive = true, handlesDefaultCredentials = false) :> ICredentialService)

        // Fallback if no sources are configured: default to nuget.org so existing behavior never regresses.
        if List.isEmpty enabledSources then
            [| Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json") |]
        else
            enabledSources
            |> List.map (fun s -> Repository.Factory.GetCoreV3(s.Source))
            |> List.toArray

    /// The framework NuGex itself targets, used as the reference point for selecting
    /// the "best" lib group from a package that ships multiple target frameworks.
    let private currentFramework =
        let attr = Assembly.GetEntryAssembly().GetCustomAttribute<TargetFrameworkAttribute>()
        NuGetFramework.ParseFrameworkName(attr.FrameworkName, DefaultFrameworkNameProvider.Instance)

    let rec private getLatestVersionFrom (packageName: string) (index: int) = task {
        if index >= repositories.Length then
            return None
        else
            let repository = repositories.[index]
            try
                let! resource = repository.GetResourceAsync<MetadataResource>()
                let! versions = resource.GetVersions(packageName, cache, logger, CancellationToken.None)
                let best =
                    versions
                    |> Seq.filter (fun v -> not v.IsPrerelease)
                    |> Seq.sortDescending
                    |> Seq.tryHead
                match best with
                | Some _ -> return best
                | None -> return! getLatestVersionFrom packageName (index + 1)
            with _ -> return! getLatestVersionFrom packageName (index + 1)
    }

    let private getLatestVersion (packageName: string) = task {
        return! getLatestVersionFrom packageName 0
    }

    let rec private downloadPackageFrom (packageName: string) (version: string option) (index: int) = task {
        if not (PackageIdValidator.IsValidPackageId(packageName)) then
            invalidArg (nameof packageName) $"'{packageName}' is not a valid NuGet package ID."

        if index >= repositories.Length then
            return None
        else
            let repository = repositories.[index]
            try
                let! nugetVersion =
                    match version with
                    | Some v -> Task.FromResult(Some (NuGetVersion.Parse(v)))
                    | None -> getLatestVersion packageName

                match nugetVersion with
                | None -> return! downloadPackageFrom packageName version (index + 1)
                | Some v ->
                    let tempFolder = Path.Combine(Path.GetTempPath(), "NuGex", $"{packageName}.{v}")
                    if not (Directory.Exists(tempFolder)) then
                        Directory.CreateDirectory(tempFolder) |> ignore

                    let nupkgPath = Path.Combine(tempFolder, $"{packageName}.{v}.nupkg")
                    if not (File.Exists(nupkgPath)) || (FileInfo(nupkgPath).Length = 0L) then
                        let! downloadResource = repository.GetResourceAsync<FindPackageByIdResource>()
                        use fs = new FileStream(nupkgPath, FileMode.Create)
                        let! copied = downloadResource.CopyNupkgToStreamAsync(packageName, v, fs, cache, logger, CancellationToken.None)
                        if not copied then
                            File.Delete(nupkgPath)
                            return! downloadPackageFrom packageName version (index + 1)
                        else
                            return Some (nupkgPath, tempFolder)
                    else
                        return Some (nupkgPath, tempFolder)
            with _ -> return! downloadPackageFrom packageName version (index + 1)
    }

    let private downloadPackage (packageName: string) (version: string option) = task {
        return! downloadPackageFrom packageName version 0
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
                    RoslynApiVisitor.visitNamespace assemblySymbol.GlobalNamespace apiTypes
                    
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
