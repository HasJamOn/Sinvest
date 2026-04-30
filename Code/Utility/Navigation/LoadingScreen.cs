using Sandbox; 
using Sinvest;
using System.Threading.Tasks;

public sealed class LoadingScreen : Component
{
	[Property] public float MinimumTime { get; set; } = 1.5f; 

	private float _timer = 0;
	private bool _loadingStarted = false;

	protected override void OnStart()
	{
		_timer = 0;
		_loadingStarted = false;

		// Ensure any pending cloud stats from the menu are pushed before we load the world
		_ = Sandbox.Services.Stats.FlushAsync();
	}

	protected override void OnUpdate()
	{
		if ( _loadingStarted ) return;

		_timer += RealTime.Delta;

		// --- DEPENDENCY CHECK ---
		// We do not proceed if the SaveSystem isn't ready. 
		// This prevents StartupManager exceptions in the next scene.
		if ( GameSaveSystem.Instance == null || GameSaveSystem.Instance.CurrentCharacter == null )
		{
			return;
		}

		// --- TIMING CHECK ---
		if ( _timer >= MinimumTime )
		{
			if ( !string.IsNullOrEmpty( SceneNavigator.TargetScene ) )
			{
				_loadingStarted = true;
				Log.Info( $"[Loading] Dependencies ready. Transitioning to: {SceneNavigator.TargetScene}" );
                
				Scene.LoadFromFile( SceneNavigator.TargetScene );
			}
		}
	}
}
