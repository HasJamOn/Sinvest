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
		var activeCam = ComponentCamera.IsValid() ? ComponentCamera : Scene.Camera;

		if ( !activeCam.IsValid() )
		{
			Log.Warning( "No valid CameraComponent found for raycasting!" );
			return;
		}

		// 1. Generate ray from the current mouse position
		// activeCam.ScreenNormalToRay converts 0.0-1.0 screen coordinates to a Ray
		var mousePos = Mouse.Position / Screen.Size;
		var ray = activeCam.ScreenNormalToRay( mousePos );
    
		// 2. Perform the Trace
		var tr = Scene.Trace.Ray( ray, ReachDistance )
			.WithTag( "dirtydish" )
			.Run();

		// --- VISUAL FEEDBACK ---

		// Draw the "Aim" line in Cyan
		DebugOverlay.Line( ray.Position, ray.Project( ReachDistance ), Color.Cyan, 2.0f );

		if ( tr.Hit )
		{
			// Draw Green line to the hit point
			DebugOverlay.Line( ray.Position, tr.HitPosition, Color.Green, 2.0f );
			DebugOverlay.Sphere( new Sphere( tr.HitPosition, 2f ), Color.Green, 2.0f );
        
			Log.Info( $"HIT! -> {tr.GameObject.Name}" );
        
			var mask = tr.GameObject.Components.Get<DishwasherDynamicMask>();
			mask?.CleanAtWorldLocation( tr );
		}
	}
}
