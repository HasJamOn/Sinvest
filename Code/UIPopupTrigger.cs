using Sandbox;
using System.Linq;

public sealed class UIPopupTrigger : Component, Component.IPressable
{
	[Property] public GameObject PopupPrefab { get; set; }
	private GameObject _activeInstance;

	bool Component.IPressable.Press( Component.IPressable.Event e )
	{
		// Don't spawn if one is already open
		if ( PopupPrefab == null || _activeInstance.IsValid() ) return false;

		// 1. Create the runtime clone (This fixes the serialization error)
		_activeInstance = PopupPrefab.Clone();
        
		// Optional: Move it out of the way if it has physical components
		_activeInstance.Transform.Position = new Vector3( 5000, 0, 0 );

		// 2. Get the HUD component and initialize it
		// Note: Using your class name 'ComputerHUD'
		var hud = _activeInstance.GetComponent<ComputerHUD>(true);
		if ( hud != null )
		{
			hud.Open(); 
		}

		return true;
	}

	bool Component.IPressable.CanPress( Component.IPressable.Event e ) => !_activeInstance.IsValid();
    
	// Stub methods for IPressable
	void Component.IPressable.Release( Component.IPressable.Event e ) { }
	void Component.IPressable.Hover( Component.IPressable.Event e ) { }
	void Component.IPressable.Blur( Component.IPressable.Event e ) { }
	void Component.IPressable.Look( Component.IPressable.Event e ) { }
}
