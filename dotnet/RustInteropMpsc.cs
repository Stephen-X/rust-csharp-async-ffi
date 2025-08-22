using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace RustInteropMpsc
{
    /// <summary>
    /// Manual C# interop for Rust async FFI using mpsc channels.
    /// This demonstrates the approach recommended in The Rustonomicon for async callbacks.
    /// </summary>
    public static class AsyncFfiMethods
    {
        private const string LibraryName = "async_ffi";

        // Delegate for the async callback
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void AsyncCallback(IntPtr result, IntPtr error);

        // Import the Rust FFI function
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void say_hello_async_mpsc(
            [MarshalAs(UnmanagedType.LPStr)] string who,
            AsyncCallback callback
        );

        // Import the helper function to free Rust strings
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void free_rust_string(IntPtr ptr);

        /// <summary>
        /// Async wrapper for the Rust say_hello_async_mpsc function.
        /// This uses TaskCompletionSource to convert the callback-based API to async/await.
        /// </summary>
        /// <param name="who">Name to greet</param>
        /// <returns>Greeting message</returns>
        /// <exception cref="Exception">If the Rust function returns an error</exception>
        public static Task<string> SayHelloAsync(string who)
        {
            var tcs = new TaskCompletionSource<string>();

            // Create a callback that will be called from Rust
            AsyncCallback callback = (result, error) =>
            {
                try
                {
                    if (error != IntPtr.Zero)
                    {
                        // Handle error case
                        string errorMessage = Marshal.PtrToStringAnsi(error) ?? "Unknown error";
                        tcs.SetException(new Exception($"Rust error: {errorMessage}"));
                    }
                    else if (result != IntPtr.Zero)
                    {
                        // Handle success case
                        string message = Marshal.PtrToStringAnsi(result) ?? "";
                        tcs.SetResult(message);
                    }
                    else
                    {
                        tcs.SetException(new Exception("Unexpected null result from Rust"));
                    }
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            };

            // Call the Rust function with our callback
            try
            {
                say_hello_async_mpsc(who, callback);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }

            return tcs.Task;
        }
    }
}