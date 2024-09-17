namespace TestApp

module Lib =
    let Value = $"{TestLibA.Lib.Value};{TestLibB.Lib.Value}"

module Program =

    [<EntryPoint>]
    let main argv =
        TestLibA.Lib.LocalOverride
        0