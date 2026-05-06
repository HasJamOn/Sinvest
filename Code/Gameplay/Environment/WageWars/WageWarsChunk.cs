using Sandbox;
using System.Collections.Generic;

/// <summary>
/// Manages Level of Detail (LOD) and visibility for a collection of cubes within a specific coordinate grid.
/// Optimization: Toggles component state based on camera proximity to reduce draw calls and UI overhead.
/// </summary>
public sealed class WageWarsChunk : Component
{
    [Property] public Vector2Int ChunkCoords { get; set; }
    
    // Internal cache of WageWarsCube components contained within this chunk for batch updates.
    private List<WageWarsCube> _cubes = new();
    
    // Tracks current visibility state to prevent redundant state assignments in the update loop.
    private bool _isVisible = true;

    protected override void OnStart()
    {
       // Populates the cube cache by locating all WageWarsCube components in the current GameObject and its hierarchy.
       // Only called once; assumes the chunk's cube population is static after instantiation.
       _cubes.AddRange( Components.GetAll<WageWarsCube>( FindMode.EverythingInSelfAndChildren ) );
    }

    protected override void OnUpdate()
    {
       // Procedural rendering optimization requires a valid Scene Camera to calculate distance.
       if ( Scene.Camera is null ) return;

       // Calculates Euclidean distance between the chunk origin and the active camera.
       float dist = Vector3.DistanceBetween( WorldPosition, Scene.Camera.WorldPosition );
       
       // Binary visibility check based on a fixed 3000 unit threshold.
       bool shouldBeVisible = dist < 3000f;

       // Only execute state changes if the visibility status has transitioned.
       if ( shouldBeVisible != _isVisible )
       {
          _isVisible = shouldBeVisible;
          
          foreach ( var cube in _cubes )
          {
             // Updates the enabled state of renderers and text components to reduce draw calls when out of range.
             // Verification: Validates handles to prevent exceptions if a cube layer was destroyed (e.g., during an attack).
             if ( cube.Renderer.IsValid() ) cube.Renderer.Enabled = _isVisible;
             if ( cube.TextComponent.IsValid() ) cube.TextComponent.Enabled = _isVisible;
          }
       }
    }
}
