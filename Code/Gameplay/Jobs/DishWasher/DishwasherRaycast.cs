using Sandbox;
using System;

namespace Sinvest;

public sealed class DishwasherRaycast : Component
{
	[Property] public float ReachDistance { get; set; } = 500f;
	[Property] public bool ShowDebug { get; set; } = true;

	private SceneTraceResult _lastTrace;
	private Ray _lastRay;

	protected override void OnUpdate()
	{
		// Safety check for Scene Camera
		if ( Scene.Camera is null ) return;

		// Construct the ray from the center of your screen
		_lastRay = new Ray( Scene.Camera.WorldPosition, Scene.Camera.WorldRotation.Forward );

		// Execute the trace
		_lastTrace = Scene.Trace.Ray( _lastRay, ReachDistance )
			.UsePhysicsWorld()
			// .WithTag( "dirtydish" ) // Uncomment this later when your dishes have tags!
			.Run();

		// Immediate feedback in the Scene View
		if ( ShowDebug )
		{
			DrawDebugLine();
		}
	}

	private void DrawDebugLine()
	{
		using ( Gizmo.Scope() )
		{
			// PRO TIP: Offset the visual line slightly down/right so it's not 
			// perfectly hidden behind the camera's center point.
			Vector3 visualStart = _lastRay.Position + (Scene.Camera.WorldRotation.Down * 5f) + (Scene.Camera.WorldRotation.Right * 5f);
			Vector3 endPos = _lastTrace.Hit ? _lastTrace.HitPosition : _lastRay.Position + (_lastRay.Forward * ReachDistance);

			Gizmo.Draw.Color = _lastTrace.Hit ? Color.Green : Color.Red;
			Gizmo.Draw.LineThickness = 3f;
            
			// Draw the line
			Gizmo.Draw.Line( visualStart, endPos );

			if ( _lastTrace.Hit )
			{
				// Draw a small box where it hits
				Gizmo.Draw.SolidBox( new BBox( endPos - 2f, endPos + 2f ) );
				Gizmo.Draw.Text( $"Hit: {_lastTrace.GameObject.Name}", new Transform( endPos + Vector3.Up * 10f ) );
			}
		}
	}
}
