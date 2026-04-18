using Sandbox;
using System.Linq;

public sealed class UIPopupTrigger : Component, Component.IPressable
{
	[Property] public GameObject PopupPrefab { get; set; }
	private GameObject _activeInstance;

	bool Component.IPressable.Press( Component.IPressable.Event e )
	{
		if ( PopupPrefab == null || _activeInstance.IsValid() ) return false;

		// 1. Create the runtime clone
		_activeInstance = PopupPrefab.Clone();
        
		// 2. Set the position (Fixed: WorldPosition is a direct property of GameObject)
		_activeInstance.WorldPosition = new Vector3( 5000, 0, 0 );

		// 3. Get the HUD component and initialize it
		var hud = _activeInstance.Components.Get<ComputerHUD>(true);
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
