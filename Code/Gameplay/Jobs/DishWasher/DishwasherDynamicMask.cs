using Sandbox;
using System;

namespace Sinvest;

/// <summary>
/// Handles real-time dirty-to-clean texture masking for mesh surfaces.
/// Projects world-space hits into a 2D local byte array and updates
/// a GPU texture attribute named 'DirtMask'.
/// </summary>
public class DishwasherDynamicMask : Component
{
    [Property] public float BrushRadius    { get; set; } = 0.05f;
    [Property] public float RotationSpeed  { get; set; } = 0.5f;

    /// <summary>
    /// Derived from the model bounds automatically; tweak manually if needed.
    /// </summary>
    [Property, ReadOnly] public float CalculatedPlateRadius { get; private set; } = 50f;

    [Property, ReadOnly] public float CleanPercentage { get; private set; } = 0f;

    private Texture  _maskTexture;
    private byte[]   _maskData;
    private const int TextureSize = 256;
    private bool _isDirty;
    private int  _cleanedPixelCount;

    // Debug state
    private Vector3 _lastWorldHit;
    private Vector2 _lastUV;
    private bool    _hasHitThisFrame;

    private bool IsStatic => GameObject.Tags.Has( "static" );

    // ── lifecycle ──────────────────────────────────────────────────────────

    protected override void OnStart()
    {
        InitializeRadius();
        InitializeMask();

        if ( IsStatic )
            Log.Warning( $"[Sinvest] {GameObject.Name} has the 'static' tag — it will not rotate!" );
    }

    /// <summary>Restores the mask to fully dirty (all white).</summary>
    public override void Reset() // Added 'override'
    {
	    base.Reset(); // Optional: Calls the base Component.Reset() to reset properties
    
	    Array.Fill( _maskData, (byte)255 );
	    _cleanedPixelCount = 0;
	    CleanPercentage    = 0f;
	    _maskTexture?.Update( _maskData );
    }

    // ── gizmo ──────────────────────────────────────────────────────────────

    protected override void OnPreRender()
    {
        if ( !Components.TryGet<ModelRenderer>( out var renderer ) ) return;
        if ( !Gizmo.IsSelected && !_hasHitThisFrame ) return;

        using ( Gizmo.Scope( "plate_debug", GameObject.WorldTransform ) )
        {
            Gizmo.Draw.Color = Color.Cyan.WithAlpha( 0.3f );
            Gizmo.Draw.Model( renderer.Model );

            if ( _hasHitThisFrame )
            {
                var localHit = GameObject.WorldTransform.PointToLocal( _lastWorldHit );
                float diameter = CalculatedPlateRadius * 2f;

                // Red = actual physics hit
                Gizmo.Draw.Color = Color.Red;
                Gizmo.Draw.SolidSphere( localHit, 1f );

                // Green = UV back-projection (should overlap red when math is correct)
                Vector3 mathPos = new Vector3(
                    (_lastUV.x - 0.5f)         * diameter,
                    ((1f - _lastUV.y) - 0.5f)  * diameter,
                    localHit.z
                );
                Gizmo.Draw.Color = Color.Green;
                Gizmo.Draw.SolidSphere( mathPos, 1f );

                // Yellow connector — shorter = more accurate
                Gizmo.Draw.Color = Color.Yellow;
                Gizmo.Draw.Line( localHit, mathPos );

                _hasHitThisFrame = false;
            }
        }
    }

    // ── input: plate rotation (right-click drag) ───────────────────────────

    protected override void OnUpdate()
    {
        if ( !Input.Down( "attack2" ) ) return;

        var plateRoot = GameObject.Parent;
        if ( !plateRoot.IsValid() ) return;

        var delta  = Input.MouseDelta;
        var camRot = Scene.Camera.WorldRotation;

        plateRoot.WorldRotation =
            Rotation.FromAxis( camRot.Up,    -delta.x * RotationSpeed ) *
            Rotation.FromAxis( camRot.Right,  delta.y * RotationSpeed ) *
            plateRoot.WorldRotation;
    }

    // ── cleaning API (called by DishwasherRaycast) ─────────────────────────

    public void CleanAtWorldLocation( SceneTraceResult tr )
    {
        if ( !Components.TryGet<ModelRenderer>( out _ ) ) return;

        // Project hit into the plate's local coordinate space.
        // UV is derived from geometry since TexCoord0 is unavailable on SceneTraceResult.
        var localHit = GameObject.WorldTransform.PointToLocal( tr.HitPosition );
        float diameter = CalculatedPlateRadius * 2f;

        float u =  (localHit.x / diameter) + 0.5f;
        float v = (-localHit.y / diameter) + 0.5f;

        _lastWorldHit    = tr.HitPosition;
        _hasHitThisFrame = true;
        _lastUV          = new Vector2( u, v );

        UpdateMask( _lastUV );
    }

    // ── internals ──────────────────────────────────────────────────────────

    private void InitializeRadius()
    {
        if ( Components.TryGet<ModelRenderer>( out var renderer ) && renderer.Model is not null )
        {
            var bounds = renderer.Model.Bounds;
            CalculatedPlateRadius =
                MathF.Max( bounds.Size.x, MathF.Max( bounds.Size.y, bounds.Size.z ) ) / 2f;
        }
    }

    private void InitializeMask()
    {
        _maskData = new byte[TextureSize * TextureSize];
        Array.Fill( _maskData, (byte)255 );

        _maskTexture = Texture.Create( TextureSize, TextureSize )
            .WithFormat( ImageFormat.I8 )
            .WithData( _maskData )
            .WithDynamicUsage()
            .Finish();

        if ( Components.TryGet<ModelRenderer>( out var renderer ) )
            renderer.Attributes.Set( "DirtMask", _maskTexture );
    }

    private void UpdateMask( Vector2 uv )
    {
        int cx = (int)(uv.x * TextureSize);
        int cy = (int)(uv.y * TextureSize);
        int r  = (int)(BrushRadius * TextureSize);

        for ( int x = -r; x <= r; x++ )
        {
            for ( int y = -r; y <= r; y++ )
            {
                if ( x * x + y * y > r * r ) continue;

                int px = cx + x;
                int py = cy + y;

                if ( px < 0 || px >= TextureSize || py < 0 || py >= TextureSize ) continue;

                int idx = py * TextureSize + px;
                if ( _maskData[idx] != 0 )
                {
                    _maskData[idx] = 0;
                    _cleanedPixelCount++;
                    _isDirty = true;
                }
            }
        }

        if ( _isDirty )
        {
            _maskTexture.Update( _maskData );
            CleanPercentage = (float)_cleanedPixelCount / (TextureSize * TextureSize) * 100f;
            _isDirty = false;
        }
    }
}
