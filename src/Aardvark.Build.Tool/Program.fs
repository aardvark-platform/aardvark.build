namespace Aardvark.Build.Tool

open System.Diagnostics

module Program =

    [<EntryPoint>]
    let main argv =
        try
            if argv.Length > 0 then
                let args = argv |> Array.skip 1 |> Args
                use _ = Log.init args

                let sw = Stopwatch()
                sw.Start()

                try
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

                finally
                    sw.Stop()
                    let elapsedSeconds = (float sw.ElapsedMilliseconds) / 1000.0
                    Log.debug $"Command '{argv.[0]}' took %.3f{elapsedSeconds} seconds."

            else
                Log.error "No commmand specified."
                -1

        with e ->
            Log.error "%A" e
            -1