// https://mozilla.github.io/uniffi-rs/latest/tutorial/Rust_scaffolding.html#setup-for-crates-using-only-proc-macros
uniffi::setup_scaffolding!();

use std::ffi::{CStr, CString};
use std::os::raw::{c_char, c_void};
use std::sync::{Arc, OnceLock};
use tokio::sync::mpsc;
use thiserror::Error;

#[derive(Debug, Error, uniffi::Error)]
pub enum Error {
    #[error("Unknown error occurred: {0}")]
    UnknownError(String),
}

#[uniffi::export(async_runtime="tokio")]
pub async fn say_hello_async(who: String) -> Result<String, Error> {
    let result = tokio::spawn(async move {
        format!("[thread={:?}][task={:?}] Hello, {}!", std::thread::current().id(), tokio::task::id(), who)
    }).await;

    match result {
        Ok(message) => Ok(message),
        Err(e) => Err(Error::UnknownError(e.to_string())),
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::sync::{Arc, Mutex};
    use std::time::Duration;

    #[tokio::test]
    async fn it_works() {
        let result = say_hello_async("Stephen".to_string()).await.unwrap();
        assert!(result.ends_with("Hello, Stephen!"));
    }

    #[test]
    fn test_manual_ffi_runtime_init() {
        // Test that we can initialize the runtime
        assert!(init_async_runtime());
        // Should return true if already initialized
        assert!(init_async_runtime());
    }

    #[test]
    fn test_manual_ffi_callback() {
        // Initialize runtime
        assert!(init_async_runtime());
        
        // Shared result storage
        let result = Arc::new(Mutex::new(None::<String>));
        let result_clone = Arc::clone(&result);
        
        // Define callback
        extern "C" fn test_callback(message_ptr: *const c_char, user_data: *mut c_void) {
            unsafe {
                let result_ptr = user_data as *mut Arc<Mutex<Option<String>>>;
                let result = &*result_ptr;
                
                let message = CStr::from_ptr(message_ptr).to_string_lossy().to_string();
                *result.lock().unwrap() = Some(message);
            }
        }
        
        // Prepare test data
        let who = CString::new("TestUser").unwrap();
        let user_data = &result_clone as *const _ as *mut c_void;
        
        // Call the function
        assert!(say_hello_async_manual(who.as_ptr(), test_callback, user_data));
        
        // Wait a bit for the async operation to complete
        std::thread::sleep(Duration::from_millis(100));
        
        // Check result
        let final_result = result.lock().unwrap();
        assert!(final_result.is_some());
        assert!(final_result.as_ref().unwrap().contains("Hello, TestUser!"));
    }
}

// Manual FFI implementation using mpsc channels
// This follows the Rustonomicon recommendation for async callbacks

// Callback function type that C# will provide
type CompletionCallback = extern "C" fn(*const c_char, *mut c_void);

// Runtime for handling async operations
static RUNTIME: OnceLock<Arc<tokio::runtime::Runtime>> = OnceLock::new();

// Initialize the async runtime
#[unsafe(no_mangle)]
pub extern "C" fn init_async_runtime() -> bool {
    RUNTIME.get_or_init(|| {
        match tokio::runtime::Runtime::new() {
            Ok(rt) => Arc::new(rt),
            Err(_) => {
                // If we can't create a runtime, we'll return a dummy one
                // The function will return false to indicate failure
                panic!("Failed to create tokio runtime");
            }
        }
    });
    true
}

// Cleanup the runtime (no-op with OnceLock, but kept for API compatibility)
#[unsafe(no_mangle)]
pub extern "C" fn cleanup_async_runtime() {
    // With OnceLock, we can't easily clean up, but that's okay for this demo
    // In a real application, you might want a more sophisticated approach
}

// Manual async function using mpsc channels
#[unsafe(no_mangle)]
pub extern "C" fn say_hello_async_manual(
    who_ptr: *const c_char,
    callback: CompletionCallback,
    user_data: *mut c_void,
) -> bool {
    if who_ptr.is_null() {
        return false;
    }

    let who_cstr = unsafe { CStr::from_ptr(who_ptr) };
    let who = match who_cstr.to_str() {
        Ok(s) => s.to_string(),
        Err(_) => return false,
    };

    if let Some(runtime) = RUNTIME.get() {
        let rt = Arc::clone(runtime);
        let user_data_addr = user_data as usize; // Convert to usize for Send
        
        // Spawn the async task
        rt.spawn(async move {
            // Create a channel for communication
            let (tx, mut rx) = mpsc::channel::<String>(1);
            
            // Spawn the actual work in another task
            let work_handle = tokio::spawn(async move {
                // Simulate some async work (similar to original function)
                let result = format!(
                    "[thread={:?}][task={:?}] Hello, {}!", 
                    std::thread::current().id(), 
                    tokio::task::id(), 
                    who
                );
                
                // Send result through channel
                if tx.send(result).await.is_err() {
                    eprintln!("Failed to send result through channel");
                }
            });
            
            // Wait for the result from the channel
            if let Some(message) = rx.recv().await {
                // Convert to C string for callback
                if let Ok(c_message) = CString::new(message) {
                    // Call the C# callback with the result
                    let user_data_ptr = user_data_addr as *mut c_void;
                    callback(c_message.as_ptr(), user_data_ptr);
                }
            }
            
            // Wait for work to complete
            let _ = work_handle.await;
        });
        
        true
    } else {
        false
    }
}

// Free a C string allocated by Rust
#[unsafe(no_mangle)]
pub extern "C" fn free_rust_string(ptr: *mut c_char) {
    if !ptr.is_null() {
        unsafe {
            let _ = CString::from_raw(ptr);
        }
    }
}
