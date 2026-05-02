using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public sealed class StampLandCube : Component
{
    [Sync] public Guid OwnerId { get; set; }
    [Sync] public int Value { get; set; } = 0;
    [Sync] public Vector2Int GridPosition { get; set; }

    [Property, Group( "References" )] public TextRenderer TextComponent { get; set; }
    [Property, Group( "References" )] public ModelRenderer Renderer { get; set; }

    [Property, Group( "Physics Pop" )] public float BounceForce { get; set; } = 600f;
    // NudgeHeight replaces the old NudgeAmount. It lifts the player fully clear of the
    // newly spawned layer (CubeHeight = 50) plus comfortable headroom. Tune upward if
    // players clip through the new surface before the bounce fires.
    [Property, Group( "Physics Pop" )] public float NudgeHeight { get; set; } = 70f;
    // Seconds between the nudge (frame N) and the impulse (frame N+1).
    // 0.05 s ≈ 3 frames at 60 fps — long enough for the physics engine to register
    // the new position but short enough to feel instant to the player.
    [Property, Group( "Physics Pop" )] public float BounceDelay { get; set; } = 0.05f;
    [Property, Group( "Physics Pop" )] public float DetectionHeight { get; set; } = 40f;
    [Property, Group( "Physics Pop" )] public float AttackBounceScale { get; set; } = 0.3f;

    private const float CubeHeight = 50f;

    // Host-only. Tracks players who already have a bounce in flight so a rapid
    // second ProcessTouch on the same cube cannot queue a second bounce mid-air.
    private readonly HashSet<Guid> _bouncingPlayers = new();

    protected override void OnUpdate()
    {
        if ( !Renderer.Enabled ) return;
        UpdateVisuals();

        // DEBUG: Draw the detection box so you can see it in the Scene view
        var bbox = GetDetectionBox();
        Gizmo.Draw.Color = Color.Yellow.WithAlpha( 0.2f );
        Gizmo.Draw.LineBBox( bbox );
    }

    private BBox GetDetectionBox()
    {
        // Start from the current top surface and look up
        Vector3 mins = WorldPosition + Vector3.Up * ( (Value - 1) * CubeHeight ) - new Vector3( 25, 25, 0 );
        Vector3 maxs = WorldPosition + Vector3.Up * ( Value * CubeHeight ) + new Vector3( 25, 25, DetectionHeight );
        return new BBox( mins, maxs );
    }

    private void UpdateVisuals()
    {
        if ( !Renderer.IsValid() ) return;
        Renderer.Tint = new Color( 0.5f, 0.5f, 0.5f, 1.0f );

        Color towerColor = (OwnerId == Connection.Local.Id)
           ? new Color( 0.0f, 1.0f, 0.0f, 1.0f )
           : new Color( 1.0f, 0.0f, 0.0f, 1.0f );

        foreach ( var child in GameObject.Children )
        {
            var childRenderer = child.Components.Get<ModelRenderer>();
            if ( childRenderer.IsValid() ) childRenderer.Tint = towerColor;
        }

        if ( TextComponent.IsValid() )
        {
            TextComponent.Enabled = true;
            TextComponent.Text = Value.ToString();
            TextComponent.LocalPosition = Vector3.Up * ( (Value * CubeHeight) + 26f );
        }
    }

    public void ProcessTouch( Guid playerId )
    {
        if ( !Networking.IsHost ) return;

        if ( Value == 0 )
        {
            OwnerId = playerId;
            Value = 1;
            ApplyPhysicalPop( BounceForce );
            SpawnVisualLayer();
        }
        else if ( OwnerId == playerId )
        {
            if ( CanUpgrade() )
            {
                Value++;
                ApplyPhysicalPop( BounceForce );
                SpawnVisualLayer();
            }
        }
        else
        {
            if ( CanAttack( playerId ) )
            {
                DespawnTopLayer();
                Value--;
                ApplyPhysicalPop( BounceForce * AttackBounceScale );

                if ( Value <= 0 )
                {
                    Value = 0;
                    OwnerId = Guid.Empty;
                }
            }
        }
    }

    // Called only on the host. Finds players standing on this cube and fires one
    // BroadcastBounce per player. The _bouncingPlayers guard ensures we never
    // queue a second bounce for a player whose first bounce hasn't resolved yet.
    private void ApplyPhysicalPop( float force )
    {
        var bbox = GetDetectionBox();
        var players = Scene.GetAllComponents<PlayerController>();

        foreach ( var controller in players )
        {
            if ( !bbox.Contains( controller.WorldPosition ) ) continue;

            Guid playerId = controller.GameObject.Id;

            if ( _bouncingPlayers.Contains( playerId ) )
            {
                Log.Info( $"[POP] Bounce already in flight for {controller.GameObject.Name}, skipping." );
                continue;
            }

            _bouncingPlayers.Add( playerId );
            BroadcastBounce( playerId, NudgeHeight, force );

            // Release the guard after the bounce has fully resolved.
            // BounceDelay * 3 gives the deferred impulse time to fire plus a frame of margin.
            ReleaseBounceGuardAsync( playerId );
        }
    }

    private async void ReleaseBounceGuardAsync( Guid playerId )
    {
        await Task.DelayRealtimeSeconds( BounceDelay * 3f );
        _bouncingPlayers.Remove( playerId );
    }

    // [Rpc.Broadcast] — runs on every peer, but the IsProxy check means only the
    // player-owning client actually modifies physics. The host and all other clients
    // skip out immediately, so there is no risk of double-application.
    [Rpc.Broadcast]
    private void BroadcastBounce( Guid targetId, float nudgeHeight, float force )
    {
        var target = Scene.Directory.FindByGuid( targetId );

        // IsProxy == true on every peer except the one that owns this player object.
        // This guarantees physics changes happen exactly once, on the right machine.
        if ( !target.IsValid() || target.IsProxy ) return;

        var rb = target.Components.Get<Rigidbody>();
        if ( !rb.IsValid() ) return;

        // ── Frame N ──────────────────────────────────────────────────────────────
        // Move the player above the new surface. This is a direct position write,
        // not a physics impulse, so it takes effect immediately regardless of
        // ground-contact state. Zeroing Z velocity prevents downward momentum from
        // fighting the upcoming impulse.
        target.WorldPosition += Vector3.Up * nudgeHeight;
        rb.Velocity = rb.Velocity.WithZ( 0f );

        // ── Frame N+1 ─────────────────────────────────────────────────────────────
        // Defer the impulse so the physics engine has one full tick to register the
        // new position (no ground contact). Applying force in the same frame as the
        // nudge risks the engine still considering the player grounded, which would
        // absorb part of the upward impulse.
        ApplyImpulseAfterDelay( rb, force );

        Log.Info( $"[POP] Nudge applied to {target.Name}. Impulse pending in {BounceDelay}s." );
    }

    private async void ApplyImpulseAfterDelay( Rigidbody rb, float force )
    {
        await Task.DelayRealtimeSeconds( BounceDelay );

        // Guard: object may have been destroyed during the delay (e.g. player left).
        if ( !rb.IsValid() ) return;

        rb.ApplyImpulse( Vector3.Up * force * rb.Mass );

        Log.Info( $"[POP] Deferred impulse applied ({force * rb.Mass} N)." );
    }

    private void SpawnVisualLayer()
    {
        var layer = GameObject.Clone();
        layer.Parent = GameObject;
        foreach ( var child in layer.Children.ToList() ) child.Destroy();
        layer.LocalPosition = Vector3.Up * ( Value * CubeHeight );
        var script = layer.Components.Get<StampLandCube>();
        if ( script.IsValid() ) script.Destroy();
        var text = layer.Components.Get<TextRenderer>();
        if ( text.IsValid() ) text.Destroy();
        layer.NetworkSpawn();
    }

    private void DespawnTopLayer()
    {
        var topLayer = GameObject.Children.LastOrDefault();
        topLayer?.Destroy();
    }

    private bool CanAttack( Guid attackerId )
    {
        if ( Value <= 1 ) return true;
        int requiredSupportValue = Value - 1;
        for ( int x = -1; x <= 1; x++ )
        {
            for ( int y = -1; y <= 1; y++ )
            {
                if ( x == 0 && y == 0 ) continue;
                var neighbor = StampLandManager.Instance.GetCubeAt( GridPosition + new Vector2Int( x, y ) );
                if ( neighbor != null && neighbor.OwnerId == attackerId && neighbor.Value >= requiredSupportValue )
                    return true;
            }
        }
        return false;
    }

    private bool CanUpgrade()
    {
        int requiredNeighborValue = Value;
        for ( int x = -1; x <= 1; x++ )
        {
            for ( int y = -1; y <= 1; y++ )
            {
                if ( x == 0 && y == 0 ) continue;
                var neighbor = StampLandManager.Instance.GetCubeAt( GridPosition + new Vector2Int( x, y ) );
                if ( neighbor == null || neighbor.Value < requiredNeighborValue )
                    return false;
            }
        }
        return true;
    }
}
