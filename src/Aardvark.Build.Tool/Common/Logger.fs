namespace Aardvark.Build.Tool

open System
open System.IO
open System.IO.MemoryMappedFiles
open System.Text

type Verbosity =
    | Minimal  = 0
    | Normal   = 1
    | Detailed = 2
    | Debug    = 3

module Log =

    type Level =
        | Output  = -1
        | Error   = 0
        | Warning = 1
        | Info    = 2
        | Debug   = 3

    type private MemoryMappedWriter(pathOrName: string, sizeInBytes: int64, persisted: bool) =
        inherit TextWriter()

        let f =
            if persisted then
                let s = new FileStream(pathOrName, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
                MemoryMappedFile.CreateFromFile(s, null, sizeInBytes, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, false)
            else
                MemoryMappedFile.OpenExisting(pathOrName, MemoryMappedFileRights.Write)

        let s = f.CreateViewStream(0L, sizeInBytes, MemoryMappedFileAccess.Write)
        let mutable bytesWritten = 0L

        override x.Encoding = Encoding.Default

        override w.Write(value: char) : unit =
            raise <| NotImplementedException()

        override x.WriteLine(line: string) =
            let bytes = Encoding.Default.GetBytes (line + Environment.NewLine)
            let length = int64 bytes.Length
            if bytesWritten + length < sizeInBytes then
                s.Write(bytes)
                bytesWritten <- bytesWritten + length

        override x.Dispose(disposing: bool) =
            if disposing then
                s.Dispose()
                f.Dispose()

    let mutable private verbosity = Verbosity.Normal
    let mutable private writerOutput = System.Console.Out
    let mutable private writerInfo = System.Console.Out
    let mutable private writerWarn = System.Console.Error
    let mutable private writerError = System.Console.Error

    let private getWriter (level: Level) =
        match level with
        | Level.Output -> writerOutput
        | Level.Error -> writerError
        | Level.Warning -> writerWarn
        | _ -> writerInfo

    let private reset() =
        if writerOutput :? MemoryMappedWriter then writerOutput.Dispose()
        if writerInfo :? MemoryMappedWriter then writerInfo.Dispose()
        if writerWarn :? MemoryMappedWriter then writerWarn.Dispose()
        if writerError :? MemoryMappedWriter then writerError.Dispose()
        writerOutput <- System.Console.Out
        writerInfo <- System.Console.Out
        writerWarn <- System.Console.Error
        writerError <- System.Console.Error

    let init (args: Args) =
        try
            args |> Args.iter "verbosity" (fun v ->
                match v.ToLowerInvariant() with
                | "minimal"  -> verbosity <- Verbosity.Minimal
                | "normal"   -> verbosity <- Verbosity.Normal
                | "detailed" -> verbosity <- Verbosity.Detailed
                | "debug"    -> verbosity <- Verbosity.Debug
                | _ -> ()
            )

            let getLogSize =
                let mutable logSize = -1L

                fun () ->
                    if logSize < 0L then logSize <- Int64.Parse args.["log-size"]
                    logSize

            let persisted =
                args.["log-persisted"].ToLowerInvariant() = "true"

            args |> Args.iter "log-output" (fun name ->
                writerOutput <- new MemoryMappedWriter(name, getLogSize(), persisted)
            )

            args |> Args.iter "log-info" (fun name ->
                writerInfo <- new MemoryMappedWriter(name, getLogSize(), persisted)
            )

            args |> Args.iter "log-warn" (fun name ->
                writerWarn <- new MemoryMappedWriter(name, getLogSize(), persisted)
            )

            args |> Args.iter "log-error" (fun name ->
                writerError <- new MemoryMappedWriter(name, getLogSize(), persisted)
            )

        with _ ->
            reset()
            reraise()

        { new IDisposable with  member x.Dispose() = reset() }

    let private line (level: Level) fmt =
        Printf.kprintf (fun str ->
            if int verbosity >= int level then
                let w = getWriter level
                w.WriteLine(str)
        ) fmt

    let error fmt = line Level.Error fmt
    let warn fmt = line Level.Warning fmt
    let info fmt = line Level.Info fmt
    let debug fmt = line Level.Debug fmt
    let output fmt = line Level.Output fmt