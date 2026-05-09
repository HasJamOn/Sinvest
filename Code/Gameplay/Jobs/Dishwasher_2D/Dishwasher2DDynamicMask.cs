using Sandbox;
using Sandbox.Utility;
using System;
using Sandbox.Audio;

namespace Sinvest;

public sealed class Dishwasher2DDynamicMask : Component
{
    [Property, Group( "Configuration" )] public int TextureSize { get; set; } = 512;
    
    [Header( "Splat Settings" )]
    [Property, Range( 1, 20 )] public int MinSplats { get; set; } = 3;
    [Property, Range( 1, 20 )] public int MaxSplats { get; set; } = 8;
    [Property, Range( 5, 100 )] public float MinSplatSize { get; set; } = 15f;
    [Property, Range( 5, 200 )] public float MaxSplatSize { get; set; } = 60f;

    [Header( "Warping (Organic Shapes)" )]
    [Property, Range( 0.01f, 0.5f )] public float DistortionFrequency { get; set; } = 0.15f;
    [Property, Range( 0f, 1f )] public float DistortionAmplitude { get; set; } = 0.8f;

    [Header( "Audio" )]
    [Property] public SoundEvent FinishSound { get; set; }

    [Property, ReadOnly] public float CleanPercentage { get; private set; } = 0f;

    /// <summary>
    /// Reference to the spawner that created this plate. 
    /// Used to signal when the plate should be removed from the scene.
    /// </summary>
    public PlateSpawner Spawner { get; set; }

    private Texture _maskTexture;
    private byte[] _maskData;
    private int _initialDirtyPixels;
    private int _cleanedPixelCount;
    private ModelRenderer _renderer;

    protected override void OnStart()
    {
        // Initialize the buffer for an I8 (8-bit grayscale) texture
        _maskData = new byte[TextureSize * TextureSize];
        Array.Fill( _maskData, (byte)0 ); 

        GenerateOrganicSplats();

        // Create the dynamic texture
        _maskTexture = Texture.Create( TextureSize, TextureSize )
            .WithFormat( ImageFormat.I8 )
            .WithDynamicUsage()
            .Finish();

        _maskTexture.Update( _maskData );

        // Apply to the renderer's Material attributes
        if ( Components.TryGet<ModelRenderer>( out _renderer ) )
        {
            // Create a copy so we don't affect other plates using the same material
            _renderer.MaterialOverride = _renderer.MaterialOverride.CreateCopy();
            _renderer.Attributes.Set( "DirtMask", _maskTexture );
        }
    }

    protected override void OnUpdate()
    {
        // Only check for despawn when the user lets go of the mouse
        if ( Input.Released( "attack1" ) )
        {
            CheckForCompletion();
        }
    }

    private void CheckForCompletion()
    {
        // Completion threshold (e.g., 92% clean)
        if ( CleanPercentage >= 92f )
        {
	        if ( FinishSound is not null )
	        {
		        // Play the sound - don't provide a position to keep it 2D
		        var handle = Sound.Play( FinishSound );
		        if ( handle.IsValid() )
		        {
			        // Force UI/Clear settings
			        handle.ListenLocal = true;
			        handle.DistanceAttenuation = false;
			        handle.Occlusion = false;
                
			        // Use Mixer.Find to get the UI bus
			        // If "UI" mixer doesn't exist, it will safely fallback to Master
			        handle.TargetMixer = Mixer.FindMixerByName( "UI" );
		        }
	        }

            // Notify the spawner to handle destruction/scoring
            Spawner?.OnPlateCleaned( GameObject );
        }
    }

    /// <summary>
    /// Manipulates the byte array at the specified UV coordinate.
    /// Called by the Scrubber component.
    /// </summary>
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
                
                // If the pixel is already clean (0), skip it
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
            
            // Calculate progress based on the initial dirty pixel count
            CleanPercentage = ((float)_cleanedPixelCount / _initialDirtyPixels) * 100f;
        }
    }

    private void GenerateOrganicSplats()
    {
	    float plateRadius = TextureSize / 2f;
	    Vector2 center = new Vector2( plateRadius, plateRadius );

	    // --- NEW: Global Noise Pass ---
	    // This adds a light coating of "grime" across the entire circular area
	    for ( int y = 0; y < TextureSize; y++ )
	    {
		    for ( int x = 0; x < TextureSize; x++ )
		    {
			    float dx = x - center.x;
			    float dy = y - center.y;
			    float distSq = dx * dx + dy * dy;

			    // Only apply noise within the plate's circular bounds
			    if ( distSq < (plateRadius * plateRadius) )
			    {
				    // Adjust frequency (0.1f) for "tightness" and threshold (0.6f) for "thickness"
				    float globalNoise = Noise.Perlin( x * 0.1f, y * 0.1f );
                
				    if ( globalNoise > 0.6f ) 
				    {
					    _maskData[y * TextureSize + x] = 255;
				    }
			    }
		    }
	    }

	    // --- EXISTING: Chunky Splats ---
	    int splatCount = Game.Random.Int( MinSplats, MaxSplats );
	    for ( int i = 0; i < splatCount; i++ )
	    {
		    float dist = MathF.Sqrt( Game.Random.Float( 0, 1 ) ) * (plateRadius * 0.8f);
		    float angle = Game.Random.Float( 0, MathF.PI * 2 );
        
		    int centerX = (int)(center.x + MathF.Cos( angle ) * dist);
		    int centerY = (int)(center.y + MathF.Sin( angle ) * dist);
		    float splatSize = Game.Random.Float( MinSplatSize, MaxSplatSize );

		    DrawWarpedSplat( centerX, centerY, splatSize );
	    }

	    // Establish the baseline for "100% dirty"
	    _initialDirtyPixels = 0;
	    foreach ( var b in _maskData ) 
	    {
		    if ( b == 255 ) _initialDirtyPixels++;
	    }
    
	    if (_initialDirtyPixels == 0) _initialDirtyPixels = 1;
    }

    private void DrawWarpedSplat( int cx, int cy, float radius )
    {
        // Add padding to the iteration bounds to account for noise-based expansion
        int r = (int)radius + 15; 
        for ( int y = -r; y <= r; y++ )
        {
            for ( int x = -r; x <= r; x++ )
            {
                int px = cx + x;
                int py = cy + y;
                
                if ( px < 0 || px >= TextureSize || py < 0 || py >= TextureSize ) continue;

                // Use Perlin noise to warp the edges of the circle for a "stain" look
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
