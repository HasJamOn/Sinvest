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
    private ModelRenderer _renderer;

    protected override void OnStart()
    {
	    _maskData = new byte[TextureSize * TextureSize];
	    Array.Fill( _maskData, (byte)255 );

	    _maskTexture = Texture.Create( TextureSize, TextureSize )
		    .WithFormat( ImageFormat.I8 )
		    .WithDynamicUsage()
		    .Finish();

	    _maskTexture.Update( _maskData );

	    if ( Components.TryGet<ModelRenderer>( out _renderer ) )
	    {
		    // 1. Create a unique material clone for THIS specific plate.
		    // This stops s&box from batching plates together and 
		    // ensures 'DirtMask' stays private to this instance.
		    _renderer.MaterialOverride = _renderer.MaterialOverride.CreateCopy();

		    // 2. Set the attribute on the unique material copy.
		    _renderer.Attributes.Set( "DirtMask", _maskTexture );
	    }
    }

    public void CleanAtUV( Vector2 uv, float radius )
    {
        // 1:1 Mapping check. 
        // localPos.x/y math in the Scrubber might need to be flipped 
        // if your plane is rotated.
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

                _maskData[idx] = 0; // 0 = clean (Black)
                _cleanedPixelCount++;
                isDirty = true;
            }
        }

        if ( isDirty )
        {
            _maskTexture.Update( _maskData );
            
            // Re-apply the attribute (sometimes required if the texture instance is swapped)
            _renderer?.Attributes.Set( "DirtMask", _maskTexture );
            
            CleanPercentage = (float)_cleanedPixelCount / (TextureSize * TextureSize) * 100f;
        }
    }
}
