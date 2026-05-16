using Sandbox;
using System.Linq;
using System.Threading.Tasks;

namespace Sinvest;

public sealed class DishwasherWorkTrigger : Component, Component.IPressable
{
    /// <summary>The prefabricated assembly template containing the minigame manager, cameras, and elements</summary>
    [Property] public GameObject MinigamePrefab { get; set; }
    [Property] public float MaxDistance { get; set; } = 150f;
    [Property] public Vector3 GameSpawnPosition { get; set; } = new Vector3( 5000, 0, 0 );

    [Property, Group( "UI" )] public string TooltipTitle { get; set; } = "Dishwasher";
    [Property, Group( "UI" )] public string TooltipIcon { get; set; } = "flatware";
    [Property, Group( "UI" )] public string TooltipDescription { get; set; } = "Start Shift";

    private GameObject _activeGameInstance;
    private PlayerController _cachedPlayer;
    private CameraComponent _cachedPlayerCamera;

    public bool CanPress( Component.IPressable.Event e )
    {
        if ( _activeGameInstance.IsValid() ) return false;
        return e.Source.IsValid() && WorldPosition.Distance( e.Source.WorldPosition ) <= MaxDistance;
    }

    public bool Press( Component.IPressable.Event e )
    {
        if ( !CanPress( e ) ) return false;
        StartGame();
        return true;
    }

    private void StartGame()
    {
        if ( !MinigamePrefab.IsValid() )
        {
            Log.Warning( "DishwasherWorkTrigger: MinigamePrefab property has not been assigned in the Inspector!" );
            return;
        }

        _cachedPlayer = GetLocalPlayer();
        if ( _cachedPlayer.IsValid() )
        {
            // 1. Lock down the player character's movement/camera mechanics
            _cachedPlayer.UseLookControls = false;
            _cachedPlayer.UseInputControls = false;
            _cachedPlayer.UseCameraControls = false;

            // 2. Shut off the world character's personal viewport camera
            _cachedPlayerCamera = _cachedPlayer.Components.Get<CameraComponent>( FindMode.EnabledInSelfAndDescendants );
            if ( _cachedPlayerCamera.IsValid() )
                _cachedPlayerCamera.Enabled = false;
        }

        // 3. Correct API: Clone the prefab source GameObject directly into the scene hierarchy
        _activeGameInstance = MinigamePrefab.Clone( new Transform( GameSpawnPosition ) );
        if ( !_activeGameInstance.IsValid() )
        {
            Log.Error( "DishwasherWorkTrigger: Failed to instantiate assigned prefab template structure." );
            EndGame();
            return;
        }

        // 4. Force the hardware pointer to render over the viewport for UI selection interaction
        Mouse.Visibility = MouseVisibility.Visible;
    }

    protected override void OnUpdate()
    {
        if ( !_activeGameInstance.IsValid() ) return;

        // Listen for immediate exit keys to close the shift layout out-of-band
        if ( Input.Pressed( "menu" ) || Input.Pressed( "use" ) )
            EndGame();
    }

    private async void EndGame()
    {
        if ( !_activeGameInstance.IsValid() ) return;

        // Wipe the cloned hierarchy branch completely out of active world memory
        _activeGameInstance.Destroy();
        _activeGameInstance = null;

        // Suspend execution until the current frame update lifecycle completes cleanup passes
        await Task.Frame();

        if ( _cachedPlayer.IsValid() )
        {
            // Restore structural mobility tracking parameters
            _cachedPlayer.UseLookControls = true;
            _cachedPlayer.UseInputControls = true;
            _cachedPlayer.UseCameraControls = true;

            if ( _cachedPlayerCamera.IsValid() )
                _cachedPlayerCamera.Enabled = true;

            // Snap view constraints cleanly back to structural heading values
            _cachedPlayer.EyeAngles = _cachedPlayer.WorldRotation.Angles();

            Input.Clear( "use" );
            Input.Clear( "menu" );
        }

        _cachedPlayerCamera = null;
        _cachedPlayer = null;

        // Reset tracking back to normal runtime context rules
        Mouse.Visibility = MouseVisibility.Auto;
    }

    private PlayerController GetLocalPlayer()
        => Scene.GetAllComponents<PlayerController>().FirstOrDefault( x => !x.IsProxy );

    public Component.IPressable.Tooltip? GetTooltip( Component.IPressable.Event e )
    {
        if ( !CanPress( e ) ) return null;
        return new Component.IPressable.Tooltip( TooltipTitle, TooltipIcon, TooltipDescription, true, this );
    }

    public void Hover( Component.IPressable.Event e ) { }
    public void Look( Component.IPressable.Event e ) { }
    public void Blur( Component.IPressable.Event e ) { }
    public bool Pressing( Component.IPressable.Event e ) => true;
    public void Release( Component.IPressable.Event e ) { }
}
