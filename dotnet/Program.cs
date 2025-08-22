using uniffi.async_ffi;
using FfiException = uniffi.async_ffi.Exception;

try
{
    // UniFFI-generated async bindings
    Console.WriteLine("=== UniFFI Generated Bindings ===");
    Console.WriteLine($"##Run 1## {await AsyncFfiMethods.SayHelloAsync("Stephen")}");
    Console.WriteLine($"##Run 2## {await AsyncFfiMethods.SayHelloAsync("Ben")}");
    Console.WriteLine($"##Run 3## {await AsyncFfiMethods.SayHelloAsync("John")}");
    
    Console.WriteLine();
    
    // Manual mpsc-based async bindings
    Console.WriteLine("=== Manual MPSC Bindings ===");
    Console.WriteLine($"##Run 1## {await ManualAsyncFfiMethods.SayHelloMpscAsync("Stephen")}");
    Console.WriteLine($"##Run 2## {await ManualAsyncFfiMethods.SayHelloMpscAsync("Ben")}");
    Console.WriteLine($"##Run 3## {await ManualAsyncFfiMethods.SayHelloMpscAsync("John")}");
} 
catch (FfiException ex)
{
    Console.WriteLine($"UniFFI Exception: {ex.Message}");
}
catch (System.Exception ex)
{
    Console.WriteLine($"Manual FFI Exception: {ex.Message}");
}
