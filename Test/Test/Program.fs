
open FSys

module Bla =
    let test a b = 
        TestThingHans.test()
        a + b

[<EntryPoint>]
let main args =
    Bla.test 1 2 |> printfn "%A"
    0