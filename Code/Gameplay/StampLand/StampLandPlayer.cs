using Sandbox;
using System;
using System.Threading.Tasks;

public sealed class StampLandPlayer : Component
{
    [Property, Group( "Stuck Recovery" )] public float StuckCheckInterval { get; set; } = 0.5f;
    [Property, Group( "Stuck Recovery" )] public float StuckCheckRadius { get; set; } = 4f;
    [Property, Group( "Stuck Recovery" )] public float DetectionVerticalOffset { get; set; } = 35f;
    [Property, Group( "Stuck Recovery" )] public float StuckRescueMargin { get; set; } = 10f;
    [Property, Group( "Stuck Recovery" )] public float StuckRescueBounce { get; set; } = 300f;

    private TimeSince _lastStuckCheck;
    private bool _rescueInProgress;

    protected override void OnUpdate()
    {
        if ( IsProxy ) return;

        // 1. Stuck Recovery (Unchanged)
        HandleStuckRecovery();

        // 2. Manual Labor Input
        if ( Input.Pressed( "use" ) )
        {
            TryPerformLabor();
        }
    }

    private void TryPerformLabor()
    {
	    var ray = Scene.Camera.ScreenNormalToRay( 0.5f );
	    var tr = Scene.Trace.Ray( ray, 150f ).WithTag( "land" ).Run();

	    if ( tr.Hit && tr.GameObject.IsValid() )
	    {
		    var cube = tr.GameObject.Components.GetInAncestorsOrSelf<StampLandCube>();
		    if ( cube.IsValid() )
		    {
			    // Send this character's unique GameObject.Id to the host
			    NotifyHostOfLabor( cube.GameObject, GameObject.Id ); 
		    }
	    }
    }

    [Rpc.Broadcast]
    public void NotifyHostOfLabor( GameObject cubeObj, Guid playerId )
    {
        if ( !Networking.IsHost ) return;
        if ( !cubeObj.IsValid() ) return;

        var cube = cubeObj.Components.Get<StampLandCube>();
        if ( cube.IsValid() )
        {
            cube.ProcessTouch( playerId );
        }
    }

    private void HandleStuckRecovery()
    {
        if ( _rescueInProgress || _lastStuckCheck < StuckCheckInterval ) return;
        _lastStuckCheck = 0;

        if ( IsInsideGeometry() )
        {
            var rb = Components.Get<Rigidbody>( FindMode.EverythingInSelf );
            if ( rb.IsValid() ) _ = RescueAsync( rb );
        }
    }

    // IsInsideGeometry now used by logic to decide if a bounce is needed
    public bool IsInsideGeometry()
    {
        Vector3 checkPos = WorldPosition + Vector3.Up * DetectionVerticalOffset;
        var tr = Scene.Trace.Sphere( StuckCheckRadius, checkPos, checkPos ).WithTag( "land" ).Run();
        return tr.StartedSolid;
    }

    private async Task RescueAsync( Rigidbody rb )
    {
        _rescueInProgress = true;
        for ( int i = 0; i < 6; i++ )
        {
            if ( !rb.IsValid() || !IsInsideGeometry() ) break;
            WorldPosition = WorldPosition.WithZ( FindSafeZAbove() );
            rb.Velocity = Vector3.Zero;
            await Task.DelayRealtimeSeconds( 0.05f );
        }
        if ( rb.IsValid() && !IsInsideGeometry() )
            rb.ApplyImpulse( Vector3.Up * StuckRescueBounce * rb.Mass );
        _rescueInProgress = false;
    }

    private float FindSafeZAbove()
    {
        var tr = Scene.Trace.Ray( WorldPosition + Vector3.Up * 800f, WorldPosition ).WithTag( "land" ).Run();
        return tr.Hit ? tr.HitPosition.z + StuckRescueMargin : WorldPosition.z + 60f;
    }
}
