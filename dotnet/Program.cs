using System.Diagnostics;
using dotnet;
using Serilog;
using uniffi.async_ffi;
using Exception = System.Exception;
using FfiException = uniffi.async_ffi.Exception;

Helpers.InitLogger();

// TODO: Run microbenchmarks with BenchmarkDotNet

var rand = new Random();
var samples1 = (uint) rand.Next(1_000, 1_000_000);
var samples2 = (uint) rand.Next(1_000, 1_000_000);
var samples3 = (uint) rand.Next(1_000, 1_000_000);

try
{
    Log.Information("######## Test #1: Interop with UniFFI ########");
    Log.Information("#1.1. Call SayHelloAsync in parallel:");
    var stopwatch = Stopwatch.StartNew();
    await Task.WhenAll(
        Task.Run(async () => Log.Information("##Run 1## {SayHelloAsync}", await AsyncFfiMethods.SayHelloAsync("Stephen", samples1))),
        Task.Run(async () => Log.Information("##Run 2## {SayHelloAsync}", await AsyncFfiMethods.SayHelloAsync("Ben", samples2))),
        Task.Run(async () => Log.Information("##Run 3## {SayHelloAsync}", await AsyncFfiMethods.SayHelloAsync("John", samples3)))
    ).ContinueWith(_ => stopwatch.Stop());
    Log.Information("#Parallel calls completed in {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);

    Log.Information("#1.2. Call SayHelloAsync sequentially:");
    stopwatch = Stopwatch.StartNew();
    Log.Information("##Run 1## {SayHelloAsync}", await AsyncFfiMethods.SayHelloAsync("Stephen", samples1));
    Log.Information("##Run 2## {SayHelloAsync}", await AsyncFfiMethods.SayHelloAsync("Ben", samples2));
    Log.Information("##Run 3## {SayHelloAsync}", await AsyncFfiMethods.SayHelloAsync("John", samples3));
    stopwatch.Stop();
    Log.Information("#Sequential calls completed in {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);
}
catch (FfiException ex)
{
    Log.Error(ex, "Exception running UniFFI interop test");
}

try
{
    Log.Information("######## Test #2: Interop with async FFI ########");
    Log.Information("#2.1. Call SayHelloAsync in parallel:");
    var stopwatch = Stopwatch.StartNew();
    await Task.WhenAll(
        Task.Run(async () => Log.Information("##Run 1## {SayHelloAsync}", await RustInteropTokio.SayHelloAsync("Stephen", samples1))),
        Task.Run(async () => Log.Information("##Run 2## {SayHelloAsync}", await RustInteropTokio.SayHelloAsync("Ben", samples2))),
        Task.Run(async () => Log.Information("##Run 3## {SayHelloAsync}", await RustInteropTokio.SayHelloAsync("John", samples3)))
    ).ContinueWith(_ => stopwatch.Stop());
    Log.Information("#Parallel calls completed in {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);

    Log.Information("#2.2. Call SayHelloAsync sequentially:");
    stopwatch = Stopwatch.StartNew();
    Log.Information("##Run 1## {SayHelloAsync}", await RustInteropTokio.SayHelloAsync("Stephen", samples1));
    Log.Information("##Run 2## {SayHelloAsync}", await RustInteropTokio.SayHelloAsync("Ben", samples2));
    Log.Information("##Run 3## {SayHelloAsync}", await RustInteropTokio.SayHelloAsync("John", samples3));
    stopwatch.Stop();
    Log.Information("#Sequential calls completed in {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);
}
catch (Exception ex)
{
    Log.Error(ex, "Exception running async FFI interop test");
}