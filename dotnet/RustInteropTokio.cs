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
        // No TaskCompletionSource provided
        if (callbackTcsPtr == IntPtr.Zero) throw new InvalidOperationException("TaskCompletionSource pointer cannot be null.");

        // TODO: Upstream introduced GCHandle<T> as IDisposable: https://github.com/dotnet/runtime/pull/111307;
        //       integrate when available.
        GCHandle callbackTcsHandle = GCHandle.FromIntPtr(callbackTcsPtr);
        try
        {
            // Retrieve the `TaskCompletionSource` from the GC handle
            var callbackTcs = (TaskCompletionSource<string>)callbackTcsHandle.Target!;

            if (callbackResultPtr == IntPtr.Zero)
            {
                callbackTcs.SetException(new InvalidOperationException("Rust function returned null."));
            }
            else
            {
                // Marshal the result back to a C# string
                var result = Marshal.PtrToStringUTF8(callbackResultPtr);
                callbackTcs.SetResult(result ?? string.Empty);
            }
        }
        catch (Exception ex)
        {
            try
            {
                // Retrieve the `TaskCompletionSource` from the GC handle
                var callbackTcs = (TaskCompletionSource<string>)callbackTcsHandle.Target!;
                callbackTcs.SetException(ex);
            }
            catch
            {
                // ignored
            }
        }
        finally
        {
            // Free allocated unmanaged memory
            callbackTcsHandle.Free();
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
        nint whoPtr = 0;

        try
        {
            var tcsPtr = GCHandle.ToIntPtr(tcsHandle);

            // Allocate unmanaged memory for the input string
            whoPtr = Marshal.StringToCoTaskMemUTF8(who);
            var samplesPtr = (UIntPtr)samples;

            // Call the Rust FFI function
            if (!ffi_say_hello_async(whoPtr, samplesPtr, Callback, tcsPtr))
            {
                tcs.SetException(new InvalidOperationException("Failed to call Rust function."));
            }

            return tcs.Task;
        }
        finally
        {
            // Free allocated unmanaged memory
            // tcsHandle.Free();
            if (whoPtr != 0) Marshal.FreeCoTaskMem(whoPtr);
        }
    }
}