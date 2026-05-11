using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sinvest;

public sealed class DishwasherDirtManager : Component
{
	[Property] public DecalDefinition DirtDecal { get; set; }
	[Property] public int DirtCount { get; set; } = 40;
	[Property] public float DecalSize { get; set; } = 0.5f;

	/// <summary>
	/// How much alpha to remove per scrub (e.g., 0.2 = 5 scrubs to reach 0 alpha).
	/// </summary>
	[Property, Range( 0.05f, 1.0f )] public float AlphaStep { get; set; } = 0.25f;

	/// <summary>
	/// How far the decal "slides" away from the scrub point per hit.
	/// </summary>
	[Property] public float ScrubPushStrength { get; set; } = 0.5f;

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
		public bool IsInFinalStage; 
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

		int scrubsPerPiece = (int)MathF.Ceiling( 0.9f / AlphaStep ) + 1;
		TotalScrubsNeeded = _activeDirt.Count * scrubsPerPiece;
		TotalScrubsApplied = 0;

		Log.Info( $"[DirtManager] Spawned {_activeDirt.Count} dirt. Step: {AlphaStep}. Needed: {TotalScrubsNeeded}" );
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
			CurrentAlpha = 1.0f,
			IsInFinalStage = false
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

			// Get current world position of the dirt piece
			Vector3 worldSurface = GameObject.WorldTransform.PointToWorld( dirt.LocalSurfacePoint );
			float dist = Vector3.DistanceBetween( worldSurface, worldHitPosition );

			if ( dist > scrubRadius )
			{
				session.Remove( dirt.Projector.Id );
				continue;
			}

			if ( session.Contains( dirt.Projector.Id ) ) continue;
			session.Add( dirt.Projector.Id );

			// Calculate push direction: From the cursor hit point toward the dirt
			Vector3 pushDirection = (worldSurface - worldHitPosition).Normal;
			
			// Move the dirt piece world position based on strength
			Vector3 newWorldPos = worldSurface + (pushDirection * ScrubPushStrength);
			
			// Update local point so it stays attached to the moving plate
			dirt.LocalSurfacePoint = GameObject.WorldTransform.PointToLocal( newWorldPos );
			dirt.Projector.WorldPosition = newWorldPos;

			if ( !dirt.Projector.Components.TryGet<Decal>( out var decal ) ) continue;

			if ( dirt.IsInFinalStage )
			{
				TotalScrubsApplied = Math.Min( TotalScrubsApplied + 1, TotalScrubsNeeded );
				dirt.Projector.Destroy();
				_activeDirt.RemoveAt( i );
				continue;
			}

			dirt.CurrentAlpha -= AlphaStep;

			if ( dirt.CurrentAlpha <= 0.10f )
			{
				dirt.CurrentAlpha = 0.10f;
				dirt.IsInFinalStage = true;
			}

			decal.ColorTint = Color.White.WithAlpha( dirt.CurrentAlpha );
			TotalScrubsApplied = Math.Min( TotalScrubsApplied + 1, TotalScrubsNeeded );
		}
	}
}
