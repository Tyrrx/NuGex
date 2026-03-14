namespace NuGex

open System.Collections.Generic

type ApiMember = {
    Name: string
    Documentation: string
    DisplayString: string
}

type ApiProperty = {
    Name: string
    Documentation: string
    Type: string
}

type ApiType = {
    Name: string
    Documentation: string
    Members: Dictionary<string, ApiMember>
    Properties: Dictionary<string, ApiProperty>
}

type ApiAssembly = {
    Name: string
    Version: string
    Types: Dictionary<string, ApiType>
}

type ApiModel = {
    Assemblies: Dictionary<string, ApiAssembly>
}
