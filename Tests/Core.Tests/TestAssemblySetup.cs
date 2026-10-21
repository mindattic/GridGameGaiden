using GridGame.Core.Battle;
using NUnit.Framework;

// A fresh fixture instance per test and fixtures run side by side, as the suite ran under xUnit.
[assembly: FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[assembly: Parallelizable(ParallelScope.Fixtures)]

/// <summary>
/// Assembly-wide setup (no namespace, so NUnit runs it once before every fixture): the whole suite plays with
/// <see cref="BattleEngine.StrictTiles"/> on, so any tile two actors share throws at the boundary it happened
/// instead of being quietly self-healed by the release net (GG-T97). Never toggled per test — fixtures run in parallel.
/// </summary>
[SetUpFixture]
public sealed class GaidenTestAssemblySetup
{
    [OneTimeSetUp]
    public void StrictBoard() => BattleEngine.StrictTiles = true;
}
