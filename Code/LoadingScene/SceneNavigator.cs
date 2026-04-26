using Sandbox; 
using Sinvest;
using System.Linq;

public static class SceneNavigator
{
	public static string TargetScene { get; private set; }

	public static void TransitionTo( SceneFile worldScene )
	{
		if ( worldScene == null )
		{
			Log.Error( "[SceneNavigator] Transition failed: The provided worldScene was null!" );
			return;
		}

		TargetScene = worldScene.ResourcePath;

		// Find the loading scene. We search by name OR path for maximum reliability.
		var loadingScene = ResourceLibrary.GetAll<SceneFile>()
			.FirstOrDefault( x => x.ResourceName.Equals( "loading", System.StringComparison.OrdinalIgnoreCase ) 
			                      || x.ResourcePath.Contains( "loading.scene" ) );

		if ( loadingScene != null )
		{
			Log.Info( $"[SceneNavigator] Navigating to: {loadingScene.ResourceName} -> {TargetScene}" );
          
			// Use Game.ActiveScene to ensure we are calling the load from a static context correctly
			Game.ActiveScene.Load( loadingScene );
		}
		else
		{
			Log.Error( "[SceneNavigator] CRITICAL: 'loading.scene' not found! Check your file names." );
		}
	}
}
