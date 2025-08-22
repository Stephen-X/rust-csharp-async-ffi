// https://mozilla.github.io/uniffi-rs/latest/tutorial/Rust_scaffolding.html#setup-for-crates-using-only-proc-macros
uniffi::setup_scaffolding!();

use std::sync::mpsc;
use std::thread;
use std::ffi::{CStr, CString};
use std::os::raw::{c_char, c_void};
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

// Manual FFI function using mpsc channels
// This demonstrates the Rustonomicon recommendation for async callbacks
// Callback function pointer type for completion
pub type CompletionCallback = extern "C" fn(user_data: *mut c_void, result: *const c_char);

#[unsafe(no_mangle)]
pub extern "C" fn say_hello_mpsc(
    who: *const c_char,
    callback: CompletionCallback,
    user_data: *mut c_void,
) -> i32 {
    if who.is_null() {
        return -1; // Error: null pointer
    }

    let name = match unsafe { CStr::from_ptr(who) }.to_str() {
        Ok(s) => s.to_string(),
        Err(_) => return -2, // Error: invalid UTF-8
    };

    // Create a channel for communication between threads
    let (sender, receiver) = mpsc::channel::<String>();

    // Spawn a thread to simulate async work (this could be any external thread)
    thread::spawn(move || {
        // Simulate some work
        thread::sleep(std::time::Duration::from_millis(100));
        
        // Format the result message
        let result = format!(
            "[thread={:?}][mpsc] Hello, {}!",
            thread::current().id(),
            name
        );
        
        // Send result back through the channel
        let _ = sender.send(result);
    });

    // Convert raw pointer to usize for thread safety
    let user_data_raw = user_data as usize;
    
    // Spawn another thread to handle the channel communication
    // This demonstrates forwarding data from C thread to Rust thread via mpsc
    thread::spawn(move || {
        match receiver.recv() {
            Ok(result) => {
                // Convert Rust string to C string
                match CString::new(result) {
                    Ok(c_result) => {
                        // Convert back to pointer and call the callback
                        let user_data_ptr = user_data_raw as *mut c_void;
                        callback(user_data_ptr, c_result.as_ptr());
                    }
                    Err(_) => {
                        // Handle string conversion error
                        let error_msg = CString::new("Error: failed to convert result").unwrap();
                        let user_data_ptr = user_data_raw as *mut c_void;
                        callback(user_data_ptr, error_msg.as_ptr());
                    }
                }
            }
            Err(_) => {
                // Handle channel receive error
                let error_msg = CString::new("Error: failed to receive result").unwrap();
                let user_data_ptr = user_data_raw as *mut c_void;
                callback(user_data_ptr, error_msg.as_ptr());
            }
        }
    });

    0 // Success
}

// Helper function to free C strings allocated by Rust
#[unsafe(no_mangle)]
pub extern "C" fn free_rust_string(ptr: *mut c_char) {
    if !ptr.is_null() {
        unsafe {
            let _ = CString::from_raw(ptr);
        }
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
    fn test_mpsc_function() {
        let result = Arc::new(Mutex::new(None));
        let result_clone = Arc::clone(&result);

        extern "C" fn test_callback(user_data: *mut c_void, result_ptr: *const c_char) {
            let result_arc = unsafe { &*(user_data as *const Arc<Mutex<Option<String>>>) };
            let c_str = unsafe { CStr::from_ptr(result_ptr) };
            if let Ok(result_str) = c_str.to_str() {
                *result_arc.lock().unwrap() = Some(result_str.to_string());
            }
        }

        let name = CString::new("TestUser").unwrap();
        let return_code = say_hello_mpsc(
            name.as_ptr(),
            test_callback,
            &result_clone as *const _ as *mut c_void,
        );

        assert_eq!(return_code, 0); // Should return success

        // Wait for callback to be called
        thread::sleep(Duration::from_millis(200));

        let result_guard = result.lock().unwrap();
        assert!(result_guard.is_some());
        let result_str: &String = result_guard.as_ref().unwrap();
        assert!(result_str.contains("Hello, TestUser!"));
        assert!(result_str.contains("[mpsc]"));
    }
}
