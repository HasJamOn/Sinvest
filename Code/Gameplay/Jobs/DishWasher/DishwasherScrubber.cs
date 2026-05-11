using Sandbox;
using System;
using System.Collections.Generic;

namespace Sinvest;

public sealed class DishwasherScrubber : Component
{
    [Property] public float ReachDistance { get; set; } = 500f;
    [Property] public float ScrubRadius { get; set; } = 15f;
    [Property] public float NormalThreshold { get; set; } = -0.05f;

    /// <summary>
    /// Tracks decals currently being touched to prevent rapid-fire cleaning.
    /// </summary>
    private readonly HashSet<Guid> _hitSession = new();
    private SceneTraceResult _positionTrace;

    protected override void OnUpdate()
    {
        var ray = Scene.Camera.ScreenPixelToRay( Mouse.Position );

        _positionTrace = Scene.Trace
            .Ray( ray, ReachDistance )
            .UsePhysicsWorld()
            .Run();

        if ( _positionTrace.Hit && Input.Down( "attack1" ) )
        {
            if ( _positionTrace.GameObject.Components.TryGet<DishwasherDirtManager>( out var manager ) )
            {
                // We pass the session to the manager so it can filter/add to it
                manager.TryCleanAt( _positionTrace.HitPosition, _positionTrace.Normal, ScrubRadius, _hitSession );
            }
        }

        // If we stop scrubbing or move away, we need logic to clear the session.
        // For "move away and come back," we check which decals are NO LONGER within the radius.
        UpdateHitSession();
    }

    private void UpdateHitSession()
    {
	    if ( !Input.Down( "attack1" ) || !_positionTrace.Hit )
	    {
		    _hitSession.Clear();
		    return;
	    }

	    // Get the manager component from the object we are hitting
	    if ( !_positionTrace.GameObject.Components.TryGet<DishwasherDirtManager>( out var manager ) )
		    return;

	    _hitSession.RemoveWhere( id => 
	    {
		    // GetAllObjects needs a bool for 'includeDisabled'
		    var go = Scene.GetAllObjects( true ).FirstOrDefault( x => x.Id == id );
        
		    if ( !go.IsValid() ) return true; 

		    float dist = Vector3.DistanceBetween( go.WorldPosition, _positionTrace.HitPosition );
        
		    // Use manager.DecalSize instead of just DecalSize
		    return dist > ScrubRadius + (manager.DecalSize * 0.5f); 
	    });
    }
}
