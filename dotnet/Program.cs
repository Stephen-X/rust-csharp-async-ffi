using dotnet;
using Serilog;
using uniffi.async_ffi;
using Exception = System.Exception;
using FfiException = uniffi.async_ffi.Exception;

Helpers.InitLogger();

try
{
    Log.Information("######## Test #1: Interop with UniFFI ########");
    Log.Information("#1.1. Call SayHelloAsync in parallel:");
    await Task.WhenAll(
        Task.Run(async () => Log.Information("##Run 1## {SayHelloAsync}", await AsyncFfiMethods.SayHelloAsync("Stephen"))),
        Task.Run(async () => Log.Information("##Run 2## {SayHelloAsync}", await AsyncFfiMethods.SayHelloAsync("Ben"))),
        Task.Run(async () => Log.Information("##Run 3## {SayHelloAsync}", await AsyncFfiMethods.SayHelloAsync("John")))
    );

    Log.Information("#1.2. Call SayHelloAsync sequentially:");
    Log.Information("##Run 1## {SayHelloAsync}", await AsyncFfiMethods.SayHelloAsync("Stephen"));
    Log.Information("##Run 2## {SayHelloAsync}", await AsyncFfiMethods.SayHelloAsync("Ben"));
    Log.Information("##Run 3## {SayHelloAsync}", await AsyncFfiMethods.SayHelloAsync("John"));
}
catch (FfiException ex)
{
    Log.Error(ex, "Exception running UniFFI interop test");
}

try
{
    Log.Information("######## Test #2: Interop with async FFI ########");
    Log.Information("#2.1. Call SayHelloAsync in parallel:");
    await Task.WhenAll(
        Task.Run(async () => Log.Information("##Run 1## {SayHelloAsync}", await RustInteropTokio.SayHelloAsync("Stephen"))),
        Task.Run(async () => Log.Information("##Run 2## {SayHelloAsync}", await RustInteropTokio.SayHelloAsync("Ben"))),
        Task.Run(async () => Log.Information("##Run 3## {SayHelloAsync}", await RustInteropTokio.SayHelloAsync("John")))
    );

    Log.Information("#2.2. Call SayHelloAsync sequentially:");
    Log.Information("##Run 1## {SayHelloAsync}", await RustInteropTokio.SayHelloAsync("Stephen"));
    Log.Information("##Run 2## {SayHelloAsync}", await RustInteropTokio.SayHelloAsync("Ben"));
    Log.Information("##Run 3## {SayHelloAsync}", await RustInteropTokio.SayHelloAsync("John"));
}
catch (Exception ex)
{
    Log.Error(ex, "Exception running async FFI interop test");
}