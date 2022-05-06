namespace TestLib

type AnimalEnum = 
    | Cat = 0
    | Dog = 1
    | Fish = 2

type AnimalAttribute(value : AnimalEnum) =
    inherit System.Attribute()
    member x.Value = value
