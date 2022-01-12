namespace Aardvark.Build

open System
open Microsoft.Build.Utilities
open Microsoft.Build.Framework
open System.IO
open System.Threading
open Aardvark.Build
open System.Xml
open System.Xml.Linq
open Paket
open Paket.Core
open Paket.Domain

type OverrideNuspecTask() as this =
    inherit Task()

    let mutable designTime = false
    let mutable repoRoot = ""
    let mutable assemblyPath = ""
    let mutable projectPath = ""
    let mutable config = ""
    let mutable assemblyName = ""
    let mutable outputPath = ""
    let mutable projectReferences = Array.empty<string>

    let mutable packageVersion = ""
    let mutable packageReleaseNotes = ""
    
    do Tools.boot this.Log

    member x.PackageReleaseNotes
        with get() = packageReleaseNotes
        and set d = packageReleaseNotes <- d

    [<Required>]
    member x.OutputPath
        with get() = outputPath
        and set p = outputPath <- p

    [<Required>]
    member x.PackageVersion
        with get() = packageVersion
        and set p = packageVersion <- p

    [<Required>]
    member x.AssemblyName
        with get() = assemblyName
        and set n = assemblyName <- n

    [<Required>]
    member x.Configuration
        with get() = config
        and set c = config <- c

    member x.RepositoryRoot
        with get() = repoRoot
        and set r = repoRoot <- r

    member x.DesignTime
        with get() = designTime
        and set d = designTime <- d

    [<Required>]        
    member x.Assembly
        with get() = assemblyPath
        and set d = assemblyPath <- d
         
    [<Required>]
    member x.ProjectPath
        with get() = projectPath
        and set d = projectPath <- d
         
    member x.ProjectReferences
        with get() = projectReferences
        and set d = projectReferences <- d

    override x.Execute() =
        if designTime then
            true
        else
            let projDir = Path.GetDirectoryName projectPath
            let root =
                if System.String.IsNullOrWhiteSpace repoRoot then Tools.findProjectRoot projDir
                else Some repoRoot

            match root with
            | Some root ->
                let depPath = Path.Combine(root, "paket.dependencies")
                let refPath = Path.Combine(projDir, "paket.references")

                if File.Exists depPath && File.Exists refPath then
                    let deps = DependenciesFile.ReadFromFile depPath
                    let findPackageConstraint (id : PackageName) =
                        deps.Groups |> Map.toSeq |> Seq.tryPick (fun (_,g) ->
                            g.Packages |> List.tryPick (fun p ->
                                if p.Name = id then Some p.VersionRequirement
                                else None
                            )
                        )
                    
                    let nugetRange (version : VersionRequirement) =
                        match version.Range with
                        | Minimum a -> string a
                        | GreaterThan a -> sprintf "(%A,)" a
                        | Maximum a -> sprintf "(,%A]" a
                        | LessThan a -> sprintf "(,%A)" a
                        | Specific a -> sprintf "[%A]" a
                        | OverrideAll a -> sprintf "[%A]" a
                        | Range(lb,l,h,hb) -> 
                            let prefix =
                                match lb with
                                | VersionRangeBound.Including -> "["
                                | VersionRangeBound.Excluding -> "("
                            let suffix =
                                match hb with
                                | VersionRangeBound.Including -> "]"
                                | VersionRangeBound.Excluding -> ")"
                            sprintf "%s%A,%A%s" prefix l h suffix

                    
                    let dependencies = 
                        let refs = ReferencesFile.FromFile refPath
                        refs.Groups |> Map.toList |> List.collect (fun (_g, ps) ->
                            ps.NugetPackages |> List.map (fun p ->
                                match findPackageConstraint p.Name with
                                | Some c -> 
                                    p.Name, nugetRange c
                                | None ->
                                    p.Name, "0.0.0"
                            )
                        )

                    let name = Path.GetFileNameWithoutExtension assemblyName
                    let nuspecPath =
                        Path.Combine(Path.GetDirectoryName(assemblyPath), "..", sprintf "%s.%s.nuspec" name packageVersion)
                        |> Path.GetFullPath


                    let nuspec = XDocument.Load nuspecPath

                    let deps = 
                        let inline name n =
                            XName.Get(n, "http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd")
                        nuspec
                            .Descendants(name "package")
                            .Descendants(name "metadata")
                            .Descendants(name "dependencies")
                            .Descendants(name "group")
                            .Descendants(name "dependency") |> Seq.toArray

                    let versions = 
                        let mutable res = Map.ofList dependencies
                        for p in projectReferences do
                            let name = Path.GetFileNameWithoutExtension p |> PackageName
                            res <- Map.add name $"[{packageVersion}]" res

                        res

                    for d in deps do
                        let p = d.Attribute(XName.Get "id").Value |> PackageName
                        let ex = d.Attribute(XName.Get "exclude")
                        if not (isNull ex) then ex.Remove()

                        match Map.tryFind p versions with
                        | Some range ->
                            d.SetAttributeValue(XName.Get "version", range)
                        | None ->
                            d.Remove()


                    nuspec.Save nuspecPath


                    // let dllName = Path.GetFileName assemblyName + ".dll"

                    // let dllPath =
                    //     Path.Combine(projDir, outputPath, Path.GetFileName assemblyName + ".dll")
                    //     |> Path.GetFullPath


                    // let framework =
                    //     Path.GetFileName(Path.GetDirectoryName dllPath)

                    // let packageId =
                    //     if String.IsNullOrWhiteSpace packageId then Path.GetFileNameWithoutExtension projectPath
                    //     else packageId

                    // let description =
                    //     if String.IsNullOrWhiteSpace packageDescription then packageId
                    //     else packageDescription

                    // let b = System.Text.StringBuilder()
                    // let inline line fmt = Printf.kprintf (fun str -> b.AppendLine str |> ignore) fmt
                    // line "<?xml version=\"1.0\" encoding=\"utf-8\"?>"
                    // line "<package xmlns=\"http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd\">"
                    // line "  <metadata>"
                    // line "    <id>%s</id>" packageId
                    // line "    <version>%s</version>" packageVersion
                    // line "    <authors>%s</authors>" packageAuthors
                    // line "    <owners>%s</owners>" packageAuthors
                    // line "    <releaseNotes>"
                    // line "%s" packageReleaseNotes
                    // line "    </releaseNotes>" 
                    // line "    <requireLicenseAcceptance>false</requireLicenseAcceptance>"
                    // line "    <description>%s</description>" description
                    // line "    <dependencies>"
                    // line "      <group targetFramework=\"%s\">" framework
                    // for (name, version) in dependencies do
                    //     line "        <dependency id=\"%s\" version=\"%s\" />" name.Name version 

                    // for proj in projectReferences do
                    //     let name = Path.GetFileNameWithoutExtension proj
                    //     line "        <dependency id=\"%s\" version=\"[%s]\" />" name packageVersion 


                    // line "      </group>"
                    // line "    </dependencies>"
                    // line "  </metadata>"
                    // line "  <files>"
                    // line "    <file src=\"%s\" target=\"lib/%s/%s\" />" dllPath framework dllName
                    // line "  </files>"
                    // line "</package>"

                    // File.WriteAllText(nuspecPath, b.ToString())


                    true
                else
                    false
            | None ->   
                x.Log.LogError("Could not find repository root")
                false
