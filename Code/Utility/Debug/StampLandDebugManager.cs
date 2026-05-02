using Sandbox;
using System;
using System.Linq;

[Group( "Sinvest" )]
public sealed class WageWarsDebugManager : Component
{
    [Property] public bool ShowWorldLabels { get; set; } = true;
    [Property] public bool TraceInteractionRay { get; set; } = true;
    [Property] public bool LogOwnershipChanges { get; set; } = true;

    protected override void OnUpdate()
    {
	    if ( !ShowWorldLabels ) return;

	    // Visualizing Identity for all players in world-space
	    foreach ( var player in Scene.GetAllComponents<WageWarsPlayer>() )
	    {
		    var labelColor = player.IsProxy ? new Color( 1f, 0.4f, 0f, 1f ) : new Color( 0f, 1f, 0f, 1f );
		    var labelText = player.IsProxy ? $"ENEMY: {player.GameObject.Name}" : "LOCAL PLAYER (YOU)";
        
		    // FIX: Wrap Vector3 in a Transform
		    Gizmo.Draw.Color = labelColor;
		    var textPos = player.WorldPosition + Vector3.Up * 80f;
		    Gizmo.Draw.Text( $"{labelText}\nID: {player.GameObject.Id}", new Transform( textPos ) );
	    }

	    // Draw Cube Ownership stats on hover
	    var ray = Scene.Camera.ScreenNormalToRay( 0.5f );
	    var tr = Scene.Trace.Ray( ray, 500f ).WithTag( "land" ).Run();

	    if ( tr.Hit && tr.GameObject.IsValid() )
	    {
		    var cube = tr.GameObject.Components.GetInAncestorsOrSelf<WageWarsCube>();
		    if ( cube.IsValid() )
		    {
			    if ( TraceInteractionRay )
			    {
				    Gizmo.Draw.Color = new Color( 1f, 1f, 1f, 0.5f );
				    Gizmo.Draw.Line( ray.Position, tr.HitPosition );
			    }

			    string ownerDisplay = cube.OwnerId == Guid.Empty ? "NEUTRAL" : $"OWNER: {cube.OwnerId}";
			    Gizmo.Draw.Color = Color.White;
            
			    // FIX: Wrap Vector3 in a Transform
			    var cubeTextPos = tr.HitPosition + Vector3.Up * 20f;
			    Gizmo.Draw.Text( $"CUBE DATA:\n{ownerDisplay}\nLevel: {cube.Value}", new Transform( cubeTextPos ) );
		    }
	    }
    }

    [Button( "Force Re-sync Local Character" )]
    public void ForceSync()
    {
        var local = Scene.GetAllComponents<WageWarsPlayer>().FirstOrDefault( p => !p.IsProxy );
        if ( local != null )
        {
            Log.Info( $"[DEBUG] Local Character Confirmed: {local.GameObject.Name} ({local.GameObject.Id})" );
        }
        else
        {
            Log.Warning( "[DEBUG] No local character found! Check NetworkHelper spawning." );
        }
    }

    [Button( "Clear All Ownership (HOST ONLY)" )]
    public void ResetMap()
    {
        if ( !Networking.IsHost )
        {
            Log.Error( "[DEBUG] Only the Host can reset map state." );
            return;
        }

        foreach ( var cube in Scene.GetAllComponents<WageWarsCube>() )
        {
            cube.Value = 0;
            cube.OwnerId = Guid.Empty;
        }
        Log.Info( "[DEBUG] Map ownership has been wiped." );
    }
}
