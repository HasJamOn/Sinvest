using Sandbox;
using System;

namespace Sinvest;

/// <summary>
/// Handles real-time dirty-to-clean texture masking for mesh surfaces.
/// This component projects world-space hits onto a 2D local byte array 
/// and updates a GPU texture attribute named 'DirtMask'.
/// </summary>
public class DishwasherDynamicMask : Component
{
	[Property] public float BrushRadius { get; set; } = 0.05f;
	[Property] public float RotationSpeed { get; set; } = 0.5f;

	/// <summary>
	/// The boundary used for UV projection. Calculated automatically from the model 
	/// but can be adjusted manually in the editor via Gizmos.
	/// </summary>
	[Property, ReadOnly] public float CalculatedPlateRadius { get; private set; } = 50f;

	private Texture _maskTexture;
	private byte[] _maskData;
	private int _textureSize = 256;
	private bool _isDirty = false;
	private int _cleanedPixelCount = 0;

	// Debug tracking
	private Vector3 _lastWorldHit;
	private Vector2 _lastUV;
	private bool _hasHitThisFrame;

	[Property, ReadOnly] public float CleanPercentage { get; private set; } = 0f;

	private bool IsStatic => GameObject.Tags.Has( "static" );

	protected override void OnStart()
	{
		InitializeRadius();
		InitializeMask();

		if ( IsStatic )
		{
			Log.Warning( $"[Sinvest] {GameObject.Name} has the 'static' tag. It will not rotate!" );
		}
	}

	private void InitializeRadius()
	{
		if ( Components.TryGet<ModelRenderer>( out var renderer ) && renderer.Model is not null )
		{
			var bounds = renderer.Model.Bounds;
			CalculatedPlateRadius = MathF.Max( bounds.Size.x, MathF.Max( bounds.Size.y, bounds.Size.z ) ) / 2f;
		}
	}

	protected override void OnPreRender()
	{
		if ( !Components.TryGet<ModelRenderer>( out var renderer ) ) return;
		if ( !Gizmo.IsSelected && !_hasHitThisFrame ) return;

		// We use the GameObject's transform scope so everything inside follows the plate
		using ( Gizmo.Scope( "plate_debug", GameObject.WorldTransform ) )
		{
			// 1. Draw the actual model geometry as a wireframe
			Gizmo.Draw.Color = Color.Cyan.WithAlpha( 0.3f );
			// This ensures the gizmo perfectly matches the dish shape
			Gizmo.Draw.Model( renderer.Model );

			if ( _hasHitThisFrame )
			{
				var localHit = GameObject.WorldTransform.PointToLocal( _lastWorldHit );

				// 2. Draw ACTUAL World Hit (Red)
				Gizmo.Draw.Color = Color.Red;
				Gizmo.Draw.SolidSphere( localHit, 1f );

				// 3. Draw CALCULATED Mask Hit (Green)
				float diameter = CalculatedPlateRadius * 2f;
				Vector3 mathLocalPos = new Vector3( 
					(_lastUV.x - 0.5f) * diameter, 
					((1.0f - _lastUV.y) - 0.5f) * diameter, 
					localHit.z // Match hit depth for visual alignment
				);

				Gizmo.Draw.Color = Color.Green;
				Gizmo.Draw.SolidSphere( mathLocalPos, 1f );
				
				// Connector line - if this is short, your math is accurate!
				Gizmo.Draw.Color = Color.Yellow;
				Gizmo.Draw.Line( localHit, mathLocalPos );

				_hasHitThisFrame = false;
			}
		}
	}

	protected override void OnUpdate()
	{
		if ( !Input.Down( "attack2" ) ) return;

		var plateRoot = GameObject.Parent;
		if ( !plateRoot.IsValid() ) return;

		var delta = Input.MouseDelta;
		var camRot = Scene.Camera.WorldRotation;
		
		plateRoot.WorldRotation = Rotation.FromAxis( camRot.Up, -delta.x * RotationSpeed ) * plateRoot.WorldRotation;
		plateRoot.WorldRotation = Rotation.FromAxis( camRot.Right, delta.y * RotationSpeed ) * plateRoot.WorldRotation;
	}

	private void InitializeMask()
	{
		_maskData = new byte[_textureSize * _textureSize];
		for ( int i = 0; i < _maskData.Length; i++ ) _maskData[i] = 255;
		
		_maskTexture = Texture.Create( _textureSize, _textureSize )
			.WithFormat( ImageFormat.I8 )
			.WithData( _maskData )
			.WithDynamicUsage() 
			.Finish();

		if ( Components.TryGet<ModelRenderer>( out var renderer ) )
		{
			renderer.Attributes.Set( "DirtMask", _maskTexture );
		}
	}

	public void CleanAtWorldLocation( SceneTraceResult tr )
	{
		if ( !Components.TryGet<ModelRenderer>( out var renderer ) ) return;
    
		// Store for debug gizmos
		_lastWorldHit = tr.HitPosition;
		_hasHitThisFrame = true;

		// Convert hit to local space
		Vector3 localPos = GameObject.WorldTransform.PointToLocal( tr.HitPosition );
		Vector3 localNormal = GameObject.WorldTransform.NormalToLocal( tr.Normal );
    
		// CRITICAL: Offset by the model's geometric center so the UVs align with the shape, 
		// not the pivot (which is usually at the bottom).
		localPos -= renderer.Model.Bounds.Center;

		float u, v;
		float absX = MathF.Abs( localNormal.x );
		float absY = MathF.Abs( localNormal.y );
		float absZ = MathF.Abs( localNormal.z );
		float diameter = CalculatedPlateRadius * 2f;

		// Tri-planar Projection Logic
		if ( absZ >= absX && absZ >= absY ) 
		{ 
			u = (localPos.x / diameter) + 0.5f; 
			v = (localPos.y / diameter) + 0.5f; 
		}
		else if ( absX >= absY && absX >= absZ ) 
		{ 
			u = (localPos.z / diameter) + 0.5f; 
			v = (localPos.y / diameter) + 0.5f; 
		}
		else 
		{ 
			u = (localPos.x / diameter) + 0.5f; 
			v = (localPos.z / diameter) + 0.5f; 
		}

		_lastUV = new Vector2( Math.Clamp( u, 0, 1 ), Math.Clamp( 1.0f - v, 0, 1 ) );
		UpdateMask( _lastUV );
	}

	private void UpdateMask( Vector2 uv )
	{
		int centerX = (int)(uv.x * _textureSize);
		int centerY = (int)(uv.y * _textureSize);
		int radiusIdx = (int)(BrushRadius * _textureSize);

		for ( int x = -radiusIdx; x <= radiusIdx; x++ )
		{
			for ( int y = -radiusIdx; y <= radiusIdx; y++ )
			{
				if ( x * x + y * y <= radiusIdx * radiusIdx )
				{
					int px = centerX + x;
					int py = centerY + y;
					
					if ( px >= 0 && px < _textureSize && py >= 0 && py < _textureSize )
					{
						int index = py * _textureSize + px;
						if ( _maskData[index] != 0 )
						{
							_maskData[index] = 0; 
							_cleanedPixelCount++;
							_isDirty = true; 
						}
					}
				}
			}
		}

		if ( _isDirty )
		{
			_maskTexture.Update( _maskData );
			CleanPercentage = (float)_cleanedPixelCount / (_textureSize * _textureSize) * 100f;
			_isDirty = false;
		}
	}
}
