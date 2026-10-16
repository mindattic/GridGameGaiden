using NUnit.Framework;

// A fresh fixture instance per test and fixtures run side by side, as the suite ran under xUnit.
[assembly: FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[assembly: Parallelizable(ParallelScope.Fixtures)]
