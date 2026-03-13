open System
open Microsoft.Build.Locator
open Microsoft.CodeAnalysis.MSBuild
open NuSkill

[<EntryPoint>]
let main argv =
    let targetPath = @"/home/drtz/repos/bluehands/WebFinger-Server-OidcDiscovery/src/WebFinger.Server.OidcDiscovery/WebFinger.Server.OidcDiscovery.sln"    

    MSBuildLocator.RegisterDefaults() |> ignore

    use workspace = MSBuildWorkspace.Create()
    
    let task = PackageProcessor.processPackage "BouncyCastle.NetCore" None
    task.Wait()
    0