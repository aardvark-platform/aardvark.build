namespace aardpack

open System

module Log =
    let mutable private indent = ""

    let private out = Console.Out

    let private consoleColorSupported =

        let o = Console.ForegroundColor
        try
            Console.ForegroundColor <- ConsoleColor.Yellow
            Console.ForegroundColor = ConsoleColor.Yellow
        finally
            Console.ForegroundColor <- o

    let start fmt =
        fmt |> Printf.kprintf (fun str ->
            out.WriteLine("> {0}{1}", indent, str)
            indent <- indent + "  "
        )

    let stop() =
        if indent.Length >= 2 then indent <- indent.Substring(0, indent.Length - 2)
        else indent <- ""

    let line fmt =
        fmt |> Printf.kprintf (fun str ->
            out.WriteLine("> {0}{1}", indent, str)
        )

    let warn fmt =
        fmt |> Printf.kprintf (fun str ->
            let c = Console.ForegroundColor
            try
                Console.ForegroundColor <- ConsoleColor.Yellow
                out.WriteLine("> {0}WRN {1}", indent, str)
                //Console.WriteLine("\u001b[1;33m{0}WRN {1}", indent, str)
            finally
                Console.ForegroundColor <- c
        )

    let error fmt =
        fmt |> Printf.kprintf (fun str ->
            let c = Console.ForegroundColor
            try
                Console.ForegroundColor <- ConsoleColor.Red
                out.WriteLine("> {0}ERR {1}", indent, str)
                //Console.WriteLine("\u001b[1;31m{0}ERR {1}", indent, str)
            finally
                Console.ForegroundColor <- c
        )