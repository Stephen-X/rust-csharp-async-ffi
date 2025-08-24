use rand::Rng;
use thiserror::Error;

#[derive(Debug, Error, uniffi::Error)]
pub enum Error {
    #[error("Unknown error occurred: {0}")]
    UnknownError(String),
}

#[uniffi::export(async_runtime = "tokio")]
pub async fn say_hello_async(who: String) -> Result<String, Error> {
    let result = tokio::spawn(async move {
        // Perform some random computationally heavy task (Monte Carlo estimation of Pi)
        let mut rng = rand::rng();
        let mut count = 0;
        let samples = rng.random_range(1_000..1_000_000);
        for _ in 0..samples {
            let x: f64 = rng.random();
            let y: f64 = rng.random();
            if x * x + y * y <= 1.0 {
                count += 1;
            }
        }
        let pi_estimate = 4.0 * (count as f64) / (samples as f64);

        format!(
            "[thread={:?}][task={:?}][sample={:?}][pi={:?}] Hello, {}!",
            std::thread::current().id(),
            tokio::task::id(),
            samples,
            pi_estimate,
            who
        )
    }).await;

    match result {
        Ok(message) => Ok(message),
        Err(e) => Err(Error::UnknownError(e.to_string())),
    }
}

#[cfg(test)]
mod tests {
    use assertor::*;
    use tokio::join;
    use super::*;

    #[tokio::test]
    async fn it_works() {
        // `join!` macro doesn't run tasks in parallel (i.e. in multiple threads),
        // need to spawn tasks explicitly
        let results = join!(
            tokio::spawn(say_hello_async("Stephen".to_string())),
            tokio::spawn(say_hello_async("Ben".to_string())),
            tokio::spawn(say_hello_async("John".to_string()))
        );

        let results0 = results.0.unwrap();
        println!("Result #1 = {:?}", results0);
        assert_that!(results0).is_ok();
        assert_that!(results0).ok().ends_with("Hello, Stephen!");

        let results1 = results.1.unwrap();
        println!("Result #2 = {:?}", results1);
        assert_that!(results1).is_ok();
        assert_that!(results1).ok().ends_with("Hello, Ben!");

        let results2 = results.2.unwrap();
        println!("Result #3 = {:?}", results2);
        assert_that!(results2).is_ok();
        assert_that!(results2).ok().ends_with("Hello, John!");
    }
}
