using uniffi.async_ffi;
using FfiException = uniffi.async_ffi.Exception;

try
{
    Console.WriteLine("######## Test #1: Interop with UniFFI ########\n");
    Console.WriteLine($"##Run 1## {await AsyncFfiMethods.SayHelloAsync("Stephen")}");
    Console.WriteLine($"##Run 2## {await AsyncFfiMethods.SayHelloAsync("Ben")}");
    Console.WriteLine($"##Run 3## {await AsyncFfiMethods.SayHelloAsync("John")}");
} catch (FfiException ex)
{
    Console.WriteLine($"Exception: {ex.Message}");
}

Console.WriteLine();

try
{
    Console.WriteLine("######## Test #2: Manual Interop with mpsc ########\n");
    Console.WriteLine($"##Run 1## {await RustInteropMpsc.AsyncFfiMethods.SayHelloAsync("Stephen")}");
    Console.WriteLine($"##Run 2## {await RustInteropMpsc.AsyncFfiMethods.SayHelloAsync("Ben")}");
    Console.WriteLine($"##Run 3## {await RustInteropMpsc.AsyncFfiMethods.SayHelloAsync("John")}");
} catch (System.Exception ex)
{
    Console.WriteLine($"Exception: {ex.Message}");
}
