namespace NuGex

open System
open System.Collections.Generic
open Microsoft.CodeAnalysis

/// Shared Roslyn symbol-walking logic used by both SolutionProcessor and PackageProcessor
/// to build an ApiModel's Types dictionary from a namespace's public surface.
module RoslynApiVisitor =

    let rec visitType (typeSymbol: INamedTypeSymbol) (types: Dictionary<string, ApiType>) =
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

    let rec visitNamespace (ns: INamespaceSymbol) (types: Dictionary<string, ApiType>) =
        for typeSymbol in ns.GetTypeMembers() do
            visitType typeSymbol types

        for childNs in ns.GetNamespaceMembers() do
            visitNamespace childNs types
