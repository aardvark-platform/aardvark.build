namespace Aardvark.Build.ReleaseNotes

type Info = { assemblyVersion : string; nugetVersion : string; releaseNotes : string }

// the goal of this seperate type and lib is to be robust against wrong fsharp.core versions being loaded in the context of 
// visual studio (e.g. missing method exceptions, see https://github.com/aardvark-platform/aardvark.build/issues/3)
// it should be standalone to be as robust as possible
module StandaloneImpl =
    
    open System
    open System.Linq
    open System.IO
    open System.Text.RegularExpressions

    // https://semver.org/#is-there-a-suggested-regular-expression-regex-to-check-a-semver-string
    let semVer = 
        "^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-((?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\+([0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?$"
        |> Regex

    let releaseLine = "\* (.*)$" |> Regex // "* example release note line" etc..

    // unsophisticated way to parse release notes. Clearly we should use a full-blown implementation but due to https://github.com/aardvark-platform/aardvark.build/issues/3 this is currently not possible.
    let parseReleaseNotes (fileName : string) : Option<Info> = 
        let lines = 
            File.ReadAllLines fileName
        let versionAndLine =
            lines
            |> Seq.zip (Seq.initInfinite id)
            |> Seq.tryPick (fun (lineIndex, line) -> 
                let m = semVer.Match(line.Trim().Replace("#","").Replace(" ","")) 
                if m.Success then
                    let nugetVersion = m.Value
                    let assemblyVersion = sprintf "%s.%s.0.0" m.Groups[1].Value m.Groups[2].Value
                    Some (nugetVersion, assemblyVersion, lineIndex)
                else 
                    None
            )
        match versionAndLine with
        | Some (nugetVersion, assemblyVersion, lineContainingVersion) -> 
            let releaseNotes = 
                lines 
                |> Seq.skip (lineContainingVersion + 1) 
                |> Seq.takeWhile (fun line -> line.Trim() <> "")
                |> Seq.choose (fun line ->
                    let m = releaseLine.Match(line)
                    if m.Success then Some m.Groups[1].Value else None
                )
            { assemblyVersion = assemblyVersion; nugetVersion = nugetVersion; releaseNotes = releaseNotes |> String.concat Environment.NewLine } |> Some
        | None -> 
            None