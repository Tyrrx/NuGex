namespace NuGex

open System
open System.Collections.Generic
open FuzzySharp

type TypeSearchEntry = {
    FullName: string
    Type: ApiType
}

type MemberSearchEntry = {
    FullName: string
    ParentTypeName: string
    Member: Choice<ApiMember, ApiProperty>
}

type TypeSearchResult = {
    Type: ApiType
    FullName: string
    Score: int
}

type MemberSearchResult = {
    Member: Choice<ApiMember, ApiProperty>
    FullName: string
    ParentTypeName: string
    Score: int
}

type SearchIndex(model: ApiModel) =
    let typeEntries = List<TypeSearchEntry>()
    let memberEntries = List<MemberSearchEntry>()

    do
        for KeyValue(_, assembly) in model.Assemblies do
            for KeyValue(typeFullName, apiType) in assembly.Types do
                typeEntries.Add({ FullName = typeFullName; Type = apiType })
                
                for KeyValue(memberFullName, apiMember) in apiType.Members do
                    memberEntries.Add({ 
                        FullName = memberFullName
                        ParentTypeName = typeFullName
                        Member = Choice1Of2 apiMember 
                    })
                
                for KeyValue(propFullName, apiProp) in apiType.Properties do
                    memberEntries.Add({ 
                        FullName = propFullName
                        ParentTypeName = typeFullName
                        Member = Choice2Of2 apiProp 
                    })

    member _.SearchTypes(query: string, limit: int) =
        if String.IsNullOrWhiteSpace(query) then []
        else
            let results = Process.ExtractTop(query, typeEntries |> Seq.map (fun e -> e.FullName), limit = limit)
            results 
            |> Seq.map (fun r -> 
                let entry = typeEntries.[r.Index]
                { Type = entry.Type; FullName = entry.FullName; Score = r.Score }
            )
            |> Seq.toList

    member _.SearchMembers(query: string, limit: int) =
        if String.IsNullOrWhiteSpace(query) then []
        else
            let results = Process.ExtractTop(query, memberEntries |> Seq.map (fun e -> e.FullName), limit = limit)
            results 
            |> Seq.map (fun r -> 
                let entry = memberEntries.[r.Index]
                { Member = entry.Member; FullName = entry.FullName; ParentTypeName = entry.ParentTypeName; Score = r.Score }
            )
            |> Seq.toList
