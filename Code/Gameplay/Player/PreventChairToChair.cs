using Sandbox; 
using Sinvest;
using Sandbox.Movement;

/// <summary>
/// Intercepts interaction events to prevent "Chair-to-Chair" teleportation.
/// Ensures a player must exit their current sitting state before entering a new one.
/// </summary>
public sealed class PreventChairToChair : Component, Component.IPressable
{
    [Property] public BaseChair ChairComponent { get; set; }

    /// <summary>
    /// Validates if the interaction is possible. 
    /// Requirement: The target ChairComponent must be valid and active.
    /// </summary>
    bool Component.IPressable.CanPress( Component.IPressable.Event e )
    {
       return ChairComponent.IsValid();
    }

    /// <summary>
    /// Handles the interaction logic. 
    /// If the player is already sitting, it forces an exit from the current chair instead of switching.
    /// </summary>
    bool Component.IPressable.Press( Component.IPressable.Event e )
    {
       // Locate the PlayerController from the interaction source (e.g., the player's pawn/hand).
       var playerController = e.Source.GameObject.Components.Get<PlayerController>( FindMode.EverythingInSelfAndAncestors );
       if ( !playerController.IsValid() ) return false;

       // Check if the player is currently utilizing the SitMoveMode (active sitting state).
       var currentSitMode = playerController.Components.Get<SitMoveMode>( FindMode.EverythingInSelfAndChildren );

       if ( currentSitMode.IsValid() )
       {
          // Intercept Logic: If the player attempts to interact with this chair while already sitting elsewhere.
          // Identifies the current ISitTarget to trigger a formal exit request.
          var currentChair = playerController.GetComponentInParent<ISitTarget>();
          if ( currentChair != null )
          {
             // Standard s&box protocol: Request to leave the current target before allowing a new interaction.
             currentChair.AskToLeave( playerController );
             return true; 
          }
       }

       // If the player is standing, proceed with the standard Sit transition logic.
       ChairComponent.Sit( playerController );
       return true;
    }

    void Component.IPressable.Release( Component.IPressable.Event e ) { }
    void Component.IPressable.Hover( Component.IPressable.Event e ) { }
}
