# Async C# bindings for Rust

This project demos two approaches to create async FFI bindings for a Rust library in C# / .NET.

## Using UniFFI

1. Install [`uniffi-bindgen-cs`](https://github.com/NordSecurity/uniffi-bindgen-cs) with:

   ```bash
   cargo install uniffi-bindgen-cs --git https://github.com/NordSecurity/uniffi-bindgen-cs --tag v0.9.1+v0.28.3
   ```
   Note down the second version number (in the example above, `0.28.3`). This is the upstream [`uniffi`](https://github.com/mozilla/uniffi-rs)
   version in Rust binary that the generated bindings will be compatible with. Update `Cargo.toml` accordingly.

2. Build the Rust library with:

   ```bash
   cargo build --release
   ```

3. Generate the C# bindings with:

   ```bash
   uniffi-bindgen-cs.exe --library target\release\async_ffi.dll --out-dir="ffi\csharp"
   ```

   For Linux, change the path to the library to `target/release/libasync_ffi.so`.

   Alternatively, to generate bindings for Python, use the official `uniffi-bindgen` tool:

   ```bash
   cargo run --bin uniffi_bindgen generate --library target\release\async_ffi.dll --language python --out-dir ffi\python
   ```

4. Copy `async_ffi.cs` from `ffi\csharp` to the root directory of the C# project `dotnet`. Rename the file to `RustInterop.cs`.

5. Go to directory `dotnet` and run the C# project with `dotnet run`.

   The `async_ffi.dll` / `async_ffi.so` library file should be automatically copied to the output directory as part of the build process
   configured in `dotnet.csproj`.


## Writing bindings manually with mpsc

The Rustonomicon has the following recommendation about writing [async callbacks](https://doc.rust-lang.org/nomicon/ffi.html#asynchronous-callbacks):

> Things get more complicated when the external library spawns its own threads and invokes callbacks from there.
> In these cases access to Rust data structures inside the callbacks is especially unsafe and proper synchronization mechanisms
> must be used. Besides classical synchronization mechanisms like mutexes, one possibility in Rust is to **use channels
> (in `std::sync::mpsc`) to forward data from the C thread that invoked the callback into a Rust thread**.

This example demonstrates creating async FFI bindings manually using `std::sync::mpsc` channels, following the Rustonomicon's recommendation.

### Rust Implementation

The Rust side defines a manual FFI function that uses channels for thread-safe communication:

```rust
// Callback function pointer type for completion
pub type CompletionCallback = extern "C" fn(user_data: *mut c_void, result: *const c_char);

#[unsafe(no_mangle)]
pub extern "C" fn say_hello_mpsc(
    who: *const c_char,
    callback: CompletionCallback,
    user_data: *mut c_void,
) -> i32 {
    // ... parameter validation ...

    // Create a channel for communication between threads
    let (sender, receiver) = mpsc::channel::<String>();

    // Spawn a thread to simulate async work (this could be any external thread)
    thread::spawn(move || {
        // Simulate some work
        thread::sleep(std::time::Duration::from_millis(100));
        
        // Format the result message
        let result = format!("[thread={:?}][mpsc] Hello, {}!", thread::current().id(), name);
        
        // Send result back through the channel
        let _ = sender.send(result);
    });

    // Spawn another thread to handle the channel communication
    // This demonstrates forwarding data from C thread to Rust thread via mpsc
    thread::spawn(move || {
        match receiver.recv() {
            Ok(result) => {
                // Convert Rust string to C string and call the callback
                let c_result = CString::new(result).unwrap();
                callback(user_data_ptr, c_result.as_ptr());
            }
            Err(_) => {
                // Handle channel receive error
                let error_msg = CString::new("Error: failed to receive result").unwrap();
                callback(user_data_ptr, error_msg.as_ptr());
            }
        }
    });

    0 // Success
}
```

### C# Manual Bindings

The C# side creates manual P/Invoke declarations and async wrappers:

```csharp
internal static class ManualMpscFfi
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void CompletionCallback(IntPtr userData, IntPtr result);

    [DllImport("async_ffi", CallingConvention = CallingConvention.Cdecl)]
    public static extern int say_hello_mpsc(
        [MarshalAs(UnmanagedType.LPStr)] string who,
        CompletionCallback callback,
        IntPtr userData
    );
}

public static class ManualAsyncFfiMethods
{
    public static Task<string> SayHelloMpscAsync(string who)
    {
        var tcs = new TaskCompletionSource<string>();
        
        // Create a GCHandle to keep the TaskCompletionSource alive
        var handle = GCHandle.Alloc(tcs);
        var userData = GCHandle.ToIntPtr(handle);

        // Define the callback that will be called from Rust
        ManualMpscFfi.CompletionCallback callback = (IntPtr userDataPtr, IntPtr resultPtr) =>
        {
            try
            {
                var result = Marshal.PtrToStringUTF8(resultPtr);
                var handleFromCallback = GCHandle.FromIntPtr(userDataPtr);
                var tcsFromCallback = (TaskCompletionSource<string>)handleFromCallback.Target!;
                
                tcsFromCallback.SetResult(result ?? "Error: null result");
                handleFromCallback.Free();
            }
            catch (Exception ex)
            {
                // Handle errors and clean up
                // ... error handling code ...
            }
        };

        // Call the Rust function
        var result = ManualMpscFfi.say_hello_mpsc(who, callback, userData);
        
        return result == 0 ? tcs.Task : Task.FromException<string>(new Exception($"FFI call failed with code: {result}"));
    }
}
```

### Key Differences from UniFFI

1. **Thread Management**: The mpsc example explicitly creates threads and uses channels for communication, demonstrating the Rustonomicon pattern
2. **Manual Memory Management**: Requires careful handling of GCHandle, string marshalling, and cleanup
3. **Callback-Based**: Uses function pointers and callbacks instead of polling-based futures
4. **Lower-Level Control**: Provides direct control over the FFI boundary and async behavior

### Running the Example

```bash
# Build the Rust library
cargo build --release

# Run the C# project
cd dotnet
dotnet run
```

The output shows both approaches working side-by-side:

```
=== UniFFI Generated Bindings ===
##Run 1## [thread=ThreadId(1)][task=Id(1)] Hello, Stephen!
##Run 2## [thread=ThreadId(1)][task=Id(2)] Hello, Ben!
##Run 3## [thread=ThreadId(1)][task=Id(3)] Hello, John!

=== Manual MPSC Bindings ===
##Run 1## [thread=ThreadId(3)][mpsc] Hello, Stephen!
##Run 2## [thread=ThreadId(5)][mpsc] Hello, Ben!
##Run 3## [thread=ThreadId(7)][mpsc] Hello, John!
```

Notice how the UniFFI example uses async tasks on the main thread, while the mpsc example uses separate threads for each operation.
