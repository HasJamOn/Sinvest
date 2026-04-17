using Sandbox;
using System.Linq;

public sealed class UIPopupTrigger : Component, Component.IPressable
{
    [Property] public GameObject PopupPrefab { get; set; }
    
    /// <summary>
    /// Reference to the spawned game so we can track when it's closed
    /// </summary>
    private GameObject _activeInstance;

    bool Component.IPressable.Press( Component.IPressable.Event e )
    {
        if ( PopupPrefab == null || _activeInstance.IsValid() ) return false;

        // 1. Spawn the Flipper2D Prefab
        _activeInstance = PopupPrefab.Clone();
        _activeInstance.Transform.Position = new Vector3( 5000, 0, 0 );

        // 2. Suppress Player Input (Inspired by GameHUD logic)
        SetPlayerInputState( false );

        return true;
    }

    protected override void OnUpdate()
    {
        // 3. Monitor for the prefab being destroyed (Player exited the game)
        // If the instance was valid but is now gone, give control back.
        if ( _activeInstance != null && !_activeInstance.IsValid() )
        {
            SetPlayerInputState( true );
            _activeInstance = null;
        }
    }

    private void SetPlayerInputState( bool enabled )
    {
        // Find the local player controller
        var player = Scene.GetAllComponents<PlayerController>().FirstOrDefault( x => !x.IsProxy );
        
        if ( player != null )
        {
            // Toggle controls without disabling the whole component
            player.UseLookControls = enabled;
            player.UseInputControls = enabled;

            // Stop movement immediately if we are taking control away
            if ( !enabled )
            {
                player.WishVelocity = Vector3.Zero;
                Mouse.Visible = true;
            }
            else
            {
                Mouse.Visible = false;
            }
        }
    }

    bool Component.IPressable.CanPress( Component.IPressable.Event e ) => !_activeInstance.IsValid();
    void Component.IPressable.Release( Component.IPressable.Event e ) { }
    void Component.IPressable.Hover( Component.IPressable.Event e ) { }
    void Component.IPressable.Blur( Component.IPressable.Event e ) { }
    void Component.IPressable.Look( Component.IPressable.Event e ) { }
}
