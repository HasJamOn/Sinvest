using Sandbox;

public sealed class InteractComputer : Component, Component.IPressable
{
	[Property] public Sinvest UI { get; set; }

	bool Component.IPressable.Press( Component.IPressable.Event e )
	{
		UI?.Open();
		return true;
	}

	bool Component.IPressable.CanPress( Component.IPressable.Event e ) => true;
	void Component.IPressable.Release( Component.IPressable.Event e ) { }
	void Component.IPressable.Hover( Component.IPressable.Event e ) { }
	void Component.IPressable.Blur( Component.IPressable.Event e ) { }
	void Component.IPressable.Look( Component.IPressable.Event e ) { }

	protected override void OnUpdate()
	{

	}
}
