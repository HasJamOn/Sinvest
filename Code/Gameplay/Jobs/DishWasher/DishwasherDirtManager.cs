using Sandbox;
using System;
using System.Collections.Generic;

namespace Sinvest;

public sealed class DishwasherDirtManager : Component
{
	[Property] public DecalDefinition DirtDecal { get; set; }
	[Property] public int DirtCount { get; set; } = 40;
	[Property] public float DecalSize { get; set; } = 0.5f;

	[Property, ReadOnly] public float CleanPercentage { get; private set; } = 0f;

	private sealed class DirtData
	{
		public GameObject Projector;
		public Vector3 LocalSurfacePoint; // local to the DirtManager's GameObject
		public float InitialAlpha;
		public float CurrentAlpha;
	}

	private readonly List<DirtData> _activeDirt = new();
	private float _totalInitialAlpha;

	protected override void OnStart()
	{
		SpawnDirtRobust();
	}

	private void SpawnDirtRobust()
	{
		var colliders = Components.GetAll<Collider>( FindMode.EverythingInSelfAndChildren ).ToList();
		if ( !colliders.Any() ) return;

		var bounds = GameObject.GetBounds();
		float radius = bounds.Size.Length;
		int spawned = 0;
		int attempts = 0;

		while ( spawned < DirtCount && attempts < DirtCount * 15 )
		{
			attempts++;
			Vector3 randomDir = Vector3.Random.Normal;
			Vector3 start = bounds.Center + randomDir * radius;
			Vector3 end = bounds.Center - randomDir * radius;

			var tr = Scene.PhysicsWorld.Trace
				.Ray( start, end )
				.WithTag( "solid" )
				.Run();

			if ( tr.Hit && tr.Shape.IsValid() && tr.Shape.Body.IsValid() && tr.Shape.Body.GameObject.IsDescendant( GameObject ) )
			{
				Vector3 rayDir = (end - start).Normal;
				if ( Vector3.Dot( tr.Normal, rayDir ) >= 0f ) continue;

				CreateDirtEntry( tr.HitPosition, tr.Normal, spawned );
				spawned++;
			}
		}

		_totalInitialAlpha = _activeDirt.Sum( x => x.InitialAlpha );
		UpdateCleanPercentage();
	}

	private void CreateDirtEntry( Vector3 position, Vector3 normal, int index )
	{
		var go = Scene.CreateObject();
		go.Name = $"Dirt_{index}";
		go.Parent = GameObject;

		const float surfaceBias = 0.02f;
		go.WorldPosition = position + (normal * surfaceBias);

		Vector3 upDir = MathF.Abs( normal.Dot( Vector3.Up ) ) > 0.98f ? Vector3.Forward : Vector3.Up;
		go.WorldRotation = Rotation.LookAt( -normal, upDir );
		go.WorldRotation *= Rotation.FromRoll( Game.Random.Float( 0, 360 ) );

		var decal = go.Components.Create<Decal>();
		if ( DirtDecal is not null )
			decal.Decals = new List<DecalDefinition> { DirtDecal };

		float variance = Game.Random.Float( 0.8f, 1.2f );
		decal.Size = new Vector2( DecalSize * variance, DecalSize * variance );
		decal.Depth = 5f;
		decal.ColorTint = Color.White;
		decal.SortLayer = (uint)(index % 256);

		_activeDirt.Add( new DirtData
		{
			Projector = go,
			// Store in local space so the point stays correct after rotation
			LocalSurfacePoint = GameObject.WorldTransform.PointToLocal( position ),
			InitialAlpha = 1.0f,
			CurrentAlpha = 1.0f
		} );
	}

	public void TryCleanAt( Vector3 worldHitPosition, float scrubRadius, HashSet<Guid> session )
	{
		bool anyChanged = false;

		for ( int i = _activeDirt.Count - 1; i >= 0; i-- )
		{
			var dirt = _activeDirt[i];
			if ( !dirt.Projector.IsValid() )
			{
				_activeDirt.RemoveAt( i );
				continue;
			}

			// Convert back to world space each frame so rotation is accounted for
			Vector3 worldSurface = GameObject.WorldTransform.PointToWorld( dirt.LocalSurfacePoint );

			float dist = Vector3.DistanceBetween( worldSurface, worldHitPosition );
			if ( dist > scrubRadius )
			{
				session.Remove( dirt.Projector.Id );
				continue;
			}

			if ( session.Contains( dirt.Projector.Id ) ) continue;

			if ( dirt.Projector.Components.TryGet<Decal>( out var decal ) )
			{
				session.Add( dirt.Projector.Id );

				dirt.CurrentAlpha -= 0.25f;
				dirt.CurrentAlpha = dirt.CurrentAlpha.Clamp( 0f, 1f );
				decal.ColorTint = Color.White.WithAlpha( dirt.CurrentAlpha );

				if ( dirt.CurrentAlpha <= 0.05f )
				{
					dirt.Projector.Destroy();
					_activeDirt.RemoveAt( i );
				}
				anyChanged = true;
			}
		}

		if ( anyChanged ) UpdateCleanPercentage();
	}

	private void UpdateCleanPercentage()
	{
		if ( _totalInitialAlpha <= 0 )
		{
			CleanPercentage = 100f;
			return;
		}

		float currentAlphaSum = _activeDirt.Sum( x => x.CurrentAlpha );
		float progress = (_totalInitialAlpha - currentAlphaSum) / _totalInitialAlpha;
		CleanPercentage = (progress * 100f).Clamp( 0f, 100f );
	}
}
