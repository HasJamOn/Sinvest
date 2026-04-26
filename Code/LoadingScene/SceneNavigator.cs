using Sandbox;
using System.Linq;

public static class SceneNavigator
{
	/// <summary>
	/// The path the Loading Screen should jump to once ready.
	/// </summary>
	public static string TargetScene { get; private set; }

	/// <summary>
	/// Safely transitions the player to the loading screen.
	/// </summary>
	public static void TransitionTo( SceneFile worldScene )
	{
		if ( worldScene == null )
		{
			Log.Error( "[SceneNavigator] Transition failed: The provided worldScene was null!" );
			return;
		}

		TargetScene = worldScene.ResourcePath;

		// Find the loading scene by name. This is faster than raw paths and 
		// works even if you move the file to a different folder.
		var loadingScene = ResourceLibrary.GetAll<SceneFile>()
			.FirstOrDefault( x => x.ResourceName.Equals( "loading", System.StringComparison.OrdinalIgnoreCase ) );

		if ( loadingScene != null )
		{
			// Useful for production logs to track player flow
			Log.Info( $"[SceneNavigator] Navigating to: {loadingScene.ResourceName} -> {TargetScene}" );
			Game.ActiveScene.Load( loadingScene );
		}
		else
		{
			// This remains critical even in shipping; if the loading screen is missing, 
			// the game would just "soft lock" on the menu without this error.
			Log.Error( "[SceneNavigator] CRITICAL: 'loading.scene' not found in ResourceLibrary!" );
		}
	}
}
