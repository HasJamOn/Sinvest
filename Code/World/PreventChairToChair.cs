using Sandbox; 
using Sinvest;
using Sandbox.Movement;

public sealed class PreventChairToChair : Component, Component.IPressable
{
	[Property] public BaseChair ChairComponent { get; set; }

	bool Component.IPressable.CanPress( Component.IPressable.Event e )
	{
		return ChairComponent.IsValid();
	}

	bool Component.IPressable.Press( Component.IPressable.Event e )
	{
		var playerController = e.Source.GameObject.Components.Get<PlayerController>( FindMode.EverythingInSelfAndAncestors );
		if ( !playerController.IsValid() ) return false;

		// Check if the player is currently in the SitMoveMode
		var currentSitMode = playerController.Components.Get<SitMoveMode>( FindMode.EverythingInSelfAndChildren );

		if ( currentSitMode.IsValid() )
		{
			// IMPORTANT: We only intercept if the player is trying to interact with a CHAIR.
			// Since this component is attached to your chair/collider, 
			// interacting with it while sitting should trigger an exit.
            
			var currentChair = playerController.GetComponentInParent<ISitTarget>();
			if ( currentChair != null )
			{
				currentChair.AskToLeave( playerController );
				return true; 
			}
		}

		// If not sitting, or if we passed the check above, proceed with the sit logic
		ChairComponent.Sit( playerController );
		return true;
	}

	void Component.IPressable.Release( Component.IPressable.Event e ) { }
	void Component.IPressable.Hover( Component.IPressable.Event e ) { }
}
