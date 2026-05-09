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
    
	    // Calculate center and radius in pixel coordinates
	    float centerX = TextureSize / 2f;
	    float centerY = TextureSize / 2f;
	    float radius = TextureSize / 2f; // Full circle radius

	    for ( int y = 0; y < TextureSize; y++ )
	    {
		    for ( int x = 0; x < TextureSize; x++ )
		    {
			    int idx = y * TextureSize + x;
            
			    // Calculate distance from center
			    float dx = x - centerX;
			    float dy = y - centerY;
			    float distSq = dx * dx + dy * dy;

			    // If inside the circle, it's dirty (255), otherwise clean (0)
			    if ( distSq <= radius * radius )
			    {
				    _maskData[idx] = 255;
			    }
			    else
			    {
				    _maskData[idx] = 0;
			    }
		    }
	    }

	    _maskTexture = Texture.Create( TextureSize, TextureSize )
		    .WithFormat( ImageFormat.I8 )
		    .WithDynamicUsage()
		    .Finish();

	    _maskTexture.Update( _maskData );

	    if ( Components.TryGet<ModelRenderer>( out _renderer ) )
	    {
		    // Break the batching so each plate is unique
		    _renderer.MaterialOverride = _renderer.MaterialOverride.CreateCopy();
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
