using Sandbox;
using Sandbox.Movement;
using System.Linq;

public sealed class UIPopupTrigger : Component
{
    [Property] public GameObject PopupPrefab { get; set; }
    [Property] public float MaxInteractionDistance { get; set; } = 100f;

    [Property, Feature( "Tooltip" )] public string TooltipTitle { get; set; } = "PC Station";
    [Property, Feature( "Tooltip" )] public string TooltipIcon { get; set; } = "computer";
    [Property, Feature( "Tooltip" )] public string TooltipDescription { get; set; } = "Open Computer";
    [Property, Feature( "Tooltip" )] public string ToolTipKey { get; set; } = "Enter";

    private GameObject _activeInstance;

    protected override void OnUpdate()
    {
	    if ( Input.Pressed( "sinvestos" ) )
	    {
		    // Find the local player
		    var player = Scene.GetAllComponents<PlayerController>().FirstOrDefault( x => !x.IsProxy );
        
		    if ( player != null )
		    {
			    // Search ancestors for the sitting interface
			    var sittingInterface = player.Components.Get<ISitTarget>( FindMode.EverythingInAncestors );

			    // FIX: Use != null instead of .IsValid() for interfaces
			    if ( sittingInterface != null && IsPlayerLookingAtMe() )
			    {
				    TogglePopup();
			    }
		    }
	    }
    }

    private bool IsPlayerLookingAtMe()
    {
        var ray = Scene.Camera.ScreenNormalToRay( 0.5f );
        var tr = Scene.Trace.Ray( ray, MaxInteractionDistance )
            .WithTag( "solid" ) 
            .Run();

        return tr.Hit && (tr.GameObject == GameObject || tr.GameObject.IsDescendant( GameObject ));
    }

    private void TogglePopup()
    {
        if ( PopupPrefab == null || _activeInstance.IsValid() )
            return;

        _activeInstance = PopupPrefab.Clone();
        _activeInstance.WorldPosition = new Vector3( 5000, 0, 0 );

        var hud = _activeInstance.Components.Get<ComputerHUD>( true );
        if ( hud != null )
        {
            hud.Open();
        }
    }

    protected override void OnDestroy()
    {
        if ( _activeInstance.IsValid() )
            _activeInstance.Destroy();
    }
}
