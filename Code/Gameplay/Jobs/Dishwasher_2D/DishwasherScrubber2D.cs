using Sandbox;
using System.Linq;
using Sandbox.Audio;

namespace Sinvest;

public sealed class DishwasherScrubber2D : Component
{
    [Property] public float BrushRadius { get; set; } = 0.05f;
    [Property] public float DragSensitivity { get; set; } = 1.0f;

    [Property, Group( "Audio" )] public SoundEvent ScrubSound { get; set; }
    [Property, Group( "Audio" )] public SoundEvent GrabSound { get; set; }

    public Dishwasher2DDynamicMask CurrentHoveredTarget { get; private set; }

    private GameObject _activeDraggingObject;
    private SoundHandle _scrubSoundHandle;
    private CameraComponent _camera;

    protected override void OnStart()
    {
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
            .WithTag( "plate" ) 
            .Run();

        // Target locking and Event subscription
        if ( tr.Hit && tr.GameObject.Components.TryGet<Dishwasher2DDynamicMask>( out var mask ) )
        {
            if ( CurrentHoveredTarget != mask )
            {
                if ( CurrentHoveredTarget is not null )
                    CurrentHoveredTarget.OnDishCleaned -= RewardLabor;

                CurrentHoveredTarget = mask;
                CurrentHoveredTarget.OnDishCleaned += RewardLabor;
            }
        }
        else if ( !Input.Down( "attack2" ) )
        {
            if ( CurrentHoveredTarget is not null )
                CurrentHoveredTarget.OnDishCleaned -= RewardLabor;

            CurrentHoveredTarget = null;
        }

        HandleDragging( tr );
        HandleScrubbing( tr );
    }

    private void RewardLabor()
    {
        if ( EconomyManager.Instance is not null )
        {
            // FIX: Added the 'm' suffix to explicitly pass a high-precision 
            // decimal value. This now seamlessly aligns with the upgraded economy pipeline.
            EconomyManager.Instance.AddLaborIncome( 1.0m, "Dish Scrubber" );
            Log.Info( "[ECONOMY] $1.00 rewarded for labor." );
        }
    }

    private void HandleDragging( SceneTraceResult tr )
    {
        if ( Input.Down( "attack2" ) )
        {
            if ( Input.Pressed( "attack2" ) && tr.Hit )
            {
                _activeDraggingObject = tr.GameObject;
                if ( GrabSound is not null )
                {
                    var handle = Sound.Play( GrabSound );
                    if ( handle.IsValid() )
                    {
                       handle.ListenLocal = true;
                       handle.DistanceAttenuation = false;
                       handle.Occlusion = false;
                    }
                }
            }

            if ( _activeDraggingObject.IsValid() )
            {
                float unitsPerPixel = _camera.OrthographicHeight / Screen.Height;
                Vector3 moveDir = new Vector3( -Mouse.Delta.y * unitsPerPixel, -Mouse.Delta.x * unitsPerPixel, 0 ) * DragSensitivity;
                _activeDraggingObject.WorldPosition += moveDir;
            }
        }
        else
        {
            _activeDraggingObject = null;
        }
    }

    private void HandleScrubbing( SceneTraceResult tr )
    {
        if ( Input.Down( "attack1" ) && tr.Hit )
        {
            var localPos = tr.GameObject.WorldTransform.PointToLocal( tr.HitPosition );
            Vector2 uv = new Vector2( (localPos.y / 100f) + 0.5f, (localPos.x / 100f) + 0.5f );

            if ( tr.GameObject.Components.TryGet<Dishwasher2DDynamicMask>( out var plateMask ) )
            {
                plateMask.CleanAtUV( uv, BrushRadius );

                if ( ScrubSound is not null && ( _scrubSoundHandle == null || !_scrubSoundHandle.IsPlaying ) )
                {
                   _scrubSoundHandle = Sound.Play( ScrubSound );
                   if ( _scrubSoundHandle.IsValid() )
                   {
                      _scrubSoundHandle.ListenLocal = true;
                      _scrubSoundHandle.DistanceAttenuation = false;
                      _scrubSoundHandle.Occlusion = false;
                   }
                }
            }
        }
        else if ( _scrubSoundHandle != null )
        {
            _scrubSoundHandle.Stop();
            _scrubSoundHandle = null;
        }
    }

    protected override void OnDisabled()
    {
        if ( CurrentHoveredTarget is not null )
            CurrentHoveredTarget.OnDishCleaned -= RewardLabor;
    }

    protected override void OnDestroy()
    {
        // Safety Catch: Ensure dynamic events completely tear down if the component 
        // is deleted, preventing unmanaged memory hooks on old masks.
        if ( CurrentHoveredTarget is not null )
            CurrentHoveredTarget.OnDishCleaned -= RewardLabor;
    }
}
