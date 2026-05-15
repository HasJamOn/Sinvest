using Sandbox;
using System.Collections.Generic;

namespace Sinvest;

/// <summary>
/// Spawns a pile of dishes at runtime. Each dish gets its own Plate_Root clone
/// positioned at the workspace anchor, hidden until that dish is selected.
/// </summary>
public sealed class DishPile : Component
{
    [Property] public GameObject DishPrefab { get; set; }
    [Property] public GameObject PlateRootPrefab { get; set; }

    /// <summary>
    /// Empty scene marker at the position the camera is aimed at.
    /// All Plate_Root clones are placed here — only one is visible at a time.
    /// </summary>
    [Property] public GameObject WorkspaceAnchor { get; set; }

    [Property] public int DishCount { get; set; } = 5;
    [Property] public float SpreadRadius { get; set; } = 15f;
    [Property] public float StackSpacing { get; set; } = 1.5f;

    /// <summary>
    /// Uniform scale applied to the 3D Plate_Root model in the workspace.
    /// </summary>
    [Property, Range(0.1f, 5.0f)] public float PlateScale { get; set; } = 1.0f;

    protected override void OnStart()
    {
       if ( !DishPrefab.IsValid() )      { Log.Warning( "DishPile: DishPrefab not assigned." ); return; }
       if ( !PlateRootPrefab.IsValid() ) { Log.Warning( "DishPile: PlateRootPrefab not assigned." ); return; }
       if ( !WorkspaceAnchor.IsValid() ) { Log.Warning( "DishPile: WorkspaceAnchor not assigned." ); return; }

       SpawnPile();
    }

    private void SpawnPile()
    {
       for ( int i = 0; i < DishCount; i++ )
       {
          // --- Spawn the pile sprite ---
          var dish = DishPrefab.Clone();
          dish.Parent = GameObject;
          dish.Name = $"Dish_{i}";

          float angle = (i / (float)DishCount) * 360f;
          float radius = SpreadRadius * Game.Random.Float( 0.6f, 1.0f );
          var offset = Rotation.FromYaw( angle ).Forward * radius;
          offset = offset.WithZ( i * StackSpacing );

          dish.LocalPosition = offset;
          dish.LocalRotation = Rotation.FromYaw( Game.Random.Float( 0f, 360f ) );

          // --- Spawn this dish's own Plate_Root at the workspace position ---
          var plateRoot = PlateRootPrefab.Clone();
          plateRoot.Parent = GameObject;
          plateRoot.Name = $"PlateRoot_{i}";
          
          // Apply the scale set in the Inspector
          plateRoot.LocalScale = new Vector3( PlateScale ); 

          plateRoot.WorldPosition = WorkspaceAnchor.WorldPosition;
          plateRoot.WorldRotation = WorkspaceAnchor.WorldRotation;
          plateRoot.Enabled = false; // hidden until this dish is selected

          // --- Wire them together ---
          var transition = dish.Components.Get<DishTransition>();
          if ( transition.IsValid() )
          {
             transition.ModelVisual = plateRoot;
          }
          else
          {
             Log.Warning( $"DishPile: '{dish.Name}' has no DishTransition." );
          }
       }

       Log.Info( $"DishPile: Spawned {DishCount} dishes with PlateScale {PlateScale}." );
    }
}
