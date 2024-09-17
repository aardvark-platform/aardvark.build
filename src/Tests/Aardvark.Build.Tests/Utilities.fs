namespace Aardvark.Build.Tests

open System.IO
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