namespace Aardvark.Build.Tests

open System
open System.IO
open System.Diagnostics
open System.Text
open Fake.DotNet
open ICSharpCode.SharpZipLib.Zip

[<Struct; RequireQualifiedAccess>]
type Framework =
    | Net8
    | Net48
    | NetStandard20

    override x.ToString() =
        match x with
        | Framework.Net8 -> "net8.0"
        | Framework.Net48 -> "net48"
        | Framework.NetStandard20 -> "netstandard2.0"

[<AutoOpen>]
module internal ZipFileExtensions =

    type ZipFile with
        member x.ReadEntry(entryName: string) =
            let e = x.GetEntry(entryName)
            use s = x.GetInputStream e
            use r = new StreamReader(s)
            r.ReadToEnd()

module internal DotNet =

    #if DEBUG
    let configuration = DotNet.BuildConfiguration.Debug
    #else
    let configuration = DotNet.BuildConfiguration.Release
    #endif

    let build (framework: Framework) (path: string) =
        path |> DotNet.build (fun o ->
            { o with
                MSBuildParams = { o.MSBuildParams with DisableInternalBinLog = true }
                Common = { o.Common with Verbosity = Some DotNet.Verbosity.Minimal; RedirectOutput = true }
                Framework = Some <| string framework
                Configuration = configuration
            }
        )

    let pack (path: string) =
        path |> DotNet.pack (fun o ->
            { o with
                MSBuildParams =
                    { o.MSBuildParams with
                        DisableInternalBinLog = true
                        Properties = ["TargetsForTfmSpecificContentInPackage", ""] // https://github.com/dotnet/fsharp/issues/12320
                    }
                Common = { o.Common with Verbosity = Some DotNet.Verbosity.Minimal; RedirectOutput = true }
                NoBuild = true
                NoRestore = true
                OutputPath = Some "pack"
                Configuration = configuration
            }
        )

module internal Process =

    let run (writeOutputToConsole: bool) (directory: string option) (cmd: string) (args: string) =
        use p = new Process()
        p.StartInfo.FileName <- cmd
        p.StartInfo.Arguments <- args
        p.StartInfo.RedirectStandardOutput <- true
        p.StartInfo.RedirectStandardError <- true
        p.StartInfo.UseShellExecute <- false
        p.StartInfo.CreateNoWindow <- true
        p.StartInfo.WorkingDirectory <- directory |> Option.defaultValue null

        let output = ResizeArray<string>()
        p.OutputDataReceived.Add (fun args ->
            if writeOutputToConsole then
                Console.WriteLine args.Data

            if not <| String.IsNullOrWhiteSpace args.Data then
                lock output (fun _ -> output.Add args.Data)
        )

        let errors = ResizeArray<string>()
        p.ErrorDataReceived.Add (fun args ->
            if not <| String.IsNullOrWhiteSpace args.Data then
                lock errors (fun _ -> errors.Add args.Data)
        )

        p.Start() |> ignore
        p.BeginOutputReadLine()
        p.BeginErrorReadLine()
        p.WaitForExit()

        if p.ExitCode <> 0 || errors.Count > 0 then
            let sb = StringBuilder()
            sb.Append $"Command '{cmd} {args}' failed (status code = {p.ExitCode})" |> ignore
            if errors.Count > 0 then
                sb.Append $":" |> ignore

                for e in errors do
                    sb.Append $"{Environment.NewLine}    {e}" |> ignore
            else
                sb.Append "." |> ignore

            failwith <| sb.ToString()

        output