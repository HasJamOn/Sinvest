using Sandbox;

public sealed class InteractFlipper2D : Component, Component.IPressable
{
	bool Component.IPressable.Press( Component.IPressable.Event e )
	{
		if ( GameHUD.Instance != null )
		{
			// Use OpenInteraction to skip the main menu and show Flipper2D
			GameHUD.Instance.OpenInteraction<Flipper2D>();
		}
        
		return true;
	}

	// Boilerplate for IPressable
	bool Component.IPressable.CanPress( Component.IPressable.Event e ) => true;
	void Component.IPressable.Release( Component.IPressable.Event e ) { }
	void Component.IPressable.Hover( Component.IPressable.Event e ) { }
	void Component.IPressable.Blur( Component.IPressable.Event e ) { }
	void Component.IPressable.Look( Component.IPressable.Event e ) { }
}
