using Sandbox;
using System;

namespace Sinvest;

public class DishwasherDynamicMask : Component
{
	[Property] public float RotationSpeed { get; set; } = 0.5f;
	[Property, ReadOnly] public float CalculatedPlateRadius { get; private set; } = 50f;
	[Property, ReadOnly] public float CleanPercentage { get; private set; } = 0f;

	private Texture _maskTexture;
	private byte[] _maskData;
	private int _cleanedPixelCount;
	private const int TextureSize = 256;

	protected override void OnStart()
	{
		InitializeRadius();
		InitializeMask();
	}

	/// <summary>
	/// Uses the HitUv from the raycast to precisely target pixels.
	/// </summary>
	public void CleanAtUV( Vector2 uv, float worldRadius )
	{
		// 1. Convert 0-1 UV to Pixel Space
		// Note: We flip Y if your texture appears upside down (common in some UV layouts)
		int cx = (int)(uv.x * TextureSize);
		int cy = (int)(uv.y * TextureSize);

		// 2. Scale world radius to UV space
		// We use the object's bounds to estimate how many pixels 'worldRadius' covers
		float uvRadius = worldRadius / (CalculatedPlateRadius * 2f);
		int r = (int)(uvRadius * TextureSize);

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

				_maskData[idx] = 0; // 0 is 'cleaned' (black in mask = clean in shader logic)
				_cleanedPixelCount++;
				dirty = true;
			}
		}

		if ( dirty )
		{
			_maskTexture.Update( _maskData );
			CleanPercentage = (float)_cleanedPixelCount / (TextureSize * TextureSize) * 100f;
		}
	}

	private void InitializeRadius()
	{
		if ( Components.TryGet<ModelRenderer>( out var renderer ) && renderer.Model is not null )
		{
			CalculatedPlateRadius = renderer.Model.Bounds.Size.Length / 2f;
		}
	}

	private void InitializeMask()
	{
		_maskData = new byte[TextureSize * TextureSize];
		Array.Fill( _maskData, (byte)255 ); // Start fully dirty (White)

		_maskTexture = Texture.Create( TextureSize, TextureSize )
			.WithFormat( ImageFormat.I8 )
			.WithData( _maskData )
			.WithDynamicUsage()
			.Finish();

		if ( Components.TryGet<ModelRenderer>( out var renderer ) )
		{
			// Make sure your Shader Graph has a Texture2D property named "DirtMask"
			renderer.Attributes.Set( "DirtMask", _maskTexture );
		}
	}

	// Simple rotation logic for the player to inspect the dish
	protected override void OnUpdate()
	{
		if ( !Input.Down( "attack2" ) ) return;

		var delta = Input.MouseDelta;
		GameObject.Parent.WorldRotation *= Rotation.FromAxis( Scene.Camera.WorldRotation.Up, -delta.x * RotationSpeed );
		GameObject.Parent.WorldRotation *= Rotation.FromAxis( Scene.Camera.WorldRotation.Right, delta.y * RotationSpeed );
	}
}
