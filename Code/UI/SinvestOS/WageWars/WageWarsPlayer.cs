using Sandbox;
using Sinvest; // EconomyManager, TransactionResult
using System;
using System.Threading.Tasks;

public sealed class WageWarsPlayer : Component
{
    // ── Persistent identity ───────────────────────────────────────────────────

    /// <summary>
    /// The player's Steam ID — stable across sessions and synced to all peers so
    /// WageWarsWorldSave can map saved OwnerSteamId values back to live Guids when
    /// players reconnect.
    /// </summary>
    [Sync] public ulong SteamId { get; set; }

    // ── Stuck recovery ────────────────────────────────────────────────────────

    [Property, Group( "Stuck Recovery" )] public float StuckCheckInterval { get; set; } = 0.5f;
    [Property, Group( "Stuck Recovery" )] public float StuckCheckRadius   { get; set; } = 4f;
    [Property, Group( "Stuck Recovery" )] public float DetectionVerticalOffset { get; set; } = 35f;
    [Property, Group( "Stuck Recovery" )] public float StuckRescueMargin  { get; set; } = 10f;
    [Property, Group( "Stuck Recovery" )] public float StuckRescueBounce  { get; set; } = 300f;

    private TimeSince _lastStuckCheck;
    private bool      _rescueInProgress;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    protected override void OnStart()
    {
        if ( IsProxy ) return;

        // Stamp our stable Steam ID so all peers (and WageWarsWorldSave) can read it.
        SteamId = Connection.Local.SteamId;

        // Tell the host to remap any cubes saved under this Steam ID to our fresh
        // GameObject Guid. Safe to call even when no save exists — it's a no-op then.
        WageWarsWorldSave.Instance?.ClaimRestoredCubes( SteamId, GameObject.Id );
    }

    // ── Per-frame ─────────────────────────────────────────────────────────────

    protected override void OnUpdate()
    {
        if ( IsProxy ) return;

        HandleStuckRecovery();

        if ( Input.Pressed( "use" ) )
            TryPerformLabor();
    }

    // ── Labor ─────────────────────────────────────────────────────────────────

    private void TryPerformLabor()
    {
        // 1. Confirm there is a valid cube in front of us BEFORE spending any shares,
        //    so a miss doesn't silently consume a share.
        var ray = Scene.Camera.ScreenNormalToRay( 0.5f );
        var tr  = Scene.Trace.Ray( ray, 150f ).WithTag( "land" ).Run();

        if ( !tr.Hit || !tr.GameObject.IsValid() ) return;

        var cube = tr.GameObject.Components.GetInAncestorsOrSelf<WageWarsCube>();
        if ( !cube.IsValid() ) return;

        // 2. Deduct 1 share from the player's economy. This is authoritative on
        //    the local client — the GameSaveSystem ledger records the outflow.
        //    If the player has no shares, labor is simply blocked.
        var economy = EconomyManager.Instance;
        if ( economy == null )
        {
            Log.Warning( "[LABOR] EconomyManager not found — labor blocked." );
            return;
        }

        var result = economy.SellShares( 1 );
        if ( result != TransactionResult.Success )
        {
            Log.Info( $"[LABOR] Share deduction failed ({result}) — labor blocked. You need at least 1 share to work." );
            // TODO: surface a UI hint to the player here (e.g. HUD flash / tooltip).
            return;
        }

        // 3. Notify the host to apply ProcessTouch on the targeted cube.
        NotifyHostOfLabor( cube.GameObject, GameObject.Id );
    }

    [Rpc.Broadcast]
    public void NotifyHostOfLabor( GameObject cubeObj, Guid playerId )
    {
        if ( !Networking.IsHost ) return;
        if ( !cubeObj.IsValid() ) return;

        var cube = cubeObj.Components.Get<WageWarsCube>();
        if ( cube.IsValid() )
            cube.ProcessTouch( playerId );
    }

    // ── Stuck recovery ────────────────────────────────────────────────────────

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
            rb.Velocity   = Vector3.Zero;
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
