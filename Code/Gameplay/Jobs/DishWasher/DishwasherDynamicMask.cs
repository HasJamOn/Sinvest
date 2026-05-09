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

    [Property, ReadOnly] public float CleanPercentage { get; private set; } = 0f;

    private bool IsStatic => GameObject.Tags.Has( "static" );

    protected override void OnStart()
    {
        InitializeRadius();
        InitializeMask();

        if ( IsStatic )
        {
            Log.Warning( $"[Sinvest] {GameObject.Name} has the 'static' tag. It will not rotate! Remove 'static' in the Inspector tags." );
        }
    }

    /// <summary>
    /// Evaluates the ModelRenderer's bounding box to establish an initial cleaning area.
    /// This ensures the UV projection matches the physical size of the mesh.
    /// </summary>
    private void InitializeRadius()
    {
        if ( Components.TryGet<ModelRenderer>( out var renderer ) && renderer.Model is not null )
        {
            var bounds = renderer.Model.Bounds;
            // Radius is half of the largest dimension of the bounding box
            CalculatedPlateRadius = MathF.Max( bounds.Size.x, MathF.Max( bounds.Size.y, bounds.Size.z ) ) / 2f;
        }
    }

    /// <summary>
    /// Renders interactive editor handles and visualizes the cleaning bounds.
    /// Uses BoundingBox controls to allow developers to fine-tune the projection area.
    /// </summary>
    protected override void OnPreRender()
    {
        if ( !Gizmo.IsSelected ) return;

        // Draw a bounding box handle that allows real-time resizing of the cleaning area
        var currentBBox = new BBox( new Vector3( -CalculatedPlateRadius ), new Vector3( CalculatedPlateRadius ) );
    
        if ( Gizmo.Control.BoundingBox( "plate_resize", currentBBox, out var nextBBox ) )
        {
           // Extract the largest component of the user's drag to set the new uniform radius
           float maxSide = MathF.Max( nextBBox.Size.x, MathF.Max( nextBBox.Size.y, nextBBox.Size.z ) );
           CalculatedPlateRadius = maxSide / 2f;
        }

        // Draw a cyan wire-sphere to show the maximum extent of the projected cleaning mask
        Gizmo.Draw.Color = Color.Cyan.WithAlpha( 0.4f );
        Gizmo.Draw.LineSphere( Vector3.Zero, CalculatedPlateRadius );

        // Draw a yellow circle representing the brush scale relative to the plate size
        Gizmo.Draw.Color = Color.Yellow;
        Gizmo.Draw.LineCircle( Vector3.Zero, Vector3.Up, BrushRadius * CalculatedPlateRadius );
    }

    /// <summary>
    /// Manages object rotation based on mouse movement when the 'attack2' (Right Click) 
    /// input is held. Adjusts based on the current camera orientation.
    /// </summary>
    protected override void OnUpdate()
    {
        if ( !Input.Down( "attack2" ) ) return;

        var plateRoot = GameObject.Parent;
        if ( !plateRoot.IsValid() ) return;

        var delta = Input.MouseDelta;
        if ( delta.Length <= 0 ) return;

        var camRot = Scene.Camera.WorldRotation;
        
        // Rotate the root object relative to the camera's view axes
        plateRoot.WorldRotation = Rotation.FromAxis( camRot.Up, -delta.x * RotationSpeed ) * plateRoot.WorldRotation;
        plateRoot.WorldRotation = Rotation.FromAxis( camRot.Right, delta.y * RotationSpeed ) * plateRoot.WorldRotation;
    }

    /// <summary>
    /// Creates the initial 8-bit (I8) grayscale texture and assigns it to the 
    /// ModelRenderer's shader attributes.
    /// </summary>
    private void InitializeMask()
    {
        _maskData = new byte[_textureSize * _textureSize];
        
        // Fill the mask with 255 (white/dirty)
        for ( int i = 0; i < _maskData.Length; i++ ) _maskData[i] = 255;
        
        _maskTexture = Texture.Create( _textureSize, _textureSize )
            .WithFormat( ImageFormat.I8 )
            .WithData( _maskData )
            .WithDynamicUsage() // Required for frequent CPU -> GPU updates
            .Finish();

        if ( Components.TryGet<ModelRenderer>( out var renderer ) )
        {
            // The shader must have a 'DirtMask' sampler to use this
            renderer.Attributes.Set( "DirtMask", _maskTexture );
        }
    }

    /// <summary>
    /// Converts a world-space trace hit into local UV coordinates using tri-planar projection.
    /// This determines which part of the mask texture corresponds to the hit point.
    /// </summary>
    public void CleanAtWorldLocation( SceneTraceResult tr )
    {
        Vector3 localPos = GameObject.WorldTransform.PointToLocal( tr.HitPosition );
        Vector3 localNormal = GameObject.WorldTransform.NormalToLocal( tr.Normal );
    
        float u, v;
        float absX = MathF.Abs( localNormal.x );
        float absY = MathF.Abs( localNormal.y );
        float absZ = MathF.Abs( localNormal.z );

        float diameter = CalculatedPlateRadius * 2f;

        // Tri-planar logic: determine which axis the hit is mostly facing and project accordingly
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

        UpdateMask( new Vector2( Math.Clamp( u, 0, 1 ), Math.Clamp( 1.0f - v, 0, 1 ) ) );
    }

    /// <summary>
    /// Iterates through the pixel data in a circular pattern around the UV coordinate
    /// to "erase" the dirt (set value to 0). Updates the GPU texture if changes occur.
    /// </summary>
    private void UpdateMask( Vector2 uv )
    {
        int centerX = (int)(uv.x * _textureSize);
        int centerY = (int)(uv.y * _textureSize);
        int radiusIdx = (int)(BrushRadius * _textureSize);

        // Perform a nested loop to simulate a circular brush tip
        for ( int x = -radiusIdx; x <= radiusIdx; x++ )
        {
            for ( int y = -radiusIdx; y <= radiusIdx; y++ )
            {
                // Pythagorean distance check for circular brush shape
                if ( x * x + y * y <= radiusIdx * radiusIdx )
                {
                    int px = centerX + x;
                    int py = centerY + y;
                    
                    if ( px >= 0 && px < _textureSize && py >= 0 && py < _textureSize )
                    {
                        int index = py * _textureSize + px;
                        if ( _maskData[index] != 0 )
                        {
                            _maskData[index] = 0; // Set to black (clean)
                            _cleanedPixelCount++;
                            _isDirty = true; 
                        }
                    }
                }
            }
        }

        // Only push data to the GPU if at least one pixel changed this frame
        if ( _isDirty )
        {
            _maskTexture.Update( _maskData );
            CleanPercentage = (float)_cleanedPixelCount / (_textureSize * _textureSize) * 100f;
            _isDirty = false;
        }
    }
}
