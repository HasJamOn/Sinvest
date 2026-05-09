using Sandbox;
using Sandbox.Utility;
using System;

namespace Sinvest;

public sealed class Dishwasher2DDynamicMask : Component
{
	[Property] public int TextureSize { get; set; } = 512;
	
	[Header( "Splat Settings" )]
	[Property, Range( 1, 20 )] public int MinSplats { get; set; } = 3;
	[Property, Range( 1, 20 )] public int MaxSplats { get; set; } = 8;
	[Property, Range( 5, 100 )] public float MinSplatSize { get; set; } = 15f;
	[Property, Range( 5, 200 )] public float MaxSplatSize { get; set; } = 60f;

	[Header( "Warping (Organic Shapes)" )]
	[Property, Range( 0.01f, 0.5f )] public float DistortionFrequency { get; set; } = 0.15f;
	[Property, Range( 0f, 1f )] public float DistortionAmplitude { get; set; } = 0.8f;

	[Property, ReadOnly] public float CleanPercentage { get; private set; } = 0f;

	// Assigned by PlateSpawner when spawned
	public PlateSpawner Spawner { get; set; }

	private Texture _maskTexture;
	private byte[] _maskData;
	private int _initialDirtyPixels;
	private int _cleanedPixelCount;
	private ModelRenderer _renderer;

	protected override void OnStart()
	{
		_maskData = new byte[TextureSize * TextureSize];
		Array.Fill( _maskData, (byte)0 ); 

		GenerateOrganicSplats();

		_maskTexture = Texture.Create( TextureSize, TextureSize )
			.WithFormat( ImageFormat.I8 )
			.WithDynamicUsage()
			.Finish();

		_maskTexture.Update( _maskData );

		if ( Components.TryGet<ModelRenderer>( out _renderer ) )
		{
			_renderer.MaterialOverride = _renderer.MaterialOverride.CreateCopy();
			_renderer.Attributes.Set( "DirtMask", _maskTexture );
		}
	}

	protected override void OnUpdate()
	{
		// NEW: Check for despawn only when the player RELEASES the scrub button
		if ( Input.Released( "attack1" ) )
		{
			CheckForCompletion();
		}
	}

	private void CheckForCompletion()
	{
		// If we've hit the threshold, tell the spawner we are done
		if ( CleanPercentage >= 92f )
		{
			Spawner?.OnPlateCleaned( GameObject );
		}
	}

	public void CleanAtUV( Vector2 uv, float brushRadius )
	{
		int cx = (int)(uv.x * TextureSize);
		int cy = (int)(uv.y * TextureSize);
		int r = (int)(brushRadius * TextureSize);

		bool dirty = false;

		for ( int dx = -r; dx <= r; dx++ )
		{
			for ( int dy = -r; dy <= r; dy++ )
			{
				if ( dx * dx + dy * dy > r * r ) continue;

				int px = cx + dx;
				int py = cy + dy;

				if ( px < 0 || px >= TextureSize || py < 0 || py >= TextureSize ) continue;

				int idx = py * TextureSize + px;
				if ( _maskData[idx] == 0 ) continue;

				_maskData[idx] = 0;
				_cleanedPixelCount++;
				dirty = true;
			}
		}

		if ( dirty )
		{
			_maskTexture.Update( _maskData );
			_renderer?.Attributes.Set( "DirtMask", _maskTexture );
			
			// Update percentage but do NOT despawn here anymore
			CleanPercentage = ((float)_cleanedPixelCount / _initialDirtyPixels) * 100f;
		}
	}

	private void GenerateOrganicSplats()
	{
		int splatCount = Game.Random.Int( MinSplats, MaxSplats );
		float plateRadius = TextureSize / 2f;
		Vector2 center = new Vector2( plateRadius, plateRadius );

		for ( int i = 0; i < splatCount; i++ )
		{
			float dist = MathF.Sqrt( Game.Random.Float( 0, 1 ) ) * (plateRadius * 0.8f);
			float angle = Game.Random.Float( 0, MathF.PI * 2 );
			
			int centerX = (int)(center.x + MathF.Cos( angle ) * dist);
			int centerY = (int)(center.y + MathF.Sin( angle ) * dist);
			float splatSize = Game.Random.Float( MinSplatSize, MaxSplatSize );

			DrawWarpedSplat( centerX, centerY, splatSize );
		}

		_initialDirtyPixels = 0;
		foreach ( var b in _maskData ) if ( b == 255 ) _initialDirtyPixels++;
	}

	private void DrawWarpedSplat( int cx, int cy, float radius )
	{
		int r = (int)radius + 15; 
		for ( int y = -r; y <= r; y++ )
		{
			for ( int x = -r; x <= r; x++ )
			{
				int px = cx + x;
				int py = cy + y;
				if ( px < 0 || px >= TextureSize || py < 0 || py >= TextureSize ) continue;

				float warpX = Noise.Perlin( (px + cx) * DistortionFrequency, py * DistortionFrequency );
				float warpY = Noise.Perlin( px * DistortionFrequency, (py + cy) * DistortionFrequency );
				float warpedX = x + (warpX - 0.5f) * radius * DistortionAmplitude;
				float warpedY = y + (warpY - 0.5f) * radius * DistortionAmplitude;
				float warpedDistSq = warpedX * warpedX + warpedY * warpedY;

				if ( warpedDistSq < radius * radius )
				{
					_maskData[py * TextureSize + px] = 255;
				}
			}
		}
	}
}
