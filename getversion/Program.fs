open Fake.Core
open System.IO

[<EntryPoint>]
let main args =
    let inPath = Path.Combine(__SOURCE_DIRECTORY__, "..", "RELEASE_NOTES.md")
    let outPath = Path.Combine(__SOURCE_DIRECTORY__, "..", ".version")
    let notes = ReleaseNotes.load inPath

    File.WriteAllText(outPath, notes.NugetVersion)

 

    0
