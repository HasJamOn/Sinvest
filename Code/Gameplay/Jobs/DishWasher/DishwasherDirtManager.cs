using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sinvest;

public sealed class DishwasherDirtManager : Component
{
    [Property] public DecalDefinition DirtDecal { get; set; }
    [Property] public int DirtCount { get; set; } = 40;
    [Property] public bool DualSidedMode { get; set; } = true;
    [Property] public float DecalSize { get; set; } = 0.5f;

    /// <summary>
    /// How far above the surface the decal origin sits before projecting in.
    /// Increase slightly if decals are z-fighting with the surface.
    /// </summary>
    [Property] public float SurfaceOffset { get; set; } = 0.05f;

    [Property, ReadOnly] public float CleanPercentage { get; private set; } = 0f;

    private readonly List<GameObject> _dirtDecals = new();
    private int _totalSpawned;
    private float _safeDecalDepth;

    protected override void OnStart()
    {
        _safeDecalDepth = CalculateSafeDecalDepth();
        SpawnDirt();
    }

    /// <summary>
    /// Uses the model's minimum bounding dimension as a conservative thickness estimate.
    /// The decal only needs to travel SurfaceOffset + a partial slice into the mesh.
    /// </summary>
    private float CalculateSafeDecalDepth()
    {
        if ( Components.TryGet<ModelRenderer>( out var renderer ) && renderer.Model is not null )
        {
            var size = renderer.Model.Bounds.Size;
            float minDimension = MathF.Min( size.x, MathF.Min( size.y, size.z ) );
            // SurfaceOffset closes the gap from origin to surface.
            // 0.35 of the thinnest dimension penetrates far enough to project
            // without punching through to the opposite face.
            return SurfaceOffset + minDimension * 0.35f;
        }

        Log.Warning( "DishwasherDirtManager: No ModelRenderer found, using fallback depth." );
        return SurfaceOffset + 0.5f;
    }

    private void SpawnDirt()
    {
        _dirtDecals.Clear();

        int spawned = 0;
        int attempts = 0;
        int maxAttempts = DirtCount * 10; // allow retries for complex shapes

        while ( spawned < DirtCount && attempts < maxAttempts )
        {
            bool isBack = DualSidedMode && (spawned % 2 != 0);

            if ( TrySpawnDecalOnSurface( isBack, spawned ) )
                spawned++;

            attempts++;
        }

        if ( spawned < DirtCount )
        {
            Log.Warning( $"DishwasherDirtManager: Only placed {spawned}/{DirtCount} decals after {maxAttempts} attempts. Check SpawnRadius or model collider." );
        }

        _totalSpawned = _dirtDecals.Count;
        UpdateCleanPercentage();
    }

    /// <summary>
    /// Fires a ray from outside the model's bounds toward its centre.
    /// On hit, places a decal at the surface point oriented along the hit normal.
    /// Returns false if the ray misses — caller retries.
    /// </summary>
    private bool TrySpawnDecalOnSurface( bool isBack, int index )
    {
        if ( !Components.TryGet<ModelRenderer>( out var renderer ) || renderer.Model is null )
            return false;

        var worldBounds = renderer.Model.Bounds.Translate( GameObject.WorldPosition );
        float outerDist = worldBounds.Size.Length;
        var center = worldBounds.Center;

        // Pick a random direction on the unit sphere, then optionally bias
        // toward front or back using the model's local up axis for dual-sided mode.
        Vector3 up = GameObject.WorldTransform.Up;
        Vector3 randomDir = GetRandomHemisphereDirection( isBack ? -up : up );

        Vector3 rayOrigin = center + randomDir * outerDist;
        Vector3 rayTarget = center - randomDir * outerDist;

        var tr = Scene.Trace
            .Ray( rayOrigin, rayTarget )
            .UsePhysicsWorld()
            .Run();

        // Only accept hits that belong to this object
        if ( !tr.Hit || tr.GameObject != GameObject )
            return false;

        var go = new GameObject( true, "Dirt" );
        go.Parent = GameObject;

        // Place slightly above the surface along the hit normal so the
        // decal projects cleanly inward rather than starting inside geometry.
        go.WorldPosition = tr.HitPosition + tr.Normal * SurfaceOffset;

        // Orient so the decal's forward axis (-normal direction) projects into the surface.
        // Choose a stable up reference that isn't parallel to the normal.
        Vector3 safeUp = MathF.Abs( Vector3.Dot( tr.Normal, Vector3.Up ) ) > 0.9f
            ? Vector3.Forward
            : Vector3.Up;

        go.WorldRotation = Rotation.LookAt( -tr.Normal, safeUp );

        var decal = go.Components.Create<Decal>();

        if ( DirtDecal is not null )
        {
            decal.Decals = new List<DecalDefinition> { DirtDecal };
        }

        decal.Size = new Vector2( DecalSize, DecalSize );
        decal.Depth = _safeDecalDepth;
        decal.Rotation = Game.Random.Float( 0f, 360f );
        decal.SortLayer = (uint)index;

        _dirtDecals.Add( go );
        return true;
    }

    /// <summary>
    /// Returns a random direction biased toward the given hemisphere.
    /// Uses cosine-weighted sampling so directions near the pole are more likely,
    /// keeping decals on the faces most visible to the player.
    /// </summary>
    private static Vector3 GetRandomHemisphereDirection( Vector3 normal )
    {
        // Random point on full sphere
        float theta = Game.Random.Float( 0f, MathF.PI * 2f );
        float phi = MathF.Acos( Game.Random.Float( -1f, 1f ) );

        var dir = new Vector3(
            MathF.Sin( phi ) * MathF.Cos( theta ),
            MathF.Sin( phi ) * MathF.Sin( theta ),
            MathF.Cos( phi )
        );

        // Flip to hemisphere of the given normal
        if ( Vector3.Dot( dir, normal ) < 0 )
            dir = -dir;

        return dir.Normal;
    }

    public void TryCleanAt( Vector3 worldHitPosition, Vector3 surfaceNormal, float scrubRadius, HashSet<Guid> session )
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

		    // Skip if we hit this decal in the current "hover" session
		    if ( session.Contains( go.Id ) ) continue;

		    float dist = Vector3.DistanceBetween( go.WorldPosition, worldHitPosition );
		    if ( dist > scrubRadius + DecalSize * 0.5f ) continue;

		    float dot = Vector3.Dot( go.WorldTransform.Forward, surfaceNormal );
		    if ( dot > 0.1f ) continue;

		    if ( go.Components.TryGet<Decal>( out var decal ) )
		    {
			    session.Add( go.Id );
			    var currentColor = decal.ColorTint.Evaluate( 0f, 0f ); 
    
			    if ( currentColor.a <= 0.15f )
			    {
				    // Destroying the object removes it from the world
				    go.Destroy();
				    _dirtDecals.RemoveAt( i );
			    }
			    else
			    {
				    currentColor.a -= 0.1f;
				    decal.ColorTint = currentColor;
			    }
    
			    anyCleaned = true;
		    }
	    }

	    if ( anyCleaned ) UpdateCleanPercentage();
    }

    private void UpdateCleanPercentage()
    {
	    // Safety check: If we didn't spawn anything, we are technically 100% clean
	    if ( _totalSpawned <= 0 ) 
	    {
		    CleanPercentage = 100f;
		    return;
	    }

	    float currentAlphaSum = 0;

	    // Loop through the list and sum remaining alpha
	    foreach ( var go in _dirtDecals )
	    {
		    // Only count valid, non-destroyed objects
		    if ( go.IsValid() && go.Components.TryGet<Decal>( out var decal ) )
		    {
			    currentAlphaSum += decal.ColorTint.Evaluate( 0f, 0f ).a;
		    }
	    }

	    // Progress = (Start - Remaining) / Start
	    // Example: (40 - 39.9) / 40 = 0.0025 -> * 100 = 0.25%
	    float progress = ( (float)_totalSpawned - currentAlphaSum ) / (float)_totalSpawned;
    
	    CleanPercentage = Math.Clamp( progress * 100f, 0f, 100f );
    
	    // Debug Log: Uncomment this to see the values in the console (F1)
	    // Log.Info( $"Alpha: {currentAlphaSum} / {_totalSpawned} | Progress: {CleanPercentage}%" );
    }
}
