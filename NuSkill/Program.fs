open System
open Microsoft.Build.Locator
open Microsoft.CodeAnalysis.MSBuild
open NuSkill

[<EntryPoint>]
let main argv =
    let packageName = "BouncyCastle.NetCore"

    MSBuildLocator.RegisterDefaults() |> ignore
    
    let task = PackageProcessor.processPackage packageName None
    let model = task.Result
    
    for KeyValue(identity, assembly) in model.Assemblies do
        Console.WriteLine($"--- Assembly: {assembly.Name} ({assembly.Version}) ---")
        for KeyValue(typeFullName, apiType) in assembly.Types do
            if not (String.IsNullOrEmpty(apiType.Documentation)) then
                Console.WriteLine($"Type: {typeFullName}")
                Console.WriteLine($"Doc: {apiType.Documentation}")
            
            for KeyValue(memberFullName, apiMember) in apiType.Members do
                if not (String.IsNullOrEmpty(apiMember.Documentation)) then
                    Console.WriteLine($"  Member: {memberFullName}")
                    Console.WriteLine($"  Doc: {apiMember.Documentation}")

    0
