using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sinvest;

public sealed class DishwasherDirtManager : Component
{
	/// <summary>
	/// The DecalDefinition asset to use for dirt. 
	/// Defaulting to monitor_screen.decal per project requirements.
	/// </summary>
	[Property] public DecalDefinition DirtDecal { get; set; }

	[Property] public int DirtCount { get; set; } = 40;

	/// <summary>
	/// New Default: 13.02f
	/// </summary>
	[Property] public float SpawnRadius { get; set; } = 13.02f;

	/// <summary>
	/// New Default: 0.1f
	/// </summary>
	[Property] public float SpawnHeight { get; set; } = 0.1f;

	/// <summary>
	/// New Default: 0.5f (Rounded from 0.499...)
	/// </summary>
	[Property] public float DecalSize { get; set; } = 0.5f;

	/// <summary>
	/// New Default: 5.0f
	/// </summary>
	[Property] public float DecalDepth { get; set; } = 5.0f;

	[Property, ReadOnly] public float CleanPercentage { get; private set; } = 0f;

	private readonly List<GameObject> _dirtDecals = new();
	private int _totalSpawned;

	public void ApplyChanges()
	{
		foreach ( var go in GameObject.Children )
		{
			if ( !go.IsValid || !go.Name.Contains( "Dirt" ) ) continue;

			go.LocalPosition = go.LocalPosition.WithZ( SpawnHeight );

			var decal = go.Components.Get<Decal>();
			if ( decal is not null )
			{
				decal.Size = new Vector2( DecalSize, DecalSize );
				decal.Depth = DecalDepth;
			}
		}
	}

	protected override void OnStart()
	{
		SpawnDirt();
	}

	private void SpawnDirt()
	{
		_dirtDecals.Clear();

		for ( int i = 0; i < DirtCount; i++ )
		{
			SpawnSingleDirtDecal();
		}

		_totalSpawned = _dirtDecals.Count;
		UpdateCleanPercentage();
	}

	private void SpawnSingleDirtDecal()
	{
		float angle = Game.Random.Float( 0f, MathF.PI * 2f );
		float radius = MathF.Sqrt( Game.Random.Float() ) * SpawnRadius;

		var localOffset = new Vector3(
			MathF.Cos( angle ) * radius,
			MathF.Sin( angle ) * radius,
			SpawnHeight 
		);

		var go = new GameObject( true, "Dirt" );
		go.Parent = GameObject;
		go.LocalPosition = localOffset;
		go.LocalRotation = Rotation.FromPitch( 90f );

		var decal = go.Components.Create<Decal>();

		if ( DirtDecal is not null )
		{
			decal.Decals = new List<DecalDefinition> { DirtDecal };
		}

		decal.Size = new Vector2( DecalSize, DecalSize );
		decal.Depth = DecalDepth;
		decal.Rotation = Game.Random.Float( 0f, 360f );

		_dirtDecals.Add( go );
	}

	public void TryCleanAt( Vector3 worldHitPosition, float scrubRadius )
	{
		bool anyCleaned = false;

		for ( int i = _dirtDecals.Count - 1; i >= 0; i-- )
		{
			var go = _dirtDecals[i];
			if ( !go.IsValid() )
			{
				_dirtDecals.RemoveAt( i );
				continue;
			}

			float dist = Vector3.DistanceBetween( go.WorldPosition, worldHitPosition );

			if ( dist <= scrubRadius + DecalSize * 0.5f )
			{
				go.Destroy();
				_dirtDecals.RemoveAt( i );
				anyCleaned = true;
			}
		}

		if ( anyCleaned ) UpdateCleanPercentage();
	}

	private void UpdateCleanPercentage()
	{
		if ( _totalSpawned == 0 ) return;
		int remaining = _dirtDecals.Count( x => x.IsValid() );
		CleanPercentage = (1f - (float)remaining / _totalSpawned) * 100f;
	}
}
