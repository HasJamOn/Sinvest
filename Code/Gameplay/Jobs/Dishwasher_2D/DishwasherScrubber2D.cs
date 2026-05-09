using Sandbox;
using System.Linq;

namespace Sinvest;

public sealed class DishwasherScrubber2D : Component
{
    [Property] public float BrushRadius { get; set; } = 0.05f;
    [Property] public float DragSensitivity { get; set; } = 1.0f;

    [Property, Group( "Audio" )] public SoundEvent ScrubSound { get; set; }
    [Property, Group( "Audio" )] public SoundEvent GrabSound { get; set; }

    /// <summary>
    /// The Mask component of the plate currently under the mouse.
    /// Your UI can read this to show the specific plate's progress.
    /// </summary>
    public Dishwasher2DDynamicMask CurrentHoveredTarget { get; private set; }

    private GameObject _activeDraggingObject;
    private SoundHandle _scrubSoundHandle;

    private CameraComponent _camera;

    protected override void OnStart()
    {
	    // Grab the camera this component lives on — never use Scene.Camera,
	    // which may resolve to the player's disabled camera mid-transition.
	    _camera = Components.Get<CameraComponent>( FindMode.InSelf );
	    Mouse.Visibility = MouseVisibility.Visible;
    }

    protected override void OnUpdate()
    {
	    if ( Mouse.Visibility != MouseVisibility.Visible )
		    Mouse.Visibility = MouseVisibility.Visible;

	    if ( _camera is null ) return;

	    var mouseRay = _camera.ScreenPixelToRay( Mouse.Position );
        var tr = Scene.Trace.Ray( mouseRay, 1500f )
            .UsePhysicsWorld()
            .WithTag( "plate" ) // Make sure your prefab root has the "plate" tag
            .Run();

        // 3. Update the Hovered Target for the UI
        if ( tr.Hit && tr.GameObject.Components.TryGet<Dishwasher2DDynamicMask>( out var mask ) )
        {
            CurrentHoveredTarget = mask;
        }
        else if ( !Input.Down( "attack2" ) ) // Keep the target locked if we are still dragging it
        {
            CurrentHoveredTarget = null;
        }

        // --- Handle Dragging (Right Click / Attack2) ---
        if ( Input.Down( "attack2" ) )
        {
            // Grab the object on the first frame of the click
            if ( Input.Pressed( "attack2" ) && tr.Hit )
            {
                _activeDraggingObject = tr.GameObject;
                
                // Play grab sound at the hit location
                if ( GrabSound is not null )
                    Sound.Play( GrabSound, tr.HitPosition );
            }

            if ( _activeDraggingObject.IsValid() )
            {
                // Match world units to screen pixels using Camera Ortho height
                float unitsPerPixel = _camera.OrthographicHeight / Screen.Height;

                // Apply your verified axis-swap and inversion logic
                float moveX = -Mouse.Delta.y * unitsPerPixel;
                float moveY = -Mouse.Delta.x * unitsPerPixel;

                Vector3 moveDir = new Vector3( moveX, moveY, 0 ) * DragSensitivity;
                _activeDraggingObject.WorldPosition += moveDir;
            }
        }
        else
        {
            _activeDraggingObject = null;
        }

        // --- Handle Scrubbing (Left Click / Attack1) ---
        if ( Input.Down( "attack1" ) && tr.Hit )
        {
            // Transform hit position to the local space of the specific plate
            var localPos = tr.GameObject.WorldTransform.PointToLocal( tr.HitPosition );

            // Convert local units (-50 to 50) to UV (0 to 1)
            // Assuming plane.vmdl (100x100) and your established axis rotation
            Vector2 uv = new Vector2(
                (localPos.y / 100f) + 0.5f,
                (localPos.x / 100f) + 0.5f
            );

            if ( tr.GameObject.Components.TryGet<Dishwasher2DDynamicMask>( out var plateMask ) )
            {
                plateMask.CleanAtUV( uv, BrushRadius );

                // Manage looping scrub sound
                if ( ScrubSound is not null && ( _scrubSoundHandle == null || !_scrubSoundHandle.IsPlaying ) )
                {
                    _scrubSoundHandle = Sound.Play( ScrubSound, tr.HitPosition );
                }
                
                // Update position of the sound to follow the brush
                if ( _scrubSoundHandle != null )
                {
                    _scrubSoundHandle.Position = tr.HitPosition;
                }
            }
        }
        else
        {
            // Stop scrubbing sound when mouse is released
            if ( _scrubSoundHandle != null )
            {
                _scrubSoundHandle.Stop();
                _scrubSoundHandle = null;
            }
        }
    }
}
