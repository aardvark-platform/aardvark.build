namespace Tests

//open System.IO
//open NUnit.Framework
//open FsUnit
//open Aardvark.Build.ReleaseNotes

//module FakeImpl =
//    open Fake.Core

//    let parseReleaseNotes (fileName : string) =
//        let releaseNotes = ReleaseNotes.load fileName

//        let nugetVersion = releaseNotes.NugetVersion
//        let assemblyVersion = sprintf "%d.%d.0.0" releaseNotes.SemVer.Major releaseNotes.SemVer.Minor
//        let notes = releaseNotes.Notes |> String.concat "\n" 
//        { assemblyVersion = assemblyVersion; nugetVersion = nugetVersion; releaseNotes = notes } |> Some



//[<TestFixture>]
//type TestClass () =

//    [<Test>]
//    member this.TestReadMyOWnReleaseNotes() =
//        let path = Path.Combine(__SOURCE_DIRECTORY__, "examples", "current.md")
//        let result = StandaloneImpl.parseReleaseNotes path
//        result.Value.nugetVersion |> should equal "1.0.17"
//        result.Value.assemblyVersion |> should equal "1.0.0.0"
//        result.Value.releaseNotes |> should equal "version bump"


//    [<Test>]
//    member this.CompareToFullBlownPaketImpl() =
//        let path = Path.Combine(__SOURCE_DIRECTORY__, "examples")
//        Directory.EnumerateFiles(path, "*.md") |> Seq.iter (fun path -> 
//            let result = StandaloneImpl.parseReleaseNotes path
//            let baseline = FakeImpl.parseReleaseNotes path
//            baseline.Value |> should equal result.Value
//        )
