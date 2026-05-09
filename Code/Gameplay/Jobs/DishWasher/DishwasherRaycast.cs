using Sandbox;
using System;
using System.Linq;

namespace Sinvest;

public sealed class DishwasherRaycast : Component
{
	// In s&box, the component class is CameraComponent
	[Property] public CameraComponent ComponentCamera { get; set; } 
	[Property] public float ReachDistance { get; set; } = 500f;

	protected override void OnUpdate()
	{
		if ( Input.Down( "attack1" ) )
		{
			ExecuteTrace();
		}
	}

	private void ExecuteTrace()
	{
		// Fallback logic remains the same, but using CameraComponent type
		var activeCam = ComponentCamera.IsValid() ? ComponentCamera : Scene.Camera;

		if ( !activeCam.IsValid() )
		{
			Log.Warning( "No valid CameraComponent found for raycasting!" );
			return;
		}

		// ScreenNormalToRay( 0.5f ) is a method of CameraComponent
		var ray = activeCam.ScreenNormalToRay( 0.5f );
		
		Log.Info( $"Tracing from: {ray.Position}" );

		// Visualization for the Plate
		var target = Scene.GetAllObjects( true ).FirstOrDefault( x => x.Name == "DirtOverlay" );
		if ( target.IsValid() )
		{
			Vector3 mins = new Vector3( -4, -4, -4 );
			Vector3 maxs = new Vector3( 4, 4, 4 );
			DebugOverlay.Box( target.WorldPosition + mins, target.WorldPosition + maxs, Color.Yellow, 0.1f );
		}

		// Trace Execution
		var tr = Scene.Trace.Ray( ray, ReachDistance ).Run();

		// Debug Line (The Red Ray)
		DebugOverlay.Line( ray.Position, ray.Project( ReachDistance ), Color.Red, 0.1f );

		if ( tr.Hit )
		{
			// Draw the hit point in Green
			var hitSphere = new Sphere( tr.HitPosition, 4f );
			DebugOverlay.Sphere( hitSphere, Color.Green, 0.1f );
			
			Log.Info( $"HIT! -> {tr.GameObject.Name} (Tags: {tr.GameObject.Tags})" );
			
			if ( tr.GameObject.Tags.Has( "dirtydish" ) )
			{
				var mask = tr.GameObject.Components.Get<DishwasherDynamicMask>();
				mask?.CleanAtWorldLocation( tr );
			}
		}
		else
		{
			Log.Info( "Trace: Absolute Miss." );
		}
	}
}
