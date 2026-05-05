using Sandbox;
using System;
using System.Collections.Generic;

namespace Sinvest;

/// <summary>
/// Handles the visual representation of an IdleMon.
/// Translates logical IdleMonData into 3D models and shader parameters.
/// </summary>
public sealed class IdleMonVisuals : Component
{
    [Property] public ModelRenderer ModelVisual { get; set; }

    /// <summary>
    /// Explicit list of models to use for this visual tier.
    /// Priority 1: Hand-picked in the Inspector.
    /// </summary>
    [Property] public List<Model> ModelPool { get; set; }

    /// <summary>
    /// FileSystem path to scan for models (e.g., "models/idlemon/").
    /// Priority 2: Automatically populated if ModelPool is empty.
    /// </summary>
    [Property] public string ModelFolder { get; set; }

    private List<Model> _cachedModels;
    
    // Shared memory to prevent expensive FileSystem scans across multiple instances
    private static List<Model> _globalFolderCache;
    private static string _lastScannedFolder;

    protected override void OnStart()
    {
       LoadModelsFromFolder();
    }

    /// <summary>
    /// Scans the mounted FileSystem for .vmdl files.
    /// Implements a static cache to ensure 50+ nodes don't trigger 50+ disk scans.
    /// </summary>
    private void LoadModelsFromFolder()
    {
       if ( string.IsNullOrEmpty( ModelFolder ) ) return;
   
       if ( _globalFolderCache != null && _lastScannedFolder == ModelFolder )
       {
          _cachedModels = _globalFolderCache;
          return;
       }

       _cachedModels = new List<Model>();
       var files = FileSystem.Mounted.FindFile( ModelFolder, "*.vmdl", true );

       foreach ( var file in files )
       {
          var fullPath = System.IO.Path.Combine( ModelFolder, file ).Replace('\\', '/');
          var model = Model.Load( fullPath );
          if ( model != null )
          {
             _cachedModels.Add( model );
          }
       }

       _globalFolderCache = _cachedModels;
       _lastScannedFolder = ModelFolder;
    }

    private List<Model> GetActivePool()
    {
       return (ModelPool != null && ModelPool.Count > 0) ? ModelPool : _cachedModels;
    }

    /// <summary>
    /// DETERMINISTIC MODEL SELECTION
    /// Seeds an RNG with the unique Guid. This ensures that a specific 'Asset' 
    /// always looks the same across different sessions and players without 
    /// needing to save the ModelPath in the ledger.
    /// </summary>
    private Model PickModel( Guid id )
    {
       var pool = GetActivePool();
       if ( pool == null || pool.Count == 0 ) return null;

       int hash = id.GetHashCode();
       var rng = new Random( hash );

       return pool[rng.Next( pool.Count )];
    }

    /// <summary>
    /// Updates the 3D entity and its Material Attributes based on current stats.
    /// This allows the shader to visually react to 'Multiplier' or 'Generation' values.
    /// </summary>
    public void UpdateFromData( IdleMonData data, int steamId = 4000 )
    {
       if ( !ModelVisual.IsValid() ) return;

       // 1. Resolve 3D Mesh
       var pooledModel = PickModel( data.ID );
       if ( pooledModel != null )
       {
          ModelVisual.Model = pooledModel;
       }
       else if ( !string.IsNullOrEmpty( data.ModelPath ) )
       {
          ModelVisual.Model = Model.Load( data.ModelPath );
       }

       // 2. Attribute Synchronization
       // Accessing the SceneObject allows us to push data directly into the GPU buffers.
       var so = ModelVisual.SceneObject;
       if ( so == null ) return;

       var attr = so.Attributes;

       // Push ASMD stats for visual feedback (e.g. glowing based on Multiplier)
       attr.Set( "Addition", (float)data.Addition );
       attr.Set( "Multiplier", (float)data.Multiplier );
       attr.Set( "Subtraction", (float)data.Subtraction );
       attr.Set( "Division", (float)data.Division );
       attr.Set( "Luck", (float)data.Luck );
       attr.Set( "TeamBonus", (float)data.TeamBonus );
       attr.Set( "Efficiency", (float)data.CostEfficiency );
       attr.Set( "Generation", (float)data.Generation );

       // 3. Child Logic Sync (e.g., UI labels or particle effects attached to the case)
       var caseLogic = Components.Get<IdlemonCase>( FindMode.EverythingInSelfAndDescendants );
       if ( caseLogic.IsValid() )
       {
          caseLogic.UpdateAllStats( steamId, data );
       }
    }

    protected override void OnUpdate()
    {
       // Continuous update for time-based shader effects (e.g., scanlines, scrolling textures)
       if ( ModelVisual.IsValid() && ModelVisual.SceneObject != null )
       {
          ModelVisual.SceneObject.Attributes.Set( "ShaderTime", RealTime.Now );
       }
    }
}
