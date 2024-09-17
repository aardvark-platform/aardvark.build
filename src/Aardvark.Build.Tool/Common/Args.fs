namespace Aardvark.Build.Tool

open System

type Args (args: string[]) =
    let map =
        args |> Array.choose (fun s ->
            let t = s.Split('=', StringSplitOptions.TrimEntries ||| StringSplitOptions.RemoveEmptyEntries)
            if t.Length = 2 && t.[0].StartsWith("--") && t.[0].Length > 2 then
                Some (t.[0].Substring(2), t.[1])
            else
                None
        )
        |> Map.ofArray

    member x.Get (name: string) =
        match map |> Map.tryFind name with
        | Some value -> value
        | _ -> failwith $"Missing argument --{name}."

    member x.TryGet (name: string) =
        map |> Map.tryFind name

    member x.Item
        with get name = x.Get name

    override x.ToString() =
        sprintf "%A" args

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Args =

    let inline tryGet (name: string) (args: Args) =
        args.TryGet name

    let inline get (name: string) (args: Args) =
        args.Get name

    let inline iter (name: string) (action: string -> unit) (args: Args) =
        match args |> tryGet name with
        | Some value -> action value
        | _ -> ()