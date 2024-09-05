
open FSys
open TestLib

[<Animal(AnimalEnum.Dog)>]
type Thing([<Animal(AnimalEnum.Dog)>] value : int) =
    member x.Value = value

[<EntryPoint>]
let main args =
    let t = Thing(100)
    printfn "%A" t.Value
    0