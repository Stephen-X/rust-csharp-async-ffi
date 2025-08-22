// https://mozilla.github.io/uniffi-rs/latest/tutorial/Rust_scaffolding.html#setup-for-crates-using-only-proc-macros
uniffi::setup_scaffolding!();

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

    #[tokio::test]
    async fn it_works() {
        let result = say_hello_async("Stephen".to_string()).await.unwrap();
        assert!(result.ends_with("Hello, Stephen!"));
    }
}
