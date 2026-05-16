using Sandbox;
using System.Linq;
using System.Threading.Tasks;

namespace Sinvest;

public sealed class DishwasherWorkTrigger : Component, Component.IPressable
{
    [Property] public GameObject MinigamePrefab { get; set; }
    [Property] public float MaxDistance { get; set; } = 150f;
    [Property] public Vector3 GameSpawnPosition { get; set; } = new Vector3( 5000, 0, 0 );

    [Property, Group( "UI" )] public string TooltipTitle { get; set; } = "Dishwasher";
    [Property, Group( "UI" )] public string TooltipIcon { get; set; } = "flatware";
    [Property, Group( "UI" )] public string TooltipDescription { get; set; } = "Start Shift";

    private GameObject _activeGameInstance;
    private PlayerController _cachedPlayer;
    private CameraComponent _cachedPlayerCamera;

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

    private async void StartGame()
    {
	    if ( !MinigamePrefab.IsValid() )
	    {
		    Log.Warning( "DishwasherWorkTrigger: MinigamePrefab property has not been assigned!" );
		    return;
	    }

	    _cachedPlayer = GetLocalPlayer();
	    if ( _cachedPlayer.IsValid() )
	    {
		    if ( _cachedPlayer.Components.TryGet<Rigidbody>( out var rb ) )
		    {
			    rb.Velocity = Vector3.Zero;
			    rb.AngularVelocity = Vector3.Zero;
			    rb.MotionEnabled = false; 
		    }

		    _cachedPlayerCamera = _cachedPlayer.Components.Get<CameraComponent>( FindMode.EnabledInSelfAndDescendants );
		    if ( _cachedPlayerCamera.IsValid() )
			    _cachedPlayerCamera.Enabled = false;
	    }

	    // 1. SPAWN DISABLED: Turn off components initially so they don't fire events prematurely
	    _activeGameInstance = MinigamePrefab.Clone( new Transform( GameSpawnPosition ), startEnabled: false );
	    if ( !_activeGameInstance.IsValid() )
	    {
		    Log.Error( "DishwasherWorkTrigger: Failed to instantiate assigned prefab template structure." );
		    EndGame();
		    return;
	    }

	    // Enforce pure transform scaling parameters
	    _activeGameInstance.WorldScale = new Vector3( 1f, 1f, 1f );

	    // 2. WAIT FOR COOKING: Allow the system engine to clean and prepare physics registers
	    await Task.Frame();

	    if ( !_activeGameInstance.IsValid() ) return;

	    // 3. ATOMIC ACTIVATION: Wake everything up simultaneously now that spatial bounds are set
	    _activeGameInstance.Enabled = true;

	    // Give it one micro-tick to let the nested OnStart/Awake sequences settle natively
	    await Task.Frame();
	    if ( !_activeGameInstance.IsValid() ) return;

	    Mouse.Visibility = MouseVisibility.Visible;

	    // Run diagnostics — this will now show your true collider structures!
	    RunPrefabDiagnostics();
    }

    private void RunPrefabDiagnostics()
    {
        Log.Info( "--- DISHWASHER WORK TRIGGER DIAGNOSTICS ---" );
        Log.Info( $"Active Game Root Position: {_activeGameInstance.WorldPosition} (Expected: {GameSpawnPosition})" );
        Log.Info( $"Active Game Root Scale: {_activeGameInstance.WorldScale}" );

        // 1. Check for active cameras inside the spawned hierarchy
        var cameras = _activeGameInstance.Components.GetAll<CameraComponent>( FindMode.EverythingInSelfAndDescendants );
        Log.Info( $"Found {cameras.Count()} camera(s) inside spawned prefab." );
        foreach ( var cam in cameras )
        {
            Log.Info( $" -> Camera: '{cam.GameObject.Name}' | Enabled: {cam.Enabled} | Priority: {cam.Priority}" );
        }

        // 2. Check for the UI ScreenPanel trees
        var panels = _activeGameInstance.Components.GetAll<ScreenPanel>( FindMode.EverythingInSelfAndDescendants );
        Log.Info( $"Found {panels.Count()} ScreenPanel(s) inside spawned prefab." );
        foreach ( var panel in panels )
        {
            Log.Info( $" -> Panel Component found on GameObject: '{panel.GameObject.Name}'" );
        }

        // 3. Scan for active colliders if the prefab relies on physical raycasts
        var colliders = _activeGameInstance.Components.GetAll<Collider>( FindMode.EverythingInSelfAndDescendants );
        Log.Info( $"Found {colliders.Count()} physics collider(s) inside spawned prefab." );
        
        Log.Info( "------------------------------------------" );
    }

    protected override void OnUpdate()
    {
        if ( !_activeGameInstance.IsValid() ) return;

        // Check for exit keys
        if ( Input.Pressed( "menu" ) || Input.Pressed( "use" ) )
            EndGame();
    }

    private async void EndGame()
    {
        if ( !_activeGameInstance.IsValid() ) return;

        _activeGameInstance.Destroy();
        _activeGameInstance = null;

        // Clear UI focus safely at the frame boundary
        await Task.Frame();

        if ( _cachedPlayer.IsValid() )
        {
            // Unfreeze physical player movement
            if ( _cachedPlayer.Components.TryGet<Rigidbody>( out var rb ) )
            {
                rb.MotionEnabled = true;
            }

            if ( _cachedPlayerCamera.IsValid() )
                _cachedPlayerCamera.Enabled = true;

            _cachedPlayer.EyeAngles = _cachedPlayer.WorldRotation.Angles();

            Input.Clear( "use" );
            Input.Clear( "menu" );
        }

        _cachedPlayerCamera = null;
        _cachedPlayer = null;

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
