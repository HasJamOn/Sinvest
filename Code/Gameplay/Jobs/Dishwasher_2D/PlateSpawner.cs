using Sandbox;
using System.Collections.Generic;
using System.Linq;

namespace Sinvest;

public sealed class PlateSpawner : Component
{
	[Property] public GameObject PlatePrefab { get; set; }
	[Property] public int SpawnCount { get; set; } = 5;
	[Property] public Vector2 SpawnArea { get; set; } = new Vector2( 500f, 500f );

	/// <summary>
	/// Persistent counter to ensure every single spawned plate 
	/// has a unique Z-depth throughout the session.
	/// </summary>
	private int _spawnIteration = 0;

	protected override void OnStart()
	{
		for ( int i = 0; i < SpawnCount; i++ )
		{
			SpawnSinglePlate();
		}
	}

	public void SpawnSinglePlate()
	{
		if ( !PlatePrefab.IsValid() ) return;

		_spawnIteration++;

		float randomX = Game.Random.Float( -SpawnArea.x / 2, SpawnArea.x / 2 );
		float randomY = Game.Random.Float( -SpawnArea.y / 2, SpawnArea.y / 2 );

		// Clone parented to this spawner, then set LOCAL position.
		// Local offsets survive any future repositioning of the minigame root.
		var plate = PlatePrefab.Clone( new Transform( WorldPosition ), GameObject );
		plate.LocalPosition = new Vector3( randomX, randomY, _spawnIteration * 1.0f );
		plate.LocalRotation = Rotation.Identity;
		plate.Name = $"Plate_{_spawnIteration}";

		if ( plate.Components.TryGet<Dishwasher2DDynamicMask>( out var mask ) )
			mask.Spawner = this;
	}

	public void OnPlateCleaned( GameObject plate )
	{
		// Destroy the old one
		plate.Destroy();

		// Spawn a new one. It will automatically get the next _spawnIteration Z-depth.
		SpawnSinglePlate();
		
		Log.Info( $"Plate Cleaned. Current Depth Layer: {_spawnIteration}" );
	}

	protected override void DrawGizmos()
	{
		Gizmo.Draw.LineBBox( new BBox( 
			new Vector3( -SpawnArea.x / 2, -SpawnArea.y / 2, -1f ), 
			new Vector3( SpawnArea.x / 2, SpawnArea.y / 2, 1f ) 
		) );
	}
}
