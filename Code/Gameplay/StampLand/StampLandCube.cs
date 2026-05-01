using Sandbox;
using System;
using System.Linq;

public sealed class StampLandCube : Component
{
    [Sync] public Guid OwnerId { get; set; }
    [Sync] public int Value { get; set; } = 0;
    [Sync] public Vector2Int GridPosition { get; set; }

    [Property, Group( "References" )] public TextRenderer TextComponent { get; set; }
    [Property, Group( "References" )] public ModelRenderer Renderer { get; set; }

    [Property, Group( "Physics Pop" )] public float BounceForce { get; set; } = 600f; // Buffed default
    [Property, Group( "Physics Pop" )] public float NudgeAmount { get; set; } = 20f;
    [Property, Group( "Physics Pop" )] public float DetectionHeight { get; set; } = 40f;
    [Property, Group( "Physics Pop" )] public float AttackBounceScale { get; set; } = 0.3f;

    private const float CubeHeight = 50f;

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

    private void ApplyPhysicalPop( float force )
    {
        var bbox = GetDetectionBox();
        var players = Scene.GetAllComponents<PlayerController>();

        foreach ( var controller in players )
        {
           if ( bbox.Contains( controller.WorldPosition ) )
           {
              // CRITICAL: We tell the player's object to bounce itself via RPC
              // This ensures the physics change happens on the owner's side
              BroadcastBounce( controller.GameObject.Id, force );
           }
        }
    }

    [Rpc.Broadcast]
    private void BroadcastBounce( Guid targetId, float force )
    {
        var target = Scene.Directory.FindByGuid( targetId );
        if ( !target.IsValid() || target.IsProxy ) return;

        var rb = target.Components.Get<Rigidbody>();
        if ( rb.IsValid() )
        {
            // 1. Instant Nudge to break ground contact
            target.WorldPosition += Vector3.Up * NudgeAmount;

            // 2. Reset and Pop
            rb.Velocity = rb.Velocity.WithZ( 0 );
            rb.ApplyImpulse( Vector3.Up * force * rb.Mass );
            
            Log.Info( $"Bounce applied locally to {target.Name}" );
        }
    }

    // ... (Keep SpawnVisualLayer, DespawnTopLayer, CanAttack, CanUpgrade as they were)
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
