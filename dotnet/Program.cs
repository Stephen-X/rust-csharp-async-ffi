using uniffi.async_ffi;
using FfiException = uniffi.async_ffi.Exception;
using ManualAsyncFfi;

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

try
{
    Console.WriteLine("\n######## Test #2: Manual Interop with mpsc channels ########\n");
    Console.WriteLine($"##Run 1## {await RustInteropManual.SayHelloAsync("Stephen")}");
    Console.WriteLine($"##Run 2## {await RustInteropManual.SayHelloAsync("Ben")}");
    Console.WriteLine($"##Run 3## {await RustInteropManual.SayHelloAsync("John")}");
    
    // Test concurrent calls to demonstrate channel usage
    Console.WriteLine("\n## Concurrent calls ##");
    var tasks = new[]
    {
        RustInteropManual.SayHelloAsync("Alice"),
        RustInteropManual.SayHelloAsync("Bob"),
        RustInteropManual.SayHelloAsync("Charlie"),
        RustInteropManual.SayHelloAsync("Diana")
    };
    
    var results = await Task.WhenAll(tasks);
    for (int i = 0; i < results.Length; i++)
    {
        Console.WriteLine($"##Concurrent {i + 1}## {results[i]}");
    }
} 
catch (System.Exception ex)
{
    Console.WriteLine($"Exception in manual interop: {ex.Message}");
}
finally
{
    // Cleanup
    RustInteropManual.Cleanup();
}
