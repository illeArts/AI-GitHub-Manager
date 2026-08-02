using Xunit;

// Several integration tests run real git processes and exercise the shared
// repository-lock/process-detection infrastructure. Running them alongside
// unrelated tests makes the clean-operation assertion nondeterministic on
// macOS. The suite is short, so deterministic serial execution is preferable
// to a release-blocking flaky test.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
