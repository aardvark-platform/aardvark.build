namespace Aardvark.Build.Tool

module Program =

    [<EntryPoint>]
    let main argv =
        try
            if argv.Length > 0 then
                let args = argv |> Array.skip 1 |> Args
                use _ = Log.init args

                match argv.[0] with
                | "notes" ->
                    ReleaseNotesCommand.run args

                | "native-deps" ->
                    NativeDependenciesCommand.run args

                | "local-sources" ->
                    LocalSourcesCommand.run args

                | cmd ->
                    Log.error $"Unknown command: {cmd}"
                    -1
            else
                Log.error "No commmand specified."
                -1

        with e ->
            Log.error "%A" e
            -1