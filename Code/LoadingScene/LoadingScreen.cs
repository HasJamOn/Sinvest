using Sandbox; 
using Sinvest;
using System.Threading.Tasks;

public sealed class LoadingScreen : Component
{
	[Property] public float MinimumTime { get; set; } = 1.5f; // Prevents "flickering" loading screens

	protected override async void OnStart()
	{
		float startTime = RealTime.Now;

		// 1. Ensure all Cloud Stats are synced before proceeding
		// This is our safety check!
		await Sandbox.Services.Stats.FlushAsync();

		// 2. Add a slight artificial delay if the loading was too fast
		// (Better for player psychology than a 0.1s flash)
		var elapsed = RealTime.Now - startTime;
		if ( elapsed < MinimumTime )
		{
			await Task.DelayRealtimeSeconds( MinimumTime - elapsed );
		}

		// 3. Load the actual game
		if ( !string.IsNullOrEmpty( SceneNavigator.TargetScene ) )
		{
			Scene.LoadFromFile( SceneNavigator.TargetScene );
		}
	}
}
