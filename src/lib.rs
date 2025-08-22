// https://mozilla.github.io/uniffi-rs/latest/tutorial/Rust_scaffolding.html#setup-for-crates-using-only-proc-macros
uniffi::setup_scaffolding!();

use thiserror::Error;
use std::ffi::{CStr, CString};
use std::os::raw::c_char;
use std::sync::mpsc;
use std::thread;

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
    use std::time::Duration;

    #[tokio::test]
    async fn it_works() {
        let result = say_hello_async("Stephen".to_string()).await.unwrap();
        assert!(result.ends_with("Hello, Stephen!"));
    }

    #[test]
    fn test_mpsc_hello() {
        use std::thread;
        use std::ffi::CString;
        
        // Test the mpsc function
        let who = CString::new("TestUser").unwrap();
        
        extern "C" fn test_callback(result: *const c_char, error: *const c_char) {
            // This is a simple test callback that would normally be more complex
            unsafe {
                if !result.is_null() {
                    let result_str = CStr::from_ptr(result).to_string_lossy();
                    assert!(result_str.contains("Hello, TestUser!"));
                    assert!(result_str.contains("thread="));
                } else if !error.is_null() {
                    let error_str = CStr::from_ptr(error).to_string_lossy();
                    panic!("Unexpected error: {}", error_str);
                } else {
                    panic!("Both result and error are null");
                }
            }
        }
        
        say_hello_async_mpsc(who.as_ptr(), test_callback);
        
        // Give the async operation time to complete
        thread::sleep(Duration::from_millis(100));
    }
}

// Manual FFI implementation using mpsc channels
// This demonstrates the approach recommended in The Rustonomicon for async callbacks

// Callback function pointer type for async completion
pub type AsyncCallback = extern "C" fn(*const c_char, *const c_char);

// Manual FFI function that uses mpsc channels for async communication
#[unsafe(no_mangle)]
pub extern "C" fn say_hello_async_mpsc(
    who: *const c_char,
    callback: AsyncCallback,
) {
    // Safety: We assume the caller provides a valid C string
    let who_str = unsafe {
        if who.is_null() {
            return;
        }
        match CStr::from_ptr(who).to_str() {
            Ok(s) => s.to_string(),
            Err(_) => return,
        }
    };

    // Create a channel for communication between threads
    let (tx, rx) = mpsc::channel::<Result<String, String>>();

    // Spawn a thread to do the async work
    thread::spawn(move || {
        // Simulate async work (like the original function)
        let result = format!(
            "[thread={:?}] Hello, {}!",
            std::thread::current().id(),
            who_str
        );
        
        // Send the result through the channel
        let _ = tx.send(Ok(result));
    });

    // Spawn another thread to handle the callback
    // This follows the Rustonomicon pattern of using channels to forward data
    thread::spawn(move || {
        match rx.recv() {
            Ok(result) => match result {
                Ok(message) => {
                    // Convert the result to a C string
                    if let Ok(c_message) = CString::new(message) {
                        callback(c_message.as_ptr(), std::ptr::null());
                    } else {
                        let error_msg = CString::new("Failed to convert message").unwrap();
                        callback(std::ptr::null(), error_msg.as_ptr());
                    }
                }
                Err(error) => {
                    if let Ok(c_error) = CString::new(error) {
                        callback(std::ptr::null(), c_error.as_ptr());
                    } else {
                        let fallback_error = CString::new("Unknown error").unwrap();
                        callback(std::ptr::null(), fallback_error.as_ptr());
                    }
                }
            },
            Err(_) => {
                let error_msg = CString::new("Channel receive error").unwrap();
                callback(std::ptr::null(), error_msg.as_ptr());
            }
        }
    });
}

// Helper function to free C strings allocated by Rust
#[unsafe(no_mangle)]
pub extern "C" fn free_rust_string(ptr: *mut c_char) {
    if !ptr.is_null() {
        unsafe {
            drop(CString::from_raw(ptr));
        }
    }
}
