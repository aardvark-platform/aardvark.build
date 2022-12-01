namespace Aardvark.Build

open Fake.Core

type ReadReleaseNotes() =
    // the goal of this seperate type and lib is to be robust against wrong fsharp.core versions being loaded in the context of 
    // visual studio (e.g. missing method exceptions, see https://github.com/aardvark-platform/aardvark.build/issues/3)
    static member ReadReleaseNotes(fileName : string) : string * string * string =
        let releaseNotes = ReleaseNotes.load fileName

        let nugetVersion = releaseNotes.NugetVersion
        let assemblyVersion = sprintf "%d.%d.0.0" releaseNotes.SemVer.Major releaseNotes.SemVer.Minor
        let notes = releaseNotes.Notes |> String.concat "\n" 
        (nugetVersion, assemblyVersion, notes) 
