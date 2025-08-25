using System.Runtime.InteropServices;

namespace dotnet;

public static class RustInteropTokio
{
    private const string LibraryName = "async_ffi";

    /// <summary>
    /// Delegate for the completion callback.
    /// </summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void CompletionCallBack(IntPtr result, IntPtr tcs);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern bool init_async_runtime();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void free_rust_string(IntPtr strPtr);

    // Test function exported from Rust
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern bool ffi_say_hello_async(IntPtr who, UIntPtr samples, CompletionCallBack callback, IntPtr tcs);

    static RustInteropTokio()
    {
        if (!init_async_runtime())
        {
            throw new InvalidOperationException("Failed to initialize Rust async runtime.");
        }
    }

    /// <summary>
    /// The callback to be called by Rust when the async operation completes.
    /// </summary>
    private static readonly CompletionCallBack Callback = (callbackResultPtr, callbackTcsPtr) =>
    {
        try
        {
            // Retrieve the `TaskCompletionSource` from the GC handle
            var callbackTcsHandle = GCHandle.FromIntPtr(callbackTcsPtr);
            var callbackTcs = (TaskCompletionSource<string>)callbackTcsHandle.Target!;

            if (callbackResultPtr == IntPtr.Zero)
            {
                callbackTcs.SetException(new InvalidOperationException("Rust function returned null."));
            }
            else
            {
                // Marshal the result back to a C# string
                var result = Marshal.PtrToStringAnsi(callbackResultPtr);
                callbackTcs.SetResult(result ?? string.Empty);
            }

            callbackTcsHandle.Free();
        }
        catch (Exception ex)
        {
            try
            {
                // Retrieve the `TaskCompletionSource` from the GC handle
                var callbackTcsHandle = GCHandle.FromIntPtr(callbackTcsPtr);
                var callbackTcs = (TaskCompletionSource<string>)callbackTcsHandle.Target!;

                callbackTcs.SetException(ex);
                callbackTcsHandle.Free();
            }
            catch
            {
                // ignored
            }
        }
        finally
        {
            // Free the unmanaged memory allocated for the result string
            if (callbackResultPtr != IntPtr.Zero)
            {
                free_rust_string(callbackResultPtr);
            }
        }
    };

    /// <summary>
    /// Test method that runs some computationally heavy task then returns a greeting message.
    /// </summary>
    /// <param name="who">Name of the person to greet.</param>
    /// <param name="samples">Number of samples to use in the Monte Carlo estimation of Pi.</param>
    /// <exception cref="InvalidOperationException"></exception>
    public static Task<string> SayHelloAsync(string who, uint samples)
    {
        var tcs = new TaskCompletionSource<string>();
        // Create a GC handle to prevent the TaskCompletionSource from being collected
        var tcsHandle = GCHandle.Alloc(tcs);
        var tcsPtr = GCHandle.ToIntPtr(tcsHandle);

        // Allocate unmanaged memory for the input string
        var whoPtr = Marshal.StringToHGlobalAnsi(who);
        var samplesPtr = (UIntPtr)samples;
        try
        {
            // Call the Rust FFI function
            if (!ffi_say_hello_async(whoPtr, samplesPtr, Callback, tcsPtr))
            {
                tcs.SetException(new InvalidOperationException("Failed to call Rust function."));
            }
        }
        finally
        {
            // Free allocated string memory
            Marshal.FreeHGlobal(whoPtr);
        }

        return tcs.Task;
    }
}