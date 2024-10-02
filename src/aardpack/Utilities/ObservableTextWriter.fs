namespace aardpack

open System
open System.IO
open System.Threading.Tasks

type ObservableTextWriter() =
    inherit TextWriter()

    let mutable newline = Environment.NewLine
    let mutable currentLine = new StringWriter()

    let event = Event<string>()

    interface IObservable<string> with
        member x.Subscribe obs = event.Publish.Subscribe obs

    override this.Close() = ()
    override this.Dispose _ = ()
    override this.DisposeAsync() = ValueTask.CompletedTask
    override this.Encoding = System.Text.Encoding.UTF8
    override this.Flush() = ()
    override this.FlushAsync() = Task.FromResult () :> Task
    override this.FormatProvider = System.Globalization.CultureInfo.InvariantCulture
    override this.NewLine
        with get () = newline
        and set v = newline <- v
    override this.Write(value: bool) = currentLine.Write value
    override this.Write(value: char) = currentLine.Write value
    override this.Write(buffer: char[]) = currentLine.Write buffer
    override this.Write(buffer: char[], index: int, count: int) = currentLine.Write(buffer, index, count)
    override this.Write(value: decimal) = currentLine.Write value
    override this.Write(value: float) = currentLine.Write value
    override this.Write(value: int) = currentLine.Write value
    override this.Write(value: int64) = currentLine.Write value
    override this.Write(value: obj) = currentLine.Write value
    override this.Write(buffer: ReadOnlySpan<char>) = currentLine.Write buffer
    override this.Write(value: float32) = currentLine.Write value
    override this.Write(value: string) = currentLine.Write value
    override this.Write(format: string, arg0: obj) = currentLine.Write(format, arg0)
    override this.Write(format: string, arg0: obj, arg1: obj) = currentLine.Write(format, arg0, arg1)
    override this.Write(format: string, arg0: obj, arg1: obj, arg2: obj) = currentLine.Write(format, arg0, arg1, arg2)
    override this.Write(format: string, arg: obj[]) = currentLine.Write(format, arg)
    override this.Write(value: Text.StringBuilder) = currentLine.Write(value)
    override this.Write(value: uint32) = currentLine.Write(value)
    override this.Write(value: uint64) = currentLine.Write(value)
    override this.WriteAsync(value: char) = currentLine.WriteAsync(value)
    override this.WriteAsync(buffer: char[], index: int, count: int) =
        let str = System.String(buffer, index, count)
        currentLine.WriteAsync(buffer, index, count)
    override this.WriteAsync(buffer: ReadOnlyMemory<char>, cancellationToken: Threading.CancellationToken) = currentLine.WriteAsync(buffer, cancellationToken)
    override this.WriteAsync(value: string) = currentLine.WriteAsync(value)
    override this.WriteAsync(value: Text.StringBuilder, cancellationToken: Threading.CancellationToken) = currentLine.WriteAsync(value, cancellationToken)
    override this.WriteLine() =
        currentLine.ToString() |> event.Trigger
        currentLine.Dispose()
        currentLine <- new StringWriter()

    override this.WriteLine(value: bool) = this.Write value; this.WriteLine()
    override this.WriteLine(value: char) = this.Write value; this.WriteLine()
    override this.WriteLine(buffer: char[]) = this.Write buffer; this.WriteLine()
    override this.WriteLine(buffer: char[], index: int, count: int) = this.Write(buffer, index, count); this.WriteLine()
    override this.WriteLine(value: decimal) = this.Write value; this.WriteLine()
    override this.WriteLine(value: float) = this.Write value; this.WriteLine()
    override this.WriteLine(value: int) = this.Write value; this.WriteLine()
    override this.WriteLine(value: int64) = this.Write value; this.WriteLine()
    override this.WriteLine(value: obj) = this.Write value; this.WriteLine()
    override this.WriteLine(buffer: ReadOnlySpan<char>) = this.Write buffer; this.WriteLine()
    override this.WriteLine(value: float32) = this.Write value; this.WriteLine()
    override this.WriteLine(value: string) = this.Write value; this.WriteLine()
    override this.WriteLine(format: string, arg0: obj) = this.Write(format, arg0); this.WriteLine()
    override this.WriteLine(format: string, arg0: obj, arg1: obj) = this.Write(format, arg0, arg1); this.WriteLine()
    override this.WriteLine(format: string, arg0: obj, arg1: obj, arg2: obj) = this.Write(format, arg0, arg1, arg2); this.WriteLine()
    override this.WriteLine(format: string, arg: obj[]) = this.Write(format, arg); this.WriteLine()
    override this.WriteLine(value: Text.StringBuilder) = this.Write(value); this.WriteLine()
    override this.WriteLine(value: uint32) = this.Write(value); this.WriteLine()
    override this.WriteLine(value: uint64) = this.Write(value); this.WriteLine()
    override this.WriteLineAsync() = this.WriteLine(); Task.FromResult () :> Task
    override this.WriteLineAsync(value: char) = this.WriteLine(value); Task.FromResult () :> Task
    override this.WriteLineAsync(buffer: char[], index: int, count: int) = this.WriteLine(buffer, index, count); Task.FromResult () :> Task
    override this.WriteLineAsync(buffer: ReadOnlyMemory<char>, cancellationToken: Threading.CancellationToken) = this.WriteAsync(buffer, cancellationToken) |> ignore; Task.FromResult () :> Task
    override this.WriteLineAsync(value: string) = this.WriteLine(value); Task.FromResult () :> Task
    override this.WriteLineAsync(value: Text.StringBuilder, cancellationToken: Threading.CancellationToken) = this.WriteLineAsync(value, cancellationToken) |> ignore; Task.FromResult () :> Task