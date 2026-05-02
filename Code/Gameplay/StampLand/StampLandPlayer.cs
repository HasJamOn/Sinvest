using Sandbox;
using System;
using System.Threading.Tasks;

public sealed class StampLandPlayer : Component, Component.ITriggerListener
{
    private StampLandCube _lastTouchedCube;

    // ── Stuck Recovery ────────────────────────────────────────────────────────
    // Runs on the owning client only. Periodically probes whether the player is
    // inside solid "land" geometry and rescues them if so. By checking at waist 
    // height with a small radius, we allow players to hug walls and stand on 
    // surfaces without false positives.

    // How often (in seconds) to probe for a stuck state.
    [Property, Group( "Stuck Recovery" )] public float StuckCheckInterval { get; set; } = 0.5f;

    // Small radius (4f) allows for natural proximity to walls without triggering.
    [Property, Group( "Stuck Recovery" )] public float StuckCheckRadius { get; set; } = 4f;

    // We check 35 units up from the feet to ensure we aren't hitting the floor 
    // the player is standing on, but still catching a block they are buried in.
    [Property, Group( "Stuck Recovery" )] public float DetectionVerticalOffset { get; set; } = 35f;

    // Extra clearance above the detected cube surface to place the player after rescue.
    [Property, Group( "Stuck Recovery" )] public float StuckRescueMargin { get; set; } = 10f;

    // Upward impulse multiplier applied once the player is clear of geometry.
    [Property, Group( "Stuck Recovery" )] public float StuckRescueBounce { get; set; } = 300f;

    // Safety cap on how many times we re-nudge before giving up.
    [Property, Group( "Stuck Recovery" )] public int StuckMaxAttempts { get; set; } = 6;

    private TimeSince _lastStuckCheck;
    private bool _rescueInProgress;

    protected override void OnUpdate()
    {
       // Only the owning client runs this. Proxy players are rescued by their own machine.
       if ( IsProxy ) return;
       if ( _rescueInProgress ) return;
       if ( _lastStuckCheck < StuckCheckInterval ) return;

       _lastStuckCheck = 0;

       if ( IsInsideGeometry() )
       {
          Log.Info( "[RESCUE] Stuck in land geometry — starting rescue." );
          var rb = Components.Get<Rigidbody>( FindMode.EverythingInSelf );
          if ( rb.IsValid() ) _ = RescueAsync( rb );
       }
    }

    // ── Rescue sequence ───────────────────────────────────────────────────────
    // Each attempt: find the top surface of the current cube column by tracing 
    // straight down from high above, teleport there, zero velocity, and re-check.
    private async Task RescueAsync( Rigidbody rb )
    {
       _rescueInProgress = true;

       for ( int attempt = 0; attempt < StuckMaxAttempts; attempt++ )
       {
          if ( !rb.IsValid() ) break;
          if ( !IsInsideGeometry() ) break;

          float safeZ = FindSafeZAbove();
          Log.Info( $"[RESCUE] Attempt {attempt + 1}/{StuckMaxAttempts} → teleporting to Z={safeZ:F1}" );

          // Teleport feet to the safe height.
          WorldPosition = WorldPosition.WithZ( safeZ );
          rb.Velocity = Vector3.Zero;

          // Wait one physics tick for the engine to register the new position.
          await Task.DelayRealtimeSeconds( 0.05f );
       }

       // Final state: apply a gentle bounce to confirm freedom.
       if ( rb.IsValid() )
       {
          if ( !IsInsideGeometry() )
          {
             rb.ApplyImpulse( Vector3.Up * StuckRescueBounce * rb.Mass );
             Log.Info( "[RESCUE] Complete — recovery bounce applied." );
          }
          else
          {
             Log.Warning( $"[RESCUE] Failed after {StuckMaxAttempts} attempts. Player may need manual help." );
          }
       }

       _rescueInProgress = false;
    }

    // Casts straight down from 800 units above to find the top of the column.
    // This handles towers of any height.
    private float FindSafeZAbove()
    {
       Vector3 startPos = WorldPosition + Vector3.Up * 800f;

       var tr = Scene.Trace
          .Ray( startPos, WorldPosition )
          .WithTag( "land" )
          .Run();

       return tr.Hit
          ? tr.HitPosition.z + StuckRescueMargin
          : WorldPosition.z + 50f + StuckRescueMargin; // Fallback: one cube height up
    }

    // Sphere trace centered at the player's torso.
    // StartedSolid == true means the torso is overlapping solid land geometry.
    private bool IsInsideGeometry()
    {
       // Offset point to check torso space rather than the feet/floor contact point.
       Vector3 checkPos = WorldPosition + Vector3.Up * DetectionVerticalOffset;

       var tr = Scene.Trace
          .Sphere( StuckCheckRadius, checkPos, checkPos )
          .WithTag( "land" )
          .Run();

       return tr.StartedSolid;
    }

    // ── Trigger handling ──────────────────────────────────────────────────────

    public void OnTriggerEnter( Collider other )
    {
       if ( IsProxy ) return;

       var cube = other.GameObject.Components.GetInAncestorsOrSelf<StampLandCube>();

       if ( cube.IsValid() )
       {
          // Calculate the world Z position of the top of this specific layer.
          // other.WorldPosition is the center of the 50-unit cube; +25 finds the surface.
          float topSurfaceZ = other.WorldPosition.z + 25f;

          // Only trigger if the player's feet are at or above the top surface.
          // Using a 5-unit epsilon to allow for physics settling.
          if ( WorldPosition.z >= topSurfaceZ - 5f )
          {
             _lastTouchedCube = cube;
             NotifyHostOfLabor( cube.GameObject, Connection.Local.Id );

             Log.Info( $"[STAMP] Top-face contact confirmed on: {cube.GridPosition}" );
          }
          else
          {
             Log.Info( "[LABOR] Side-swipe ignored. Must stand on top." );
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

    public void OnTriggerExit( Collider other )
    {
       if ( IsProxy ) return;

       var cube = other.GameObject.Components.GetInAncestorsOrSelf<StampLandCube>();

       if ( _lastTouchedCube.IsValid() && cube == _lastTouchedCube )
       {
          Log.Info( $"[LABOR] Transaction Finalized: {_lastTouchedCube.GridPosition}" );
          _lastTouchedCube = null;
       }
    }
}
