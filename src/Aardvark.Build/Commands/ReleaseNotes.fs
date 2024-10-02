namespace Aardvark.Build.Tool

open System.IO
open System.Text.RegularExpressions
open Fake.Core

module ReleaseNotesCommand =

    let private findNotesFile =
        let rx = Regex(@"^release(_|-)?notes(\.md)?$", RegexOptions.IgnoreCase ||| RegexOptions.Compiled)

        Utilities.locate "release notes file" (fun directory ->
            let files = Directory.GetFiles directory
            files |> Array.tryFind (Path.GetFileName >> rx.IsMatch)
        )

    let run (args: Args) =
        let file =
            match args |> Args.tryGet "release-notes-path" with
            | Some f ->
                if File.Exists f then Some f
                else
                    Log.warn $"Release notes file does not exist: {f}"
                    None

            | _ ->
                let path = Path.GetDirectoryName(args.["project-path"])
                Log.debug "Locating release notes for path: %s" path
                findNotesFile path

        let printNotes (nugetVersion: string) (assemblyVersion: string) (notes: string list) =
            File.WriteAllText(args.["output-nuget-version"], nugetVersion)
            File.WriteAllText(args.["output-assembly-version"], assemblyVersion)
            File.WriteAllLines(args.["output-release-notes"], notes)

        match file with
        | Some file ->
            try
                Log.debug "Found release notes: %s" file

                let data =
                    // Skip all invalid lines for preliminary release notes
                    // https://github.com/fsprojects/FAKE/blob/04c2b476becaea55b2caa54420c2bbf64c901460/src/app/Fake.Core.ReleaseNotes/ReleaseNotes.fs#L232
                    File.ReadAllLines(file)
                    |> Array.skipWhile (fun line ->
                        let line = line.Trim('-', ' ')
                        if line.Length > 0 then
                            line.[0] <> '*' && line.[0] <> '#'
                        else
                            true
                    )

                let releaseNotes = ReleaseNotes.parse data
                let nugetVersion = releaseNotes.NugetVersion
                let assemblyVersion = sprintf "%d.%d.0.0" releaseNotes.SemVer.Major releaseNotes.SemVer.Minor
                Log.info "Version: %s" nugetVersion
                printNotes nugetVersion assemblyVersion releaseNotes.Notes

            with e ->
                Log.warn $"Failed to parse release notes '{file}': {e.Message}"
                printNotes "1.0.0.0" "1.0.0.0" []

        | None ->
            Log.warn "No release notes found, version will be 1.0.0.0. Consider adding a RELEASE_NOTES.md to your repository root."
            printNotes "1.0.0.0" "1.0.0.0" []

        0