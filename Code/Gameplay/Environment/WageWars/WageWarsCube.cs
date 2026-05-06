using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Represents an individual interactable tile in the Wage Wars world.
/// Handles territory ownership, visual stacking (Value), and physics-based player bouncing.
/// </summary>
public sealed class WageWarsCube : Component
{
    // --- Networking & Persistence Data ---
    [Sync] public Guid OwnerId { get; set; }
    [Sync] public ulong OwnerSteamId { get; set; } // Persistent identifier for Save/Load
    [Sync] public int Value { get; set; } = 0; // Represents the height/level of the cube stack
    [Sync] public Vector2Int GridPosition { get; set; }

    // --- Scene References ---
    [Property, Group( "References" )] public TextRenderer TextComponent { get; set; }
    [Property, Group( "References" )] public ModelRenderer Renderer { get; set; }

    // --- Physics & Interaction Settings ---
    [Property, Group( "Physics Pop" )] public float BounceForce { get; set; } = 600f;
    [Property, Group( "Physics Pop" )] public float NudgeHeight { get; set; } = 70f;
    [Property, Group( "Physics Pop" )] public float BounceDelay { get; set; } = 0.05f;
    [Property, Group( "Physics Pop" )] public float DetectionHeight { get; set; } = 40f;
    [Property, Group( "Physics Pop" )] public float AttackBounceScale { get; set; } = 0.3f;
    [Property, Group( "Physics Pop" )] public int MaxRecoveryAttempts { get; set; } = 4;
    [Property, Group( "Physics Pop" )] public float StuckCheckRadius { get; set; } = 20f;
    
    // --- Visual Materials ---
    [Property, Group( "References" )] public Material NeutralMaterial { get; set; } 
    [Property, Group( "References" )] public Material ActiveMaterial { get; set; }
    
    // --- Sound Assets ---
    [Property, Group( "Audio" )] public SoundEvent PlaceSound { get; set; }
    [Property, Group( "Audio" )] public SoundEvent UpgradeSound { get; set; }
    [Property, Group( "Audio" )] public SoundEvent AttackSound { get; set; }

    private const float CubeHeight = 50f;

    // Prevents a single interaction from triggering multiple physics impulses on the same player
    private readonly HashSet<Guid> _bouncingPlayers = new();

    protected override void OnUpdate()
    {
        // Optimization: Do not process visual updates if the Chunk has disabled this renderer
        if ( !Renderer.Enabled ) return;
        UpdateVisuals();
    }

    /// <summary>
    /// Defines the 3D volume where players are detected for "labor" (bouncing).
    /// </summary>
    private BBox GetDetectionBox()
    {
        Vector3 mins = WorldPosition + Vector3.Up * ( (Value - 1) * CubeHeight ) - new Vector3( 25, 25, 0 );
        Vector3 maxs = WorldPosition + Vector3.Up * ( Value * CubeHeight ) + new Vector3( 25, 25, DetectionHeight );
        return new BBox( mins, maxs );
    }

    /// <summary>
    /// Updates materials and Tint colors based on ownership. 
    /// Uses Red for enemies and Green for the local player.
    /// </summary>
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

        // Apply owner color to all visual "Value" layers attached to this cube
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

    /// <summary>
    /// Primary logic for claiming, upgrading, or attacking a cube. Host-authoritative.
    /// </summary>
    public void ProcessTouch( Guid playerId )
    {
        if ( !Networking.IsHost ) return;

        ulong steamId = ResolveOwnerSteamId( playerId );

        if ( Value == 0 ) // Unclaimed
        {
           OwnerId      = playerId;
           OwnerSteamId = steamId;
           Value        = 1;
           ApplyPhysicalPop( BounceForce, true ); 
           SpawnVisualLayer();
           BroadcastPlaySound( "place" );
        }
        else if ( OwnerId == playerId ) // Upgrade existing
        {
           if ( CanUpgrade() )
           {
              Value++;
              ApplyPhysicalPop( BounceForce, true );
              SpawnVisualLayer();
              BroadcastPlaySound( "upgrade" );
           }
        }
        else if ( CanAttack( playerId ) ) // Enemy Siege
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
           return;
        }

        // Trigger persistence via the Authoritative Ledger model
        WageWarsWorldSave.Instance?.SaveWorld();
    }

    private ulong ResolveOwnerSteamId( Guid playerId )
    {
        var player = Scene.GetAllComponents<WageWarsPlayer>()
                          .FirstOrDefault( p => p.GameObject.Id == playerId );
        return player?.SteamId ?? 0;
    }

    // --- Visual Layer Management ---

    private void SpawnVisualLayer() => SpawnVisualLayerAt( Value );

    /// <summary>
    /// Creates a visual-only clone of the cube to represent height. 
    /// Removes functional components from the clone to prevent logic recursion.
    /// </summary>
    private void SpawnVisualLayerAt( int layerIndex )
    {
        var layer = GameObject.Clone();
        layer.Parent = GameObject;
        foreach ( var child in layer.Children.ToList() ) child.Destroy();
        
        layer.LocalPosition = Vector3.Up * ( layerIndex * CubeHeight );
        
        // Strip logic components from the visual clone
        var script = layer.Components.Get<WageWarsCube>();
        if ( script.IsValid() ) script.Destroy();
        var text = layer.Components.Get<TextRenderer>();
        if ( text.IsValid() ) text.Destroy();
        
        layer.NetworkSpawn();
    }

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

    // --- Physics Interaction (The "Pop") ---

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

    /// <summary>
    /// Detects players inside the cube's collision and triggers a broadcasted physics impulse.
    /// </summary>
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

        // Teleport slightly upward to prevent getting stuck in the floor before applying force
        if ( nudgeAmount > 0 )
           target.WorldPosition += Vector3.Up * nudgeAmount;
    
        rb.Velocity = rb.Velocity.WithZ( 0f );
        ApplyImpulseAfterDelay( rb, force );
    }

    /// <summary>
    /// Applies upward force. Includes a recovery loop to re-nudge players if they remain stuck in geometry.
    /// </summary>
    private async void ApplyImpulseAfterDelay( Rigidbody rb, float force )
    {
        await Task.DelayRealtimeSeconds( BounceDelay );
        if ( !rb.IsValid() ) return;

        for ( int attempt = 0; attempt < MaxRecoveryAttempts; attempt++ )
        {
            if ( !IsInsideGeometry( rb.GameObject ) ) break;

            rb.GameObject.WorldPosition += Vector3.Up * NudgeHeight;
            rb.Velocity = rb.Velocity.WithZ( 0f );

            await Task.DelayRealtimeSeconds( BounceDelay );
            if ( !rb.IsValid() ) return;
        }

        rb.ApplyImpulse( Vector3.Up * force * rb.Mass );
    }

    private bool IsInsideGeometry( GameObject go )
    {
        if ( !go.IsValid() ) return false;
        var checkPos = go.WorldPosition + Vector3.Up * 35f;
        var tr = Scene.Trace.Sphere( 4f, checkPos, checkPos ).WithTag( "land" ).Run();
        return tr.StartedSolid;
    }

    // --- Progression Rules (Labor -> Capital) ---

    /// <summary>
    /// Attack Rule: To siege a tower > Level 1, you must have an adjacent tower of higher level (Value + 1).
    /// </summary>
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

    /// <summary>
    /// Upgrade Rule: A cube can only level up if all 8 neighbors are at least the same level.
    /// </summary>
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
    
    // --- UI Feedback Strings ---
    
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

        for ( int x = -1; x <= 1; x++ )
        for ( int y = -1; y <= 1; y++ )
        {
           if ( x == 0 && y == 0 ) continue;
           var neighbor = WageWarsManager.Instance.GetCubeAt( GridPosition + new Vector2Int( x, y ) );
           if ( neighbor != null && neighbor.OwnerId == localId && neighbor.Value >= requiredHeight )
              return null;
        }

        return $"Requires adjacent Level {requiredHeight} tower to siege";
    }
}
