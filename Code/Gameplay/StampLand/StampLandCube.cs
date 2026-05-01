using Sandbox;
using System;

public sealed class StampLandCube : Component
{
    [Sync] public Guid OwnerId { get; set; }
    [Sync] public int Value { get; set; } = 0;
    
    /// <summary>
    /// Set by StampLandManager during generation to allow neighbor lookups.
    /// </summary>
    [Sync] public Vector2Int GridPosition { get; set; }

    [Property, Group("References")] public TextRenderer TextComponent { get; set; }
    [Property, Group("References")] public ModelRenderer Renderer { get; set; }

    protected override void OnUpdate()
    {
       // If the chunk culling has disabled the renderer, skip logic
       if ( !Renderer.Enabled ) return;

       if ( TextComponent.IsValid() )
          TextComponent.Text = Value > 0 ? Value.ToString() : "";

       UpdateVisuals();
    }

    private void UpdateVisuals()
    {
       if ( Value == 0 )
       {
          Renderer.Tint = new Color( 0.5f, 0.5f, 0.5f, 1.0f ); 
          return;
       }

       // Local vs Remote Player coloring
       Renderer.Tint = (OwnerId == Connection.Local.Id) 
          ? new Color( 0.0f, 1.0f, 0.0f, 1.0f ) // Green
          : new Color( 1.0f, 0.0f, 0.0f, 1.0f ); // Red
    }

    public void ProcessTouch( Guid playerId )
    {
	    if ( !Networking.IsHost ) return;

	    // 1. CLAIMING EMPTY LAND
	    if ( Value == 0 ) 
	    { 
		    OwnerId = playerId; 
		    Value = 1; 
	    }
	    // 2. UPGRADING OWN LAND (Capital Progression)
	    else if ( OwnerId == playerId ) 
	    {
		    if ( CanUpgrade() )
		    {
			    Value++;
		    }
	    }
	    // 3. ATTACKING HOSTILE LAND (Labor Sabotage)
	    else 
	    {
		    // NEW: Requirement to attack hostile cubes
		    // To decrease a Value 3 cube, you must have a neighbor that is >= Value 2
		    // This forces players to "build a bridge" of their own territory to the enemy
		    if ( CanAttack( playerId ) )
		    {
			    Value--;
			    if ( Value <= 0 ) 
			    { 
				    Value = 0; 
				    OwnerId = Guid.Empty; 
			    }
		    }
		    else
		    {
			    Log.Info( "Attack blocked: You must build territory adjacent to this cube first." );
		    }
	    }
    }

    /// <summary>
    /// Requirement: To attack a cube of Value N, you must own at least one 
    /// neighboring cube of Value N-1 or higher.
    /// </summary>
    private bool CanAttack( Guid attackerId )
    {
	    // Level 1 cubes can always be attacked (the "Frontier")
	    if ( Value <= 1 ) return true;

	    int requiredSupportValue = Value - 1;

	    for ( int x = -1; x <= 1; x++ )
	    {
		    for ( int y = -1; y <= 1; y++ )
		    {
			    if ( x == 0 && y == 0 ) continue;

			    Vector2Int neighborCoords = GridPosition + new Vector2Int( x, y );
			    var neighbor = StampLandManager.Instance.GetCubeAt( neighborCoords );

			    // Look for a neighbor owned by the attacker with sufficient tier
			    if ( neighbor != null && neighbor.OwnerId == attackerId && neighbor.Value >= requiredSupportValue )
			    {
				    return true;
			    }
		    }
	    }

	    return false;
    }

    /// <summary>
    /// Requirement: All 8 neighbors (including diagonals) must have a Value 
    /// greater than or equal to this cube's current Value.
    /// </summary>
    private bool CanUpgrade()
    {
        int requiredNeighborValue = Value;

        for ( int x = -1; x <= 1; x++ )
        {
            for ( int y = -1; y <= 1; y++ )
            {
                // Skip the center cube (self)
                if ( x == 0 && y == 0 ) continue;

                Vector2Int neighborCoords = GridPosition + new Vector2Int( x, y );
                var neighbor = StampLandManager.Instance.GetCubeAt( neighborCoords );

                // If neighbor is missing (edge of world) or value is too low, block upgrade
                if ( neighbor == null || neighbor.Value < requiredNeighborValue )
                    return false;
            }
        }

        return true;
    }
}
