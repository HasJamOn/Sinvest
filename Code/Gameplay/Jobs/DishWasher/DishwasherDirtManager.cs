using Sandbox;
using System;
using System.Collections.Generic;

namespace Sinvest;

public sealed class DishwasherDirtManager : Component
{
	[Property] public DecalDefinition DirtDecal { get; set; }
	[Property] public int DirtCount { get; set; } = 40;
	[Property] public float DecalSize { get; set; } = 0.5f;

	// Each dirt piece takes exactly this many scrubs to fully remove
	// (alpha starts at 1.0, each scrub removes 0.25, removed at <= 0.05)
	public const int ScrubsPerDirt = 4;

	// Read by the HUD
	public int TotalScrubsNeeded { get; private set; }
	public int TotalScrubsApplied { get; private set; }
	public float CleanPercentage => TotalScrubsNeeded <= 0
		? 0f
		: ((float)TotalScrubsApplied / TotalScrubsNeeded * 100f).Clamp( 0f, 100f );

	private sealed class DirtData
	{
		public GameObject Projector;
		public Vector3 LocalSurfacePoint;
		public float CurrentAlpha;
	}

	private readonly List<DirtData> _activeDirt = new();

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

			if ( !tr.Hit || !tr.Shape.IsValid() || !tr.Shape.Body.IsValid() ) continue;
			if ( !tr.Shape.Body.GameObject.IsDescendant( GameObject ) ) continue;

			Vector3 rayDir = (end - start).Normal;
			if ( Vector3.Dot( tr.Normal, rayDir ) >= 0f ) continue;

			CreateDirtEntry( tr.HitPosition, tr.Normal, spawned );
			spawned++;
		}

		// Total scrubs = actual spawned count * scrubs each needs
		TotalScrubsNeeded = _activeDirt.Count * ScrubsPerDirt;
		TotalScrubsApplied = 0;

		Log.Info( $"[DirtManager] Spawned {_activeDirt.Count} dirt pieces. Total scrubs needed: {TotalScrubsNeeded}" );
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
			LocalSurfacePoint = GameObject.WorldTransform.PointToLocal( position ),
			CurrentAlpha = 1.0f
		} );
	}

	public void TryCleanAt( Vector3 worldHitPosition, float scrubRadius, HashSet<Guid> session )
	{
		for ( int i = _activeDirt.Count - 1; i >= 0; i-- )
		{
			var dirt = _activeDirt[i];

			if ( !dirt.Projector.IsValid() )
			{
				_activeDirt.RemoveAt( i );
				continue;
			}

			Vector3 worldSurface = GameObject.WorldTransform.PointToWorld( dirt.LocalSurfacePoint );
			float dist = Vector3.DistanceBetween( worldSurface, worldHitPosition );

			if ( dist > scrubRadius )
			{
				session.Remove( dirt.Projector.Id );
				continue;
			}

			// One scrub application per drag-enter per dirt piece
			if ( session.Contains( dirt.Projector.Id ) ) continue;
			session.Add( dirt.Projector.Id );

			if ( !dirt.Projector.Components.TryGet<Decal>( out var decal ) ) continue;

			dirt.CurrentAlpha -= 0.25f;
			dirt.CurrentAlpha = dirt.CurrentAlpha.Clamp( 0f, 1f );
			decal.ColorTint = Color.White.WithAlpha( dirt.CurrentAlpha );

			// Count every scrub hit regardless of removal
			TotalScrubsApplied = Math.Min( TotalScrubsApplied + 1, TotalScrubsNeeded );

			if ( dirt.CurrentAlpha <= 0.05f )
			{
				dirt.Projector.Destroy();
				_activeDirt.RemoveAt( i );
			}
		}
	}
}
