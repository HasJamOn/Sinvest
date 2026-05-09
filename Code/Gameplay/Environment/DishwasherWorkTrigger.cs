using Sandbox;
using System.Linq;

namespace Sinvest;

public sealed class DishwasherWorkTrigger : Component, Component.IPressable
{
    [Property] public GameObject GamePrefab { get; set; }
    [Property] public float MaxDistance { get; set; } = 150f;
    [Property] public Vector3 GameSpawnPosition { get; set; } = new Vector3( 5000, 0, 0 );

    [Property, Group( "UI" )] public string TooltipTitle { get; set; } = "Dishwasher";
    [Property, Group( "UI" )] public string TooltipIcon { get; set; } = "flatware";
    [Property, Group( "UI" )] public string TooltipDescription { get; set; } = "Start Shift";

    private GameObject _activeGameInstance;
    private PlayerController _cachedPlayer;
    private CameraComponent _cachedPlayerCamera;
    private Rigidbody _cachedPlayerRigidbody;

    public bool CanPress( IPressable.Event e )
    {
        if ( _activeGameInstance.IsValid() ) return false;
        return e.Source.IsValid() && WorldPosition.Distance( e.Source.WorldPosition ) <= MaxDistance;
    }

    public bool Press( IPressable.Event e )
    {
        if ( !CanPress( e ) ) return false;
        StartGame();
        return true;
    }

    private void StartGame()
    {
	    if ( !GamePrefab.IsValid() ) return;

	    _cachedPlayer = GetLocalPlayer();
	    if ( _cachedPlayer.IsValid() )
	    {
		    // 1. Surgical flags: Stop the controller from reading input
		    _cachedPlayer.UseLookControls = false;
		    _cachedPlayer.UseInputControls = false;
		    _cachedPlayer.UseCameraControls = false;

		    // 2. Hide the player's view if they have a personal camera
		    _cachedPlayerCamera = _cachedPlayer.Components.Get<CameraComponent>( FindMode.EnabledInSelfAndDescendants );
		    if ( _cachedPlayerCamera.IsValid() )
			    _cachedPlayerCamera.Enabled = false;
	    }

	    _activeGameInstance = GamePrefab.Clone( new Transform( GameSpawnPosition ) );
    
	    // 3. Set Mouse Visibility
	    Mouse.Visibility = MouseVisibility.Visible;
    }

    protected override void OnUpdate()
    {
        if ( !_activeGameInstance.IsValid() ) return;

        if ( Input.Pressed( "menu" ) || Input.Pressed( "use" ) )
            EndGame();
    }

    private async void EndGame()
    {
	    if ( !_activeGameInstance.IsValid() ) return;

	    // 1. Destroy the minigame instance
	    _activeGameInstance.Destroy();
	    _activeGameInstance = null;

	    // 2. Wait for the end of the frame
	    // This allows the engine to realize the UI is gone and 
	    // clears the "UI Focus" naturally.
	    await Task.Frame();

	    if ( _cachedPlayer.IsValid() )
	    {
		    // Restore controls
		    _cachedPlayer.UseLookControls = true;
		    _cachedPlayer.UseInputControls = true;
		    _cachedPlayer.UseCameraControls = true;

		    if ( _cachedPlayerCamera.IsValid() )
			    _cachedPlayerCamera.Enabled = true;

		    // Reset angles to current view to prevent snapping
		    _cachedPlayer.EyeAngles = _cachedPlayer.WorldRotation.Angles();
        
		    Input.Clear( "use" );
		    Input.Clear( "menu" );
	    }

	    _cachedPlayerCamera = null;
	    _cachedPlayer = null;

	    // 3. Reset Mouse
	    Mouse.Visibility = MouseVisibility.Auto;
    }

    private PlayerController GetLocalPlayer()
        => Scene.GetAllComponents<PlayerController>().FirstOrDefault( x => !x.IsProxy );

    public IPressable.Tooltip? GetTooltip( IPressable.Event e )
        => CanPress( e ) ? new IPressable.Tooltip( TooltipTitle, TooltipIcon, TooltipDescription, true, this ) : null;

    public void Hover( IPressable.Event e ) { }
    public void Look( IPressable.Event e ) { }
    public void Blur( IPressable.Event e ) { }
    public bool Pressing( IPressable.Event e ) => true;
    public void Release( IPressable.Event e ) { }
}
