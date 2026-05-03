using Sandbox;
using System;
using System.Collections.Generic;

namespace Sinvest;

public sealed class IdleMonVisuals : Component
{
	[Property] public ModelRenderer ModelVisual { get; set; }

	// OPTION A: Manually assign models in inspector
	[Property] public List<Model> ModelPool { get; set; }

	// OPTION B: Auto-load models from folder (e.g. "models/idlemon/")
	[Property] public string ModelFolder { get; set; }

	private List<Model> _cachedModels;

	protected override void OnStart()
	{
		LoadModelsFromFolder();
	}
	
	private static List<Model> _globalFolderCache;
	private static string _lastScannedFolder;

	private void LoadModelsFromFolder()
	{
		if ( string.IsNullOrEmpty( ModelFolder ) ) return;
   
		// Only scan if the folder changed or we haven't scanned yet
		if ( _globalFolderCache != null && _lastScannedFolder == ModelFolder )
		{
			_cachedModels = _globalFolderCache;
			return;
		}

		_cachedModels = new List<Model>();

		var files = FileSystem.Mounted.FindFile( ModelFolder, "*.vmdl", true );

		foreach ( var file in files )
		{
			// Ensure we combine the folder path with the filename
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
		if ( ModelPool != null && ModelPool.Count > 0 )
			return ModelPool;

		return _cachedModels;
	}

	/// <summary>
	/// Deterministic selection based on IdleMon ID
	/// Ensures all clients pick the same model without networking
	/// </summary>
	private Model PickModel( Guid id )
	{
		var pool = GetActivePool();

		if ( pool == null || pool.Count == 0 )
			return null;

		int hash = id.GetHashCode();
		var rng = new Random( hash );

		return pool[rng.Next( pool.Count )];
	}

	public void UpdateFromData( IdleMonData data, int steamId = 4000 )
	{
		if ( !ModelVisual.IsValid() ) return;

		// 1. Resolve Model: Pool takes priority to enforce the new system
		var pooledModel = PickModel( data.ID );
    
		if ( pooledModel != null )
		{
			ModelVisual.Model = pooledModel;
		}
		else if ( !string.IsNullOrEmpty( data.ModelPath ) )
		{
			// Fallback for legacy assets or specific manual overrides
			ModelVisual.Model = Model.Load( data.ModelPath );
		}

		// 2. Attribute Synchronization
		// Note: We access ModelVisual.Direct attributes or the SceneObject if ready
		var so = ModelVisual.SceneObject;
		if ( so == null ) return;

		// Use a local reference to Attributes for cleaner syntax
		var attr = so.Attributes;

		// Push gameplay stats to the shader
		attr.Set( "Addition", (float)data.Addition );
		attr.Set( "Multiplier", (float)data.Multiplier );
		attr.Set( "Subtraction", (float)data.Subtraction );
		attr.Set( "Division", (float)data.Division );
		attr.Set( "Luck", (float)data.Luck );
		attr.Set( "TeamBonus", (float)data.TeamBonus );
		attr.Set( "Efficiency", (float)data.CostEfficiency );
		attr.Set( "Generation", (float)data.Generation );

		// 3. Child Component Sync
		var caseLogic = Components.Get<IdlemonCase>( FindMode.EverythingInSelfAndDescendants );
		if ( caseLogic.IsValid() )
		{
			caseLogic.UpdateAllStats( steamId, data );
		}
	}

	protected override void OnUpdate()
	{
		if ( ModelVisual.IsValid() && ModelVisual.SceneObject != null )
		{
			ModelVisual.SceneObject.Attributes.Set( "ShaderTime", RealTime.Now );
		}
	}
}
