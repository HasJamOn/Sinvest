using Sandbox;
using System;
using System.Linq;

namespace Sinvest;

/// <summary>
/// Manages a dynamic I8 texture mask to simulate cleaning dirt off a surface.
/// Coordinates between raycast hits and UV space to update a "DirtMask" shader attribute.
/// </summary>
public class DishwasherDynamicMask : Component
{
    [Property] public GameObject Plate { get; set; }
    [Property] public CameraComponent Camera { get; set; }
    [Property] public float BrushRadius { get; set; } = 0.05f;
    [Property] public float RotationSpeed { get; set; } = 0.5f;
    
    /// <summary>
    /// Half-extents of the target mesh. Used to normalize local coordinates into 0-1 UV space.
    /// </summary>
    [Property] public float PlateRadius { get; set; } = 50f; 
    [Property] public bool ShowDebugTrace { get; set; } = true;

    private Texture _maskTexture;
    private byte[] _maskData;
    private int _textureSize = 256;
    private bool _isDirty = false;

    [Property, ReadOnly] public float CleanPercentage { get; private set; } = 0f;
    private bool _isLevelComplete = false;

    protected override void OnStart()
    {
        if ( Camera == null ) Camera = Scene.GetAllComponents<CameraComponent>().FirstOrDefault();
        
        // Ensure mouse is active for the interaction loop
        Mouse.Visibility = MouseVisibility.Visible;
        InitializeMask();
    }

    /// <summary>
    /// Generates a procedural 8-bit single-channel texture. 
    /// Default state is 255 (fully opaque/dirty).
    /// </summary>
    private void InitializeMask()
    {
        _maskData = new byte[_textureSize * _textureSize];
        for ( int i = 0; i < _maskData.Length; i++ ) _maskData[i] = 255;

        _maskTexture = Texture.Create( _textureSize, _textureSize )
            .WithFormat( ImageFormat.I8 )
            .WithData( _maskData )
            .WithDynamicUsage()
            .Finish();

        // Bind the dynamic texture to the shader's 'DirtMask' attribute
        if ( Plate.Components.TryGet<ModelRenderer>( out var renderer ) )
        {
            renderer.Attributes.Set( "DirtMask", _maskTexture );
        }
    }

    protected override void OnUpdate()
    {
        if ( _isLevelComplete ) return;

        HandleRotation();
        HandleCleaning();

        // Deferred GPU Upload: Batch pixel changes to avoid multiple uploads per frame
        if ( _isDirty )
        {
            _maskTexture.Update( _maskData );
            CalculateProgress();
            _isDirty = false;
        }
    }

    /// <summary>
    /// Rotates the plate or its parent container based on relative mouse movement.
    /// Uses WorldRotation to maintain consistent controls regardless of parent orientation.
    /// </summary>
    private void HandleRotation()
    {
        if ( Input.Down( "attack2" ) )
        {
            float x = Mouse.Delta.x * RotationSpeed;
            float y = Mouse.Delta.y * RotationSpeed;

            if ( Plate.Parent.IsValid() )
                Plate.Parent.WorldRotation *= Rotation.From( y, -x, 0 );
            else
                Plate.WorldRotation *= Rotation.From( y, -x, 0 );
        }
    }

    /// <summary>
    /// Performs a screen-to-world raycast. If the hit object is validated as part of the dish,
    /// it triggers a mask update at the intersection point.
    /// </summary>
    private void HandleCleaning()
    {
        if ( !Input.Down( "attack1" ) ) return;

        var ray = Camera.ScreenPixelToRay( Mouse.Position );
    
        // 2000f distance ensures coverage in various camera FOVs/distances
        var tr = Scene.Trace.Ray( ray, 2000f ).Run(); 

        if ( tr.Hit )
        {
           if ( IsPartOfPlate( tr.GameObject ) )
           {
              Vector2 projectedUv = CalculateLocalUV( tr );
              UpdateMask( projectedUv );
           }
        }
    }

    /// <summary>
    /// Projects a 3D world hit position into 2D UV space.
    /// Determines the dominant face of the hit normal to apply the correct planar projection.
    /// </summary>
    private Vector2 CalculateLocalUV( SceneTraceResult tr )
    {
        Vector3 localPos = Plate.Transform.World.PointToLocal( tr.HitPosition );
        Vector3 localNormal = Plate.Transform.World.NormalToLocal( tr.Normal );
    
        float u = 0.5f;
        float v = 0.5f;

        float absX = Math.Abs( localNormal.x );
        float absY = Math.Abs( localNormal.y );
        float absZ = Math.Abs( localNormal.z );

        // Planar Projection Logic:
        // Maps the hit point based on the largest normal component to ensure the "brush"
        // follows the geometry surface accurately across different faces.
        if ( absZ >= absX && absZ >= absY ) 
        {
           u = (localPos.x / (PlateRadius * 2f)) + 0.5f;
           v = (localPos.y / (PlateRadius * 2f)) + 0.5f;
        }
        else if ( absX >= absY && absX >= absZ ) 
        {
           u = (localPos.z / (PlateRadius * 2f)) + 0.5f;
           v = (localPos.y / (PlateRadius * 2f)) + 0.5f;
        }
        else 
        {
           u = (localPos.x / (PlateRadius * 2f)) + 0.5f;
           v = (localPos.z / (PlateRadius * 2f)) + 0.5f;
        }

        return new Vector2( Math.Clamp(u, 0, 1), Math.Clamp(1.0f - v, 0, 1) ); 
    }
    
    /// <summary>
    /// Validates if the hit object is the plate or structurally linked to the plate.
    /// Necessary for complex models with multiple child mesh components.
    /// </summary>
    private bool IsPartOfPlate( GameObject obj )
    {
        if ( obj == null || Plate == null ) return false;

        return obj == Plate || 
               obj.Parent == Plate.Parent || 
               obj.IsAncestor( Plate ) || 
               Plate.IsAncestor( obj );
    }

    /// <summary>
    /// Iterates through the byte array in a circular pattern around the UV coordinate.
    /// Sets values to 0 (clean) to reveal the clean texture in the shader.
    /// </summary>
    private void UpdateMask( Vector2 uv )
    {
        int centerX = (int)(uv.x * _textureSize);
        int centerY = (int)(uv.y * _textureSize);
        int radiusIdx = (int)(BrushRadius * _textureSize);

        for ( int x = -radiusIdx; x <= radiusIdx; x++ )
        {
            for ( int y = -radiusIdx; y <= radiusIdx; y++ )
            {
                // Circular brush check using Pythagorean distance
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
                            _isDirty = true; 
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Scans the mask to determine completion ratio.
    /// On threshold (92%), triggers the authoritative ledger logic for rewards.
    /// </summary>
    private void CalculateProgress()
    {
        int cleanedPixels = 0;
        for ( int i = 0; i < _maskData.Length; i++ )
        {
            if ( _maskData[i] == 0 ) cleanedPixels++;
        }

        CleanPercentage = (float)cleanedPixels / _maskData.Length * 100f;

        if ( CleanPercentage >= 92f )
        {
            _isLevelComplete = true;
            // Reward logic is handled via the Sinvest Ledger model (Inflow)
            Log.Info( "Dish Cleaned! Great job." );
        }
    }
}
