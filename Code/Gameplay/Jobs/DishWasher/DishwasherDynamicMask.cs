using Sandbox;
using System;

namespace Sinvest;

public class DishwasherDynamicMask : Component
{
    [Property] public float BrushRadius { get; set; } = 0.05f;
    [Property] public float PlateRadius { get; set; } = 50f;

    private Texture _maskTexture;
    private byte[] _maskData;
    private int _textureSize = 256;
    private bool _isDirty = false;
    private int _cleanedPixelCount = 0;

    [Property, ReadOnly] public float CleanPercentage { get; private set; } = 0f;

    protected override void OnStart()
    {
        InitializeMask();
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
            renderer.Attributes.Set( "DirtMask", _maskTexture );
    }

    /// <summary>
    /// Called by the Camera component when a hit is detected.
    /// </summary>
    public void CleanAtWorldLocation( SceneTraceResult tr )
    {
        // Convert the hit into the rotating local space of this specific plate
        Vector3 localPos = Transform.World.PointToLocal( tr.HitPosition );
        Vector3 localNormal = Transform.World.NormalToLocal( tr.Normal );
    
        // Planar Projection
        float u, v;
        float absX = Math.Abs( localNormal.x ), absY = Math.Abs( localNormal.y ), absZ = Math.Abs( localNormal.z );

        if ( absZ >= absX && absZ >= absY ) { u = (localPos.x / (PlateRadius * 2f)) + 0.5f; v = (localPos.y / (PlateRadius * 2f)) + 0.5f; }
        else if ( absX >= absY && absX >= absZ ) { u = (localPos.z / (PlateRadius * 2f)) + 0.5f; v = (localPos.y / (PlateRadius * 2f)) + 0.5f; }
        else { u = (localPos.x / (PlateRadius * 2f)) + 0.5f; v = (localPos.z / (PlateRadius * 2f)) + 0.5f; }

        UpdateMask( new Vector2( Math.Clamp(u, 0, 1), Math.Clamp(1.0f - v, 0, 1) ) );
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
                    int px = centerX + x, py = centerY + y;
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
