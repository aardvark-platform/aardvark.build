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


type ReleaseNotesTask() =
    inherit Task()
    let mutable designTime = false
    let mutable projectPath = ""
    let mutable nugetVersion = ""
    let mutable assemblyVersion = ""
    let mutable repoRoot = ""
    let mutable notes = ""

    member x.RepositoryRoot
        with get() = repoRoot
        and set r = repoRoot <- r

    member x.DesignTime
        with get() = designTime
        and set d = designTime <- d
         
    [<Required>]
    member x.ProjectPath
        with get() = projectPath
        and set p = projectPath <- p

    [<Output>]
    member x.NugetVersion
        with get() = nugetVersion
        and set d = nugetVersion <- d

    [<Output>]
    member x.AssemblyVersion
        with get() = assemblyVersion
        and set d = assemblyVersion <- d

    [<Output>]
    member x.ReleaseNotes
        with get() = notes
        and set d = notes <- d

    override x.Execute() =  
        if designTime then
            true
        else
            let projDir = Path.GetDirectoryName projectPath
            let root =
                if System.String.IsNullOrWhiteSpace repoRoot then Tools.findProjectRoot projDir
                elif Directory.Exists repoRoot then Some repoRoot
                else None

            match root with
            | Some rootDir ->
                let releaseNotes =
                    let path = 
                        Directory.GetFiles(rootDir, "*") |> Array.tryFind Tools.isReleaseNotesFile

                    match path with
                    | Some path ->  
                        try Some (Fake.Core.ReleaseNotes.load path)
                        with _ -> None
                    | None ->
                        None

                match releaseNotes with
                | Some n -> 
                    nugetVersion <- n.NugetVersion
                    assemblyVersion <- sprintf "%d.%d.0.0" n.SemVer.Major n.SemVer.Minor
                    notes <- n.Notes |> String.concat "\n" 
                    true
                | None ->
                    x.Log.LogWarning "No release notes found: version will be 1.0.0.0. consider adding a RELEASE_NOTES.md to your repository root."
                    nugetVersion <- "1.0.0.0"
                    assemblyVersion <- "1.0.0.0"
                    notes <- ""
                    true
            | None ->
                x.Log.LogWarning "Could not find repository root (please specify RepositoryRoot Property)"   
                true
