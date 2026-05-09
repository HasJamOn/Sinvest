using Sandbox;
using System;

namespace Sinvest;

public class DishwasherDynamicMask : Component
{
    [Property] public float BrushRadius { get; set; } = 0.05f;
    [Property] public float PlateRadius { get; set; } = 50f;
    [Property] public float RotationSpeed { get; set; } = 0.5f;

    private Texture _maskTexture;
    private byte[] _maskData;
    private int _textureSize = 256;
    private bool _isDirty = false;
    private int _cleanedPixelCount = 0;

    [Property, ReadOnly] public float CleanPercentage { get; private set; } = 0f;

    // Helper to check if the object is static since 'GameObject.Static' isn't a direct symbol
    private bool IsStatic => GameObject.Tags.Has( "static" );

    protected override void OnStart()
    {
       InitializeMask();

       if ( IsStatic )
       {
          Log.Warning( $"[Sinvest] {GameObject.Name} has the 'static' tag. It will not rotate! Remove 'static' in the Inspector tags." );
       }
    }

    protected override void OnUpdate()
    {
	    if ( !Input.Down( "attack2" ) ) return;

	    // 1. Identify the Target (Plate_Root)
	    var plateRoot = GameObject.Parent;
	    if ( !plateRoot.IsValid() ) return;

	    // 2. Get Mouse Movement
	    var delta = Input.MouseDelta;
	    if ( delta.Length <= 0 ) return;

	    // 3. Get Camera Orientation
	    // We rotate around the camera's relative axes so it feels 'correct' to the player
	    var camRot = Scene.Camera.WorldRotation;
	    var right = camRot.Right;
	    var up = camRot.Up;

	    // 4. Apply Rotation using the Camera as a reference frame
	    // Horizontal mouse movement rotates around the Camera's Up axis
	    // Vertical mouse movement rotates around the Camera's Right axis
	    plateRoot.WorldRotation = Rotation.FromAxis( up, -delta.x * RotationSpeed ) * plateRoot.WorldRotation;
	    plateRoot.WorldRotation = Rotation.FromAxis( right, delta.y * RotationSpeed ) * plateRoot.WorldRotation;

	    // Visual feedback that the code is executing
	    DebugOverlay.ScreenText( new Vector2( 50, 50 ), $"Rotating: {plateRoot.Name}", 0.1f );
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
       {
          renderer.Attributes.Set( "DirtMask", _maskTexture );
       }
    }

    public void CleanAtWorldLocation( SceneTraceResult tr )
    {
       // Transform world hit to local space
       Vector3 localPos = GameObject.WorldTransform.PointToLocal( tr.HitPosition );
       Vector3 localNormal = GameObject.WorldTransform.NormalToLocal( tr.Normal );
    
       float u, v;
       float absX = Math.Abs( localNormal.x );
       float absY = Math.Abs( localNormal.y );
       float absZ = Math.Abs( localNormal.z );

       // Planar projection logic
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

       UpdateMask( new Vector2( Math.Clamp( u, 0, 1 ), Math.Clamp( 1.0f - v, 0, 1 ) ) );
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
                int px = centerX + x;
                int py = centerY + y;
                
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
