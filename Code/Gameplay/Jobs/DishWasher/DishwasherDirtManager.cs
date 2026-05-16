using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sinvest;

public sealed class DishwasherDirtManager : Component
{
	[Property] public DecalDefinition DirtDecal { get; set; }
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

	public void SpawnDirtRobust()
	{
		var colliders = Components.GetAll<Collider>( FindMode.EverythingInSelfAndChildren ).ToList();
		if ( !colliders.Any() ) return;

		int targetCount = 40; // Fallback
		if ( DishwasherManager.Instance.IsValid() )
		{
			int min = DishwasherManager.Instance.MinDirtCount;
			int max = DishwasherManager.Instance.MaxDirtCount;
			float power = DishwasherManager.Instance.DirtSkewPower;
			int range = max - min;

			float rawRandom = Game.Random.Float( 0f, 1f );
			float skewedRandom = MathF.Pow( rawRandom, power );

			targetCount = min + (int)MathF.Floor( skewedRandom * (range + 1) );
			targetCount = targetCount.Clamp( min, max );
		}

		var bounds = GameObject.GetBounds();
		float radius = bounds.Size.Length;
		int spawned = 0;
		int attempts = 0;

		// FIX: Use a fixed safety cap of 500 attempts instead of scaling down to 15 when targetCount is 1
		int maxAttempts = Math.Max( 500, targetCount * 15 );

		while ( spawned < targetCount && attempts < maxAttempts )
		{
			attempts++;
			Vector3 randomDir = Vector3.Random.Normal;
			Vector3 start = bounds.Center + randomDir * radius;
			Vector3 end = bounds.Center - randomDir * radius;

			var tr = Scene.PhysicsWorld.Trace.Ray( start, end ).WithTag( "solid" ).Run();

			if ( !tr.Hit || !tr.Shape.IsValid() || !tr.Shape.Body.IsValid() ) continue;
			if ( !tr.Shape.Body.GameObject.IsDescendant( GameObject ) ) continue;

			Vector3 rayDir = (end - start).Normal;
			if ( Vector3.Dot( tr.Normal, rayDir ) >= 0f ) continue;

			var localPos = GameObject.WorldTransform.PointToLocal( tr.HitPosition );
			CreateDirtEntry( localPos, tr.Normal, 1.0f, spawned );
			spawned++;
		}

		RecalculateTotals( resetApplied: true );
	}

	private void RecalculateTotals( bool resetApplied )
	{
		int scrubsPerPiece = (int)MathF.Ceiling( 0.9f / AlphaStep ) + 1;
		TotalScrubsNeeded = _activeDirt.Count * scrubsPerPiece;
		if ( resetApplied ) TotalScrubsApplied = 0;
	}

	private void CreateDirtEntry( Vector3 localPosition, Vector3 normal, float alpha, int index )
	{
		var go = Scene.CreateObject();
		go.Name = $"Dirt_{index}";
		go.Parent = GameObject;

		const float surfaceBias = 0.02f;
		go.LocalPosition = localPosition + (normal * surfaceBias);

		Vector3 upDir = MathF.Abs( normal.Dot( Vector3.Up ) ) > 0.98f ? Vector3.Forward : Vector3.Up;
		go.LocalRotation = Rotation.LookAt( -normal, upDir );
		go.LocalRotation *= Rotation.FromRoll( Game.Random.Float( 0, 360 ) );

		var decal = go.Components.Create<Decal>();
		if ( DirtDecal is not null )
			decal.Decals = new List<DecalDefinition> { DirtDecal };

		// Let's copy dynamic size properties over from central system rules if possible
		float globalSize = DishwasherManager.Instance.IsValid() ? DishwasherManager.Instance.GlobalDecalSize : DecalSize;
		float variance = Game.Random.Float( 0.8f, 1.2f );
		decal.Size = new Vector2( globalSize * variance, globalSize * variance );
		
		decal.Depth = 5f;
		decal.ColorTint = Color.White.WithAlpha( alpha );
		decal.SortLayer = (uint)(index % 256);

		_activeDirt.Add( new DirtData
		{
			Projector = go,
			LocalSurfacePoint = localPosition,
			CurrentAlpha = alpha,
			IsInFinalStage = alpha <= 0.1f
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

			if ( session.Contains( dirt.Projector.Id ) ) continue;
			session.Add( dirt.Projector.Id );

			Vector3 pushDirection = (worldSurface - worldHitPosition).Normal;
			Vector3 newWorldPos = worldSurface + (pushDirection * ScrubPushStrength);
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

	public DishProgress SaveState()
	{
		var state = new DishProgress
		{
			IsInitialized = true,
			SavedScrubsApplied = TotalScrubsApplied,
			SavedScrubsNeeded = TotalScrubsNeeded
		};

		foreach ( var dirt in _activeDirt )
		{
			state.AlphaValues.Add( dirt.CurrentAlpha );
			state.LocalPoints.Add( dirt.LocalSurfacePoint );
		}

		foreach ( var dirt in _activeDirt ) dirt.Projector.Destroy();
		_activeDirt.Clear();

		return state;
	}

	public void LoadState( DishProgress state )
	{
		foreach ( var dirt in _activeDirt ) dirt.Projector.Destroy();
		_activeDirt.Clear();

		for ( int i = 0; i < state.AlphaValues.Count; i++ )
		{
			CreateDirtEntry( state.LocalPoints[i], Vector3.Up, state.AlphaValues[i], i );
		}

		TotalScrubsNeeded = state.SavedScrubsNeeded;
		TotalScrubsApplied = state.SavedScrubsApplied;
	}

	public void ResetAndSpawn( bool shouldRandomize = true )
	{
		foreach ( var dirt in _activeDirt ) dirt.Projector.Destroy();
		_activeDirt.Clear();

		if ( shouldRandomize ) SpawnDirtRobust();
	}
}
