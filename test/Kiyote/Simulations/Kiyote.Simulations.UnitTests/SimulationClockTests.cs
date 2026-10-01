namespace Kiyote.Simulations.UnitTests;

[TestFixture]
public sealed class SimulationClockTests {

	[Test]
	public void FixedTimeStep_IsTenthOfASecond() {
		ISimulationClock clock = new SimulationClock();

		Assert.That( clock.FixedTimeStep, Is.EqualTo( 0.1f ) );
	}

}
