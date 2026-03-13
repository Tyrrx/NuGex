namespace NuSkill

open System.Collections.Generic

type ApiMember = {
    Name: string
    Documentation: string
    DisplayString: string
}

type ApiType = {
    Name: string
    Documentation: string
    Members: Dictionary<string, ApiMember>
}

type ApiAssembly = {
    Name: string
    Version: string
    Types: Dictionary<string, ApiType>
}

type ApiModel = {
    Assemblies: Dictionary<string, ApiAssembly>
}
