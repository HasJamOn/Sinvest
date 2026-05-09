using Sandbox;
using System.Collections.Generic;

namespace Sinvest;

public sealed class PlateSpawner : Component
{
	// Allows you to choose the prefab in the inspector
	[Property] public GameObject PlatePrefab { get; set; }
	[Property] public int SpawnCount { get; set; } = 5;
	
	/// <summary>
	/// The area (in local units) where plates can spawn.
	/// X = Width, Y = Height.
	/// </summary>
	[Property] public Vector2 SpawnArea { get; set; } = new Vector2( 500f, 500f );

	protected override void OnStart()
	{
		SpawnPlates();
	}

	public void SpawnPlates()
	{
		if ( !PlatePrefab.IsValid() )
		{
			Log.Warning( "PlateSpawner: No prefab assigned!" );
			return;
		}

		for ( int i = 0; i < SpawnCount; i++ )
		{
			// Generate a random position within the defined bounds
			float randomX = Game.Random.Float( -SpawnArea.x / 2, SpawnArea.x / 2 );
			float randomY = Game.Random.Float( -SpawnArea.y / 2, SpawnArea.y / 2 );

			// Calculate final position with a tiny Z-offset to prevent flickering
			Vector3 spawnPos = WorldPosition + new Vector3( randomX, randomY, i * 0.1f );

			// MATCHING CANDIDATE: Clone( Vector3 position, Rotation rotation, Vector3 scale )
			// This ensures the prefab retains its 1:1:1 scale (or whatever scale you want)
			var plate = PlatePrefab.Clone( 
				spawnPos, 
				WorldRotation, 
				Vector3.One 
			);

			plate.Name = $"Plate_{i}";
		}
	}

	protected override void DrawGizmos()
	{
		// Draws a box in the editor to visualize the spawn boundaries
		Gizmo.Draw.LineBBox( new BBox( 
			new Vector3( -SpawnArea.x / 2, -SpawnArea.y / 2, -1f ), 
			new Vector3( SpawnArea.x / 2, SpawnArea.y / 2, 1f ) 
		) );
	}
}
