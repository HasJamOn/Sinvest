using Sandbox;
using System;

namespace Sinvest;

public sealed class Dishwasher2DDynamicMask : Component
{
	[Property] public int TextureSize { get; set; } = 512;
	[Property, ReadOnly] public float CleanPercentage { get; private set; } = 0f;

	private Texture _maskTexture;
	private byte[] _maskData;
	private int _cleanedPixelCount;

	protected override void OnStart()
	{
		_maskData = new byte[TextureSize * TextureSize];
		Array.Fill( _maskData, (byte)255 ); // 255 = fully dirty

		_maskTexture = Texture.Create( TextureSize, TextureSize )
			.WithFormat( ImageFormat.I8 )
			.WithData( _maskData )
			.WithDynamicUsage()
			.Finish();

		// Apply the texture to the ModelRenderer as an attribute for the shader
		if ( Components.TryGet<ModelRenderer>( out var renderer ) )
		{
			renderer.Attributes.Set( "DirtMask", _maskTexture );
		}
	}

	public void CleanAtUV( Vector2 uv, float radius )
	{
		// Convert 0-1 UV to pixel space
		int cx = (int)(uv.x * TextureSize);
		int cy = (int)(uv.y * TextureSize);
		int r = (int)(radius * TextureSize);

		bool isDirty = false;

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

				_maskData[idx] = 0; // 0 = clean
				_cleanedPixelCount++;
				isDirty = true;
			}
		}

		if ( isDirty )
		{
			_maskTexture.Update( _maskData );
			CleanPercentage = (float)_cleanedPixelCount / (TextureSize * TextureSize) * 100f;
		}
	}
}
