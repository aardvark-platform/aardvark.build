namespace Aardvark.Build.Tool

type Verbosity =
    | Minimal  = 0
    | Normal   = 1
    | Detailed = 2
    | Debug    = 3

module Log =

    type Level =
        | Error   = 0
        | Warning = 1
        | Info    = 2
        | Debug   = 3

    let mutable private command = ""
    let mutable private verbosity = Verbosity.Normal

    let private getWriter (level: Level) =
        match level with
        | Level.Error | Level.Warning -> System.Console.Error
        | _ -> System.Console.Out

    let init (cmd: string) (args: Args) =
        command <- cmd

        args |> Args.iter "verbosity" (fun v ->
            match v.ToLowerInvariant() with
            | "minimal"  -> verbosity <- Verbosity.Minimal
            | "normal"   -> verbosity <- Verbosity.Normal
            | "detailed" -> verbosity <- Verbosity.Detailed
            | "debug"    -> verbosity <- Verbosity.Debug
            | _ -> ()
        )

    let private line (level: Level) (str: string) =
        if int verbosity >= int level then
            let w = getWriter level
            w.WriteLine(str)

    // https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-diagnostic-format-for-tasks
    let private diag (isError: bool) fmt =
        fmt |> Printf.kprintf (fun str ->
            let level, category = if isError then Level.Error, "error" else Level.Warning, "warning"
            line level $"Aardvark.Build : {command} {category} : {str}"
        )

    let private msg (level: Level) fmt =
        fmt |> Printf.kprintf (fun str ->
            line level $"[Aardvark.Build] {str}"
        )

    let error fmt = diag true fmt
    let warn fmt = diag false fmt
    let info fmt = msg Level.Info fmt
    let debug fmt = msg Level.Debug fmt