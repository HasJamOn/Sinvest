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

        // 1. Generate a ray from the mouse cursor position
        // This handles the conversion from 2D screen space to 3D world space
        _lastRay = Scene.Camera.ScreenPixelToRay( Mouse.Position );

        // 2. Run the trace
        _lastTrace = Scene.Trace.Ray( _lastRay, ReachDistance )
           .UsePhysicsWorld()
           .Run();

        // 3. Check for Attack1 (Left Click)
        if ( Input.Down( "attack1" ) && _lastTrace.Hit )
        {
            // Attempt to get the mask component using the s&box standard .Get<T>()
            var mask = _lastTrace.GameObject.Components.Get<DishwasherDynamicMask>();
            
            if ( mask.IsValid() ) 
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
            // We draw from the ray's origin (the camera) to the hit point
            Vector3 endPos = _lastTrace.Hit ? _lastTrace.HitPosition : _lastRay.Project( ReachDistance );

            Gizmo.Draw.Color = _lastTrace.Hit ? Color.Green : Color.Red;
            Gizmo.Draw.LineThickness = 2f;
            Gizmo.Draw.Line( _lastRay.Position, endPos );

            // Visual feedback for hitting a valid dish
            var mask = _lastTrace.GameObject?.Components.Get<DishwasherDynamicMask>();
            if ( mask.IsValid() )
            {
                Gizmo.Draw.Color = Color.Yellow;
                Gizmo.Draw.SolidSphere( _lastTrace.HitPosition, 2f );
                
                if ( Input.Down( "attack1" ) )
                {
                    Gizmo.Draw.Text( "SCRUBBING", new Transform( _lastTrace.HitPosition + Vector3.Up * 10f ) );
                }
            }
        }
    }
}
