using Sandbox;
using System;
using System.Collections.Generic;

namespace Sinvest;

public sealed class DishwasherScrubber : Component
{
	[Property] public float ReachDistance { get; set; } = 500f;
	[Property] public Sphere ScrubberShape { get; set; } = new Sphere( Vector3.Zero, 10f );

	private SceneTraceResult _positionTrace;

	protected override void OnUpdate()
	{
		var ray = Scene.Camera.ScreenPixelToRay( Mouse.Position );

		// UseRenderMeshes is the flag required to get Triangle data from the trace
		_positionTrace = Scene.Trace
			.Ray( ray, ReachDistance )
			.UsePhysicsWorld()
			.UseRenderMeshes()
			.Run();

		if ( _positionTrace.Hit && Input.Down( "attack1" ) )
		{
			if ( _positionTrace.GameObject.Components.TryGet<DishwasherDynamicMask>( out var mask ) )
			{
				var uv = GetUvFromTrace( _positionTrace );
				mask.CleanAtUV( uv, ScrubberShape.Radius );
			}
		}
	}

	private Vector2 GetUvFromTrace( SceneTraceResult tr )
	{
		// Fetch the renderer to access the model data found in the investigation
		var renderer = tr.GameObject.Components.Get<ModelRenderer>();
		if ( renderer == null || renderer.Model == null ) return default;

		// 1. Fetch raw geometry buffers confirmed in your Model member dump
		var indices = renderer.Model.GetIndices();
		var vertices = renderer.Model.GetVertices();

		// 2. Identify the three vertices of the hit triangle using explicit casts for uint to int
		int i0 = (int)indices[tr.Triangle * 3 + 0];
		int i1 = (int)indices[tr.Triangle * 3 + 1];
		int i2 = (int)indices[tr.Triangle * 3 + 2];

		var v0 = vertices[i0];
		var v1 = vertices[i1];
		var v2 = vertices[i2];

		// 3. Calculate Barycentric Coordinates to interpolate the UV
		// Project the world hit position into the object's local space to match vertex positions
		Vector3 p = tr.GameObject.WorldTransform.PointToLocal( tr.HitPosition );
		Vector3 f1 = v1.Position - v0.Position;
		Vector3 f2 = v2.Position - v0.Position;
		Vector3 f3 = p - v0.Position;

		float d00 = Vector3.Dot( f1, f1 );
		float d01 = Vector3.Dot( f1, f2 );
		float d11 = Vector3.Dot( f2, f2 );
		float d20 = Vector3.Dot( f3, f1 );
		float d21 = Vector3.Dot( f3, f2 );
		float denom = d00 * d11 - d01 * d01;

		// Using Math.Abs to avoid the MathF context error
		if ( Math.Abs( denom ) < 0.000001f ) return v0.TexCoord0;

		float v = (d11 * d20 - d01 * d21) / denom;
		float w = (d00 * d21 - d01 * d20) / denom;
		float u = 1.0f - v - w;

		// 4. Return the weighted average of the UV coordinates (TexCoord0)
		return v0.TexCoord0 * u + v1.TexCoord0 * v + v2.TexCoord0 * w;
	}
}
