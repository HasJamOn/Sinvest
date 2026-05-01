using Sandbox;
using System;
using System.Linq;

public sealed class StampLandDebugManager : Component
{
	[Property] public bool RunIdentityDiagnostics { get; set; } = false;

	protected override void OnUpdate()
	{
		if ( RunIdentityDiagnostics )
		{
			RunIdentityDiagnostics = false;
			PerformIdentityCheck();
		}
	}

	/// <summary>
	/// This adds a button to the Inspector in the s&box editor.
	/// </summary>
	[Button( "Run Player Model Check" )]
	public void CheckForPlayers()
	{
		var renderers = Scene.GetAllComponents<SkinnedModelRenderer>();
    
		Log.Info( "--- PLAYER MODEL DIAGNOSTICS ---" );

		foreach ( var renderer in renderers )
		{
			var go = renderer.GameObject;
        
			// Correctly join the tags into a readable string
			string activeTags = string.Join( ", ", go.Tags );
        
			Log.Info( $"[Object: {go.Name}] " +
			          $"Enabled: {renderer.Enabled} | " +
			          $"IsProxy: {go.IsProxy} | " +
			          $"Tags: {(string.IsNullOrWhiteSpace(activeTags) ? "None" : activeTags)}" );

			// Visual aid
			DebugOverlay.Box( renderer.Bounds, Color.Yellow, 5f );
		}

		Log.Info( "--- DIAGNOSTICS COMPLETE ---" );
	}

	public void PerformIdentityCheck()
	{
		var cubes = Scene.GetAllComponents<StampLandCube>();
		var localId = Connection.Local.Id;

		Log.Info( "--- STAMPLAND IDENTITY DIAGNOSTICS ---" );
		foreach ( var cube in cubes )
		{
			if ( cube.Value == 0 ) continue;
			bool matches = cube.OwnerId == localId;
			Log.Info( $"[Cube {cube.GameObject.Name}] Value: {cube.Value} | Match: {matches}" );
		}
	}
}
