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

    /// <summary>
    /// Static reference to the currently open popup instance across the game.
    /// </summary>
    public static GameObject ActiveInstance { get; private set; }

    protected override void OnUpdate()
    {
        if ( Input.Pressed( "sinvestos" ) )
        {
           var player = Scene.GetAllComponents<PlayerController>().FirstOrDefault( x => !x.IsProxy );
        
           if ( player != null )
           {
              var sittingInterface = player.Components.Get<ISitTarget>( FindMode.EverythingInAncestors );

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
        // Don't open a new one if an instance already exists
        if ( PopupPrefab == null || ActiveInstance.IsValid() )
            return;

        ActiveInstance = PopupPrefab.Clone();
        ActiveInstance.WorldPosition = new Vector3( 5000, 0, 0 );

        var hud = ActiveInstance.Components.Get<ComputerHUD>( true );
        if ( hud != null )
        {
            hud.Open();
        }
    }

    protected override void OnDestroy()
    {
        // Ensure we clear the static reference if this specific trigger created it
        if ( ActiveInstance.IsValid() )
        {
            ActiveInstance.Destroy();
            ActiveInstance = null;
        }
    }
}
