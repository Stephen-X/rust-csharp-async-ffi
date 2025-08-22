using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace ManualAsyncFfi
{
    /// <summary>
    /// Manual P/Invoke interop for async Rust functions using mpsc channels.
    /// This demonstrates the Rustonomicon's recommendation for async callbacks.
    /// </summary>
    public static class RustInteropManual
    {
        private const string LibraryName = "async_ffi";

        // Delegate for the completion callback
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void CompletionCallback(IntPtr result, IntPtr userData);

        // P/Invoke declarations
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern bool init_async_runtime();

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void cleanup_async_runtime();

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern bool say_hello_async_manual(
            IntPtr who, 
            CompletionCallback callback, 
            IntPtr userData);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void free_rust_string(IntPtr ptr);

        // Static constructor to initialize the runtime
        static RustInteropManual()
        {
            if (!init_async_runtime())
            {
                throw new InvalidOperationException("Failed to initialize async runtime");
            }
        }

        /// <summary>
        /// Calls the Rust say_hello_async_manual function asynchronously using mpsc channels.
        /// </summary>
        /// <param name="who">The name to greet</param>
        /// <returns>A task that completes with the greeting message</returns>
        public static Task<string> SayHelloAsync(string who)
        {
            var tcs = new TaskCompletionSource<string>();
            
            // Create a GC handle to prevent the TaskCompletionSource from being collected
            var handle = GCHandle.Alloc(tcs);
            var userData = GCHandle.ToIntPtr(handle);

            // Define the callback that will be called by Rust
            CompletionCallback callback = (resultPtr, userDataPtr) =>
            {
                try
                {
                    // Convert the user data back to TaskCompletionSource
                    var gcHandle = GCHandle.FromIntPtr(userDataPtr);
                    var taskCompletionSource = (TaskCompletionSource<string>)gcHandle.Target!;
                    
                    // Convert the result from C string to managed string
                    var result = Marshal.PtrToStringUTF8(resultPtr);
                    
                    // Complete the task
                    taskCompletionSource.SetResult(result ?? string.Empty);
                    
                    // Free the GC handle
                    gcHandle.Free();
                }
                catch (Exception ex)
                {
                    // If anything goes wrong, try to get the TaskCompletionSource and set the exception
                    try
                    {
                        var gcHandle = GCHandle.FromIntPtr(userDataPtr);
                        var taskCompletionSource = (TaskCompletionSource<string>)gcHandle.Target!;
                        taskCompletionSource.SetException(ex);
                        gcHandle.Free();
                    }
                    catch
                    {
                        // If we can't even do that, just ignore the error
                        // This shouldn't happen in normal circumstances
                    }
                }
            };

            // Convert the string to UTF-8 bytes and get a pointer
            var whoBytes = System.Text.Encoding.UTF8.GetBytes(who + "\0"); // null-terminate
            var whoPtr = Marshal.AllocHGlobal(whoBytes.Length);
            try
            {
                Marshal.Copy(whoBytes, 0, whoPtr, whoBytes.Length);
                
                // Call the Rust function
                if (!say_hello_async_manual(whoPtr, callback, userData))
                {
                    // If the call failed, clean up and return a failed task
                    handle.Free();
                    tcs.SetException(new InvalidOperationException("Failed to call Rust function"));
                }
            }
            finally
            {
                // Free the allocated string memory
                Marshal.FreeHGlobal(whoPtr);
            }

            return tcs.Task;
        }

        /// <summary>
        /// Cleanup method to call when shutting down the application
        /// </summary>
        public static void Cleanup()
        {
            cleanup_async_runtime();
        }
    }
}