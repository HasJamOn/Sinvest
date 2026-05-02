using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public sealed class WageWarsCube : Component
{
    [Sync] public Guid OwnerId { get; set; }
    /// <summary>
    /// Stable cross-session owner identity (Steam ID). Populated whenever OwnerId is
    /// set so WageWarsWorldSave can persist ownership without relying on the ephemeral
    /// GameObject Guid.
    /// </summary>
    [Sync] public ulong OwnerSteamId { get; set; }
    [Sync] public int Value { get; set; } = 0;
    [Sync] public Vector2Int GridPosition { get; set; }

    [Property, Group( "References" )] public TextRenderer TextComponent { get; set; }
    [Property, Group( "References" )] public ModelRenderer Renderer { get; set; }

    [Property, Group( "Physics Pop" )] public float BounceForce { get; set; } = 600f;
    [Property, Group( "Physics Pop" )] public float NudgeHeight { get; set; } = 70f;
    [Property, Group( "Physics Pop" )] public float BounceDelay { get; set; } = 0.05f;
    [Property, Group( "Physics Pop" )] public float DetectionHeight { get; set; } = 40f;
    [Property, Group( "Physics Pop" )] public float AttackBounceScale { get; set; } = 0.3f;
    [Property, Group( "Physics Pop" )] public int MaxRecoveryAttempts { get; set; } = 4;
    [Property, Group( "Physics Pop" )] public float StuckCheckRadius { get; set; } = 20f;
    
    [Property, Group( "References" )] public Material NeutralMaterial { get; set; } 
    [Property, Group( "References" )] public Material ActiveMaterial { get; set; }
    
    [Property, Group( "Audio" )] public SoundEvent PlaceSound { get; set; }
    [Property, Group( "Audio" )] public SoundEvent UpgradeSound { get; set; }
    [Property, Group( "Audio" )] public SoundEvent AttackSound { get; set; }

    private const float CubeHeight = 50f;

    private readonly HashSet<Guid> _bouncingPlayers = new();

    protected override void OnUpdate()
    {
        if ( !Renderer.Enabled ) return;
        UpdateVisuals();
    }

    private BBox GetDetectionBox()
    {
        Vector3 mins = WorldPosition + Vector3.Up * ( (Value - 1) * CubeHeight ) - new Vector3( 25, 25, 0 );
        Vector3 maxs = WorldPosition + Vector3.Up * ( Value * CubeHeight ) + new Vector3( 25, 25, DetectionHeight );
        return new BBox( mins, maxs );
    }

    private void UpdateVisuals()
    {
	    if ( !Renderer.IsValid() ) return;

	    if ( Value <= 0 || OwnerId == Guid.Empty )
	    {
		    Renderer.MaterialOverride = NeutralMaterial;
		    Renderer.Attributes.Set( "OwnerColor", Color.White );
		    Renderer.Tint = Color.White;
		    return; 
	    }

	    Renderer.MaterialOverride = ActiveMaterial; 

	    var myCharacter = Scene.GetAllComponents<WageWarsPlayer>().FirstOrDefault( p => !p.IsProxy );
    
	    Color territoryColor = new Color( 0.5f, 0.5f, 0.5f, 1.0f );

	    if ( myCharacter != null )
	    {
		    bool isMine = OwnerId == myCharacter.GameObject.Id;
		    territoryColor = isMine 
			    ? new Color( 0.0f, 1.0f, 0.0f, 1.0f )
			    : new Color( 1.0f, 0.0f, 0.0f, 1.0f );
	    }

	    Renderer.Attributes.Set( "OwnerColor", territoryColor );
	    Renderer.Tint = territoryColor;

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

	    // Resolve the stable Steam ID from the active player list so it can be
	    // persisted in the world save file.
	    ulong steamId = ResolveOwnerSteamId( playerId );

	    if ( Value == 0 )
	    {
		    OwnerId      = playerId;
		    OwnerSteamId = steamId;
		    Value        = 1;
		    ApplyPhysicalPop( BounceForce, true ); 
		    SpawnVisualLayer();
		    BroadcastPlaySound( "place" );
	    }
	    else if ( OwnerId == playerId )
	    {
		    if ( CanUpgrade() )
		    {
			    Value++;
			    ApplyPhysicalPop( BounceForce, true );
			    SpawnVisualLayer();
			    BroadcastPlaySound( "upgrade" );
		    }
	    }
	    else if ( CanAttack( playerId ) )
	    {
		    DespawnTopLayer();
		    Value--;
		    ApplyPhysicalPop( BounceForce * AttackBounceScale, false );
            
		    if ( Value <= 0 )
		    {
			    Value        = 0;
			    OwnerId      = Guid.Empty;
			    OwnerSteamId = 0;
		    }
		    BroadcastPlaySound( "attack" );
	    }
	    else
	    {
		    // No state change — nothing to persist.
		    return;
	    }

	    // Persist the world after every successful state change (host-only write).
	    WageWarsWorldSave.Instance?.SaveWorld();
    }

    /// <summary>
    /// Looks up the WageWarsPlayer whose GameObject.Id matches <paramref name="playerId"/>
    /// and returns their stable SteamId for persistence.
    /// </summary>
    private ulong ResolveOwnerSteamId( Guid playerId )
    {
	    var player = Scene.GetAllComponents<WageWarsPlayer>()
	                      .FirstOrDefault( p => p.GameObject.Id == playerId );
	    return player?.SteamId ?? 0;
    }

    // ── Visual layer helpers ───────────────────────────────────────────────────

    /// <summary>
    /// Spawns a single visual layer at the current Value height (used during live play).
    /// </summary>
    private void SpawnVisualLayer() => SpawnVisualLayerAt( Value );

    /// <summary>
    /// Spawns a visual layer at an explicit stack index. Used both during live play
    /// and during save restoration so the stack can be rebuilt without temporarily
    /// mutating Value.
    /// </summary>
    private void SpawnVisualLayerAt( int layerIndex )
    {
        var layer = GameObject.Clone();
        layer.Parent = GameObject;
        foreach ( var child in layer.Children.ToList() ) child.Destroy();
        layer.LocalPosition = Vector3.Up * ( layerIndex * CubeHeight );
        var script = layer.Components.Get<WageWarsCube>();
        if ( script.IsValid() ) script.Destroy();
        var text = layer.Components.Get<TextRenderer>();
        if ( text.IsValid() ) text.Destroy();
        layer.NetworkSpawn();
    }

    /// <summary>
    /// Rebuilds the entire visual stack from scratch for a cube whose Value and
    /// OwnerSteamId have already been set by WageWarsWorldSave.LoadWorld().
    /// Called only on the host during scene restore.
    /// </summary>
    public void RestoreVisualLayers()
    {
        for ( int i = 1; i <= Value; i++ )
            SpawnVisualLayerAt( i );
    }

    private void DespawnTopLayer()
    {
        var topLayer = GameObject.Children.LastOrDefault();
        topLayer?.Destroy();
    }

    // ── Physics bounce ─────────────────────────────────────────────────────────

    [Rpc.Broadcast]
    private void BroadcastPlaySound( string type )
    {
	    var soundToPlay = type switch
	    {
		    "place"   => PlaceSound,
		    "upgrade" => UpgradeSound,
		    "attack"  => AttackSound,
		    _         => null
	    };

	    if ( soundToPlay != null )
		    Sound.Play( soundToPlay, WorldPosition );
    }

    private void ApplyPhysicalPop( float force, bool isLabor )
    {
	    var players = Scene.GetAllComponents<WageWarsPlayer>();

	    foreach ( var player in players )
	    {
		    if ( !player.IsInsideGeometry() ) continue;

		    Guid playerId = player.GameObject.Id;
		    if ( _bouncingPlayers.Contains( playerId ) ) continue;

		    _bouncingPlayers.Add( playerId );
		    BroadcastBounce( playerId, isLabor ? NudgeHeight : 0f, force );
		    ReleaseBounceGuardAsync( playerId );
	    }
    }

    private async void ReleaseBounceGuardAsync( Guid playerId )
    {
        await Task.DelayRealtimeSeconds( BounceDelay * (MaxRecoveryAttempts + 3) );
        _bouncingPlayers.Remove( playerId );
    }

    [Rpc.Broadcast]
    private void BroadcastBounce( Guid targetId, float nudgeAmount, float force )
    {
	    var target = Scene.Directory.FindByGuid( targetId );
	    if ( !target.IsValid() || target.IsProxy ) return;

	    var rb = target.Components.Get<Rigidbody>();
	    if ( !rb.IsValid() ) return;

	    if ( nudgeAmount > 0 )
		    target.WorldPosition += Vector3.Up * nudgeAmount;
    
	    rb.Velocity = rb.Velocity.WithZ( 0f );
	    ApplyImpulseAfterDelay( rb, force );
    }

    private async void ApplyImpulseAfterDelay( Rigidbody rb, float force )
    {
        await Task.DelayRealtimeSeconds( BounceDelay );
        if ( !rb.IsValid() ) return;

        for ( int attempt = 0; attempt < MaxRecoveryAttempts; attempt++ )
        {
            if ( !IsInsideGeometry( rb.GameObject ) ) break;

            Log.Info( $"[POP] Stuck detected (attempt {attempt + 1}/{MaxRecoveryAttempts}) — re-nudging." );
            rb.GameObject.WorldPosition += Vector3.Up * NudgeHeight;
            rb.Velocity = rb.Velocity.WithZ( 0f );

            await Task.DelayRealtimeSeconds( BounceDelay );
            if ( !rb.IsValid() ) return;
        }

        rb.ApplyImpulse( Vector3.Up * force * rb.Mass );
        Log.Info( $"[POP] Bounce applied ({force * rb.Mass:F0} N)." );
    }

    private bool IsInsideGeometry( GameObject go )
    {
	    if ( !go.IsValid() ) return false;
	    var checkPos = go.WorldPosition + Vector3.Up * 35f;
	    var tr = Scene.Trace.Sphere( 4f, checkPos, checkPos ).WithTag( "land" ).Run();
	    return tr.StartedSolid;
    }

    // ── Upgrade / attack rules ─────────────────────────────────────────────────

    private bool CanAttack( Guid attackerId )
    {
	    if ( Value <= 1 ) return true;

	    int requiredHeight = Value + 1;
	    for ( int x = -1; x <= 1; x++ )
	    for ( int y = -1; y <= 1; y++ )
	    {
		    if ( x == 0 && y == 0 ) continue;
		    var neighbor = WageWarsManager.Instance.GetCubeAt( GridPosition + new Vector2Int( x, y ) );
		    if ( neighbor != null && neighbor.OwnerId == attackerId && neighbor.Value >= requiredHeight )
			    return true;
	    }
	    return false;
    }

    private bool CanUpgrade()
    {
        int requiredNeighborValue = Value;
        for ( int x = -1; x <= 1; x++ )
        for ( int y = -1; y <= 1; y++ )
        {
            if ( x == 0 && y == 0 ) continue;
            var neighbor = WageWarsManager.Instance.GetCubeAt( GridPosition + new Vector2Int( x, y ) );
            if ( neighbor == null || neighbor.Value < requiredNeighborValue )
                return false;
        }
        return true;
    }
    
    public string GetUpgradeRequirement()
    {
	    int requiredNeighborValue = Value;
	    for ( int x = -1; x <= 1; x++ )
	    for ( int y = -1; y <= 1; y++ )
	    {
		    if ( x == 0 && y == 0 ) continue;
		    var neighbor = WageWarsManager.Instance.GetCubeAt( GridPosition + new Vector2Int( x, y ) );
		    if ( neighbor == null || neighbor.Value < requiredNeighborValue )
			    return $"Surrounding tiles must be Level {requiredNeighborValue}";
	    }
	    return null;
    }
    
    public string GetAttackRequirement()
    {
	    if ( Value <= 1 ) return null;

	    var localPlayer = Scene.GetAllComponents<WageWarsPlayer>().FirstOrDefault( p => !p.IsProxy );
	    if ( localPlayer == null ) return null;
	    var localId = localPlayer.GameObject.Id;

	    int  requiredHeight = Value + 1;
	    bool hasHighGround  = false;

	    for ( int x = -1; x <= 1; x++ )
	    for ( int y = -1; y <= 1; y++ )
	    {
		    if ( x == 0 && y == 0 ) continue;
		    var neighbor = WageWarsManager.Instance.GetCubeAt( GridPosition + new Vector2Int( x, y ) );
		    if ( neighbor != null && neighbor.OwnerId == localId && neighbor.Value >= requiredHeight )
		    {
			    hasHighGround = true;
			    break;
		    }
		    if ( hasHighGround ) break;
	    }

	    return hasHighGround ? null : $"Requires adjacent Level {requiredHeight} tower to siege";
    }
}
