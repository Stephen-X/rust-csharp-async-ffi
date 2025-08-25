//! This is an implementation of async-supporting Rust FFI
//! with a provided Tokio runtime.
//! See also: https://doc.rust-lang.org/nomicon/ffi.html

use std::ffi::{c_char, c_uint, c_void, CStr, CString};
use std::sync::OnceLock;
use tokio::runtime::Runtime;
use crate::function::say_hello_async;

/// Async callback method that C# will provide.
type CompletionCallback = extern "C" fn(*const c_char, *mut c_void);

// Single global Tokio runtime instance.
static RUNTIME: OnceLock<Runtime> = OnceLock::new();

/// Used for initializing the Tokio async runtime.
#[unsafe(no_mangle)]
pub extern "C" fn init_async_runtime() -> bool {
    RUNTIME.get_or_init(|| {
        // TODO: Expose options to configure Tokio worker thread pool (for CPU vs I/O bound tasks)
        tokio::runtime::Builder::new_multi_thread()
            .enable_all()
            .build()
            .expect("Failed to create Tokio runtime")
    });
    true
}

/// Used for freeing a C string allocated by Rust.
#[unsafe(no_mangle)]
pub extern "C" fn free_rust_string(str_ptr: *mut c_char) {
    if !str_ptr.is_null() {
        unsafe {
            // Retake ownership of a CString to drop it and free memory.
            let _ = CString::from_raw(str_ptr);
        }
    }
}

/// FFI function for the `say_hello_async` method.
///
/// # Arguments
/// * `who_ptr` - Pointer to a C string containing the name.
/// * `samples` - Number of samples to use in the Monte Carlo estimation of Pi.
/// * `callback` - Callback function to be called with the result.
/// * `tcs` - C# [`TaskCompletionSource`](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.taskcompletionsource-1?view=net-9.0).
///
/// # Return
/// Returns `false` if the task failed to be scheduled because invalid arguments were provided.
#[unsafe(no_mangle)]
pub extern "C" fn ffi_say_hello_async(
    who_ptr: *const c_char,
    samples: c_uint,
    callback: CompletionCallback,
    tcs: *mut c_void,
) -> bool {
    if who_ptr.is_null() || callback as usize == 0 {
        return false;
    }

    let who_str = unsafe { CStr::from_ptr(who_ptr) }
        .to_string_lossy() // Replace invalid UTF-8 sequences in string, does not panic
        .to_string();
    let runtime = RUNTIME.get().unwrap();
    let tcs_addr = tcs as usize; // Convert to usize for Send

    // Spawn a background task to execute the target library function
    runtime.spawn(async move {
        match say_hello_async(who_str, samples as u32).await {
            // On success, invoke the C# callback with the result
            Ok(message) => {
                match CString::new(message) {
                    Ok(message_cstr) => {
                        let tcs_ptr = tcs_addr as *mut c_void; // Convert back to pointer
                        callback(message_cstr.as_ptr(), tcs_ptr)
                    },
                    Err(e) => {
                        eprintln!("Failed to create CString: {:?}", e);
                    }
                }
            }
            Err(e) => {
                eprintln!("Error executing `say_hello_async`: {:?}", e);
            }
        }
    });

    true
}


#[cfg(test)]
mod tests {
    use std::sync::{Arc, Mutex};
    use std::time::Duration;
    use rand::Rng;
    use super::*;

    #[test]
    fn test_ffi_say_hello_async_callback() {
        // Initialize runtime
        assert!(init_async_runtime());

        // Define callback method
        // Here C#'s `TaskCompletionSource` is simulated with an `Arc<Mutex<Option<String>>>`
        extern "C" fn test_callback(message_ptr: *const c_char, tcs: *mut c_void) {
            unsafe {
                let result_ptr = tcs as *mut Arc<Mutex<Option<String>>>;
                let result = &*result_ptr;

                let message = CStr::from_ptr(message_ptr).to_string_lossy().to_string();
                *result.lock().unwrap() = Some(message);
            }
        }

        // Prepare test data
        let who_cstr = CString::new("Stephen").unwrap();
        let tcs = Arc::new(Mutex::new(None::<String>));

        // Call the FFI function
        let mut rng = rand::rng();
        assert!(ffi_say_hello_async(
            who_cstr.as_ptr(),
            rng.random_range(1_000..1_000_000) as u32,
            test_callback,
            Arc::into_raw(tcs.clone()) as *mut c_void)
        );

        // Wait for async task to complete
        std::thread::sleep(Duration::from_millis(300));

        // Check result
        let message = tcs.lock().unwrap();
        assert!(message.is_some());
        assert!(message.as_ref().unwrap().ends_with("Hello, Stephen!"));
    }
}
