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
	    if ( Scene.Camera is null ) return;

	    _lastRay = new Ray( Scene.Camera.WorldPosition, Scene.Camera.WorldRotation.Forward );

	    _lastTrace = Scene.Trace.Ray( _lastRay, ReachDistance )
		    .UsePhysicsWorld()
		    .Run();

	    // Check for Attack1 (Left Click)
	    if ( Input.Down( "attack1" ) && _lastTrace.Hit )
	    {
		    // In s&box, we check for existence by simply attempting to Get the component
		    var mask = _lastTrace.GameObject.Components.Get<DishwasherDynamicMask>();
            
		    if ( mask.IsValid() ) // Use .IsValid() for managed s&box objects
		    {
			    mask.CleanAtWorldLocation( _lastTrace );
		    }
	    }

	    if ( ShowDebug ) DrawDebugLine();
    }

    private void DrawDebugLine()
    {
	    using ( Gizmo.Scope() )
	    {
		    Vector3 visualStart = _lastRay.Position + (Scene.Camera.WorldRotation.Down * 5f);
		    Vector3 endPos = _lastTrace.Hit ? _lastTrace.HitPosition : _lastRay.Position + (_lastRay.Forward * ReachDistance);

		    Gizmo.Draw.Color = _lastTrace.Hit ? Color.Green : Color.Red;
		    Gizmo.Draw.LineThickness = 2f;
		    Gizmo.Draw.Line( visualStart, endPos );

		    // Fix: Check for the component's existence using Get() != null
		    bool isDish = _lastTrace.Hit && _lastTrace.GameObject.Components.Get<DishwasherDynamicMask>() is not null;

		    if ( Input.Down( "attack1" ) && isDish )
		    {
			    Gizmo.Draw.Color = Color.Yellow;
			    Gizmo.Draw.SolidSphere( _lastTrace.HitPosition, 1f );
		    }
	    }
    }
}
