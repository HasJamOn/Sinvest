using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public sealed class WageWarsCube : Component
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
    // How many extra nudges to attempt if the player is still inside geometry after
    // the initial nudge. Each attempt waits another BounceDelay before re-checking.
    [Property, Group( "Physics Pop" )] public int MaxRecoveryAttempts { get; set; } = 4;
    // Sphere radius used to probe whether the player is clipped into solid geometry.
    // Keep below 25 (half a cube side) so adjacent column walls don't cause false positives.
    [Property, Group( "Physics Pop" )] public float StuckCheckRadius { get; set; } = 20f;
    
    [Property, Group( "References" )] public Material NeutralMaterial { get; set; } 
    [Property, Group( "References" )] public Material ActiveMaterial { get; set; }
    
    [Property, Group( "Audio" )] public SoundEvent PlaceSound { get; set; }
    [Property, Group( "Audio" )] public SoundEvent UpgradeSound { get; set; }
    [Property, Group( "Audio" )] public SoundEvent AttackSound { get; set; }

    private const float CubeHeight = 50f;

    // Host-only. Tracks players who already have a bounce in flight so a rapid
    // second ProcessTouch on the same cube cannot queue a second bounce mid-air.
    private readonly HashSet<Guid> _bouncingPlayers = new();

    protected override void OnUpdate()
    {
        if ( !Renderer.Enabled ) return;
        UpdateVisuals();
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

	    // 1. MATERIAL SWITCHING
	    if ( Value <= 0 || OwnerId == Guid.Empty )
	    {
		    Renderer.MaterialOverride = NeutralMaterial; // floortile.vmat[cite: 1]
		    // Neutral tiles should stay white/original
		    Renderer.Attributes.Set( "OwnerColor", Color.White );
		    Renderer.Tint = Color.White;
		    return; 
	    }

	    // Use the money stack
	    Renderer.MaterialOverride = ActiveMaterial; 

	    // 2. IDENTITY RESOLUTION
	    var myCharacter = Scene.GetAllComponents<WageWarsPlayer>().FirstOrDefault( p => !p.IsProxy );
    
	    // If player isn't found, default to a neutral gray so it doesn't stay "stuck" red
	    Color territoryColor = new Color( 0.5f, 0.5f, 0.5f, 1.0f );

	    if ( myCharacter != null )
	    {
		    bool isMine = OwnerId == myCharacter.GameObject.Id;
		    territoryColor = isMine 
			    ? new Color( 0.0f, 1.0f, 0.0f, 1.0f ) // Green
			    : new Color( 1.0f, 0.0f, 0.0f, 1.0f ); // Red
	    }

	    // 3. APPLY TO RENDERER
	    // We set BOTH because the shader likely ignores Tint in favor of OwnerColor
	    Renderer.Attributes.Set( "OwnerColor", territoryColor );
	    Renderer.Tint = territoryColor;

	    // 4. APPLY TO CLONED LAYERS (Children)
	    foreach ( var child in GameObject.Children )
	    {
		    var childRenderer = child.Components.Get<ModelRenderer>();
		    if ( childRenderer.IsValid() )
		    {
			    childRenderer.MaterialOverride = ActiveMaterial;
			    childRenderer.Attributes.Set( "OwnerColor", territoryColor );
			    childRenderer.Tint = territoryColor;
		    }
	    }
    }

    public void ProcessTouch( Guid playerId )
    {
	    if ( !Networking.IsHost ) return;

	    if ( Value == 0 )
	    {
		    OwnerId = playerId;
		    Value = 1;
		    ApplyPhysicalPop( BounceForce, true ); 
		    SpawnVisualLayer();
		    BroadcastPlaySound( "place" ); // Trigger Sound
	    }
	    else if ( OwnerId == playerId )
	    {
		    if ( CanUpgrade() )
		    {
			    Value++;
			    ApplyPhysicalPop( BounceForce, true );
			    SpawnVisualLayer();
			    BroadcastPlaySound( "upgrade" ); // Trigger Sound
		    }
	    }
	    else if ( CanAttack( playerId ) )
	    {
		    DespawnTopLayer();
		    Value--;
		    ApplyPhysicalPop( BounceForce * AttackBounceScale, false );
            
		    if ( Value <= 0 )
		    {
			    Value = 0;
			    OwnerId = Guid.Empty;
		    }
		    BroadcastPlaySound( "attack" ); // Trigger Sound
	    }
    }
    
    [Rpc.Broadcast]
    private void BroadcastPlaySound( string type )
    {
	    // Use the SoundEvent properties assigned in the editor
	    var soundToPlay = type switch
	    {
		    "place" => PlaceSound,
		    "upgrade" => UpgradeSound,
		    "attack" => AttackSound,
		    _ => null
	    };

	    if ( soundToPlay != null )
	    {
		    // Plays at the cube's position. 
		    // Setting distance to 0f uses the SoundEvent's default attenuation.
		    Sound.Play( soundToPlay, WorldPosition );
	    }
    }

    // Called only on the host. Finds players standing on this cube and fires one
    // BroadcastBounce per player. The _bouncingPlayers guard ensures we never
    // queue a second bounce for a player whose first bounce hasn't resolved yet.
    private void ApplyPhysicalPop( float force, bool isLabor )
    {
	    var players = Scene.GetAllComponents<WageWarsPlayer>();

	    foreach ( var player in players )
	    {
		    // Is the player actually inside the block's future/current volume?
		    // We use the player's own detection logic here.
		    if ( !player.IsInsideGeometry() ) continue;

		    Guid playerId = player.GameObject.Id;
		    if ( _bouncingPlayers.Contains( playerId ) ) continue;

		    _bouncingPlayers.Add( playerId );
        
		    // For Labor (Green), we nudge then bounce. 
		    // For Attack (Red), we just bounce to keep them from falling through.
		    BroadcastBounce( playerId, isLabor ? NudgeHeight : 0f, force );

		    ReleaseBounceGuardAsync( playerId );
	    }
    }

    private async void ReleaseBounceGuardAsync( Guid playerId )
    {
        // Wait long enough for the full recovery sequence to complete:
        // initial delay + up to MaxRecoveryAttempts re-nudge delays + one frame of margin.
        await Task.DelayRealtimeSeconds( BounceDelay * (MaxRecoveryAttempts + 3) );
        _bouncingPlayers.Remove( playerId );
    }

    // [Rpc.Broadcast] — runs on every peer, but the IsProxy check means only the
    // player-owning client actually modifies physics. The host and all other clients
    // skip out immediately, so there is no risk of double-application.
    [Rpc.Broadcast]
    private void BroadcastBounce( Guid targetId, float nudgeAmount, float force )
    {
	    var target = Scene.Directory.FindByGuid( targetId );
	    if ( !target.IsValid() || target.IsProxy ) return;

	    var rb = target.Components.Get<Rigidbody>();
	    if ( !rb.IsValid() ) return;

	    // Apply the nudge only if nudgeAmount > 0
	    if ( nudgeAmount > 0 )
	    {
		    target.WorldPosition += Vector3.Up * nudgeAmount;
	    }
    
	    rb.Velocity = rb.Velocity.WithZ( 0f );

	    // Apply impulse after the same delay to ensure physics sync
	    ApplyImpulseAfterDelay( rb, force );
    }

    private async void ApplyImpulseAfterDelay( Rigidbody rb, float force )
    {
        await Task.DelayRealtimeSeconds( BounceDelay );
        if ( !rb.IsValid() ) return;

        // ── Stuck recovery ────────────────────────────────────────────────────────
        // After the initial nudge the player should be floating above the new layer.
        // On high-latency clients or during a rapid spawn, they can end up clipped
        // into the geometry anyway. We probe their position each iteration and keep
        // nudging upward until they're clear, or until we hit MaxRecoveryAttempts.
        // This runs entirely on the owning client — no RPC needed, no host involvement.
        for ( int attempt = 0; attempt < MaxRecoveryAttempts; attempt++ )
        {
            if ( !IsInsideGeometry( rb.GameObject ) ) break;

            Log.Info( $"[POP] Stuck detected (attempt {attempt + 1}/{MaxRecoveryAttempts}) — re-nudging." );

            rb.GameObject.WorldPosition += Vector3.Up * NudgeHeight;
            rb.Velocity = rb.Velocity.WithZ( 0f );

            await Task.DelayRealtimeSeconds( BounceDelay );
            if ( !rb.IsValid() ) return;
        }

        // Apply the final upward impulse. By this point the player is guaranteed to
        // be (or have been nudged to be) outside solid geometry, so the full force
        // translates into actual vertical velocity rather than being absorbed by a
        // ground-contact constraint.
        rb.ApplyImpulse( Vector3.Up * force * rb.Mass );
        Log.Info( $"[POP] Bounce applied ({force * rb.Mass:F0} N)." );
    }

    // Runs on the owning client only (called from within the !IsProxy branch of BroadcastBounce).
    // Uses a zero-length sphere trace: StartedSolid == true means the origin point
    // is overlapping solid geometry — the exact definition of "stuck inside a block".
    // StuckCheckRadius (default 20f) is kept well below 25 (half a cube side) so the
    // probe doesn't accidentally reach into the walls of neighbouring columns.
    private bool IsInsideGeometry( GameObject go )
    {
	    if ( !go.IsValid() ) return false;

	    // Use a small radius and a vertical offset here as well.
	    // This ensures the "post-bounce" check doesn't fail just because
	    // the player is standing near a wall.
	    var checkPos = go.WorldPosition + Vector3.Up * 35f;

	    var tr = Scene.Trace
		    .Sphere( 4f, checkPos, checkPos )
		    .WithTag( "land" )
		    .Run();

	    return tr.StartedSolid;
    }

    private void SpawnVisualLayer()
    {
        var layer = GameObject.Clone();
        layer.Parent = GameObject;
        foreach ( var child in layer.Children.ToList() ) child.Destroy();
        layer.LocalPosition = Vector3.Up * ( Value * CubeHeight );
        var script = layer.Components.Get<WageWarsCube>();
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
	    // Rule: Level 1 cubes can be claimed/attacked by anyone to ensure map flow
	    if ( Value <= 1 ) return true;

	    // Rule: To attack a Level N cube, you must have an adjacent cube that is at least Level N + 1
	    int requiredHeight = Value + 1;

	    for ( int x = -1; x <= 1; x++ )
	    {
		    for ( int y = -1; y <= 1; y++ )
		    {
			    if ( x == 0 && y == 0 ) continue;
            
			    var neighbor = WageWarsManager.Instance.GetCubeAt( GridPosition + new Vector2Int( x, y ) );
            
			    // Check if any neighbor is owned by the attacker and is strictly taller
			    if ( neighbor != null && neighbor.OwnerId == attackerId && neighbor.Value >= requiredHeight )
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
                var neighbor = WageWarsManager.Instance.GetCubeAt( GridPosition + new Vector2Int( x, y ) );
                if ( neighbor == null || neighbor.Value < requiredNeighborValue )
                    return false;
            }
        }
        return true;
    }
    
    public string GetUpgradeRequirement()
    {
	    int requiredNeighborValue = Value;
	    for ( int x = -1; x <= 1; x++ )
	    {
		    for ( int y = -1; y <= 1; y++ )
		    {
			    if ( x == 0 && y == 0 ) continue;
			    var neighbor = WageWarsManager.Instance.GetCubeAt( GridPosition + new Vector2Int( x, y ) );
            
			    if ( neighbor == null || neighbor.Value < requiredNeighborValue )
			    {
				    return $"Surrounding tiles must be Level {requiredNeighborValue}";
			    }
		    }
	    }
	    return null; // All conditions met
    }
    
    public string GetAttackRequirement()
    {
	    // Entry level cubes are always vulnerable
	    if ( Value <= 1 ) return null;

	    // Find the local player's ID instead of Connection.Local.Id
	    var localPlayer = Scene.GetAllComponents<WageWarsPlayer>().FirstOrDefault( p => !p.IsProxy );
	    if ( localPlayer == null ) return null;
	    var localId = localPlayer.GameObject.Id;

	    int requiredHeight = Value + 1;
	    bool hasHighGround = false;

	    for ( int x = -1; x <= 1; x++ )
	    {
		    for ( int y = -1; y <= 1; y++ )
		    {
			    if ( x == 0 && y == 0 ) continue;
            
			    var neighbor = WageWarsManager.Instance.GetCubeAt( GridPosition + new Vector2Int( x, y ) );
			    if ( neighbor != null && neighbor.OwnerId == localId && neighbor.Value >= requiredHeight )
			    {
				    hasHighGround = true;
				    break;
			    }
		    }
		    if ( hasHighGround ) break;
	    }

	    if ( !hasHighGround )
	    {
		    return $"Requires adjacent Level {requiredHeight} tower to siege";
	    }

	    return null;
    }
}
