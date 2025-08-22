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

TODO
