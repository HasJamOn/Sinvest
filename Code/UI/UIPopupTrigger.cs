using Sandbox; 
using Sinvest;
using Sandbox.Movement;
using System.Linq;

/**
 * UIPopupTrigger Component
 * Manages interaction with stationary terminals.
 * Audio is now configured as a 2D UI sound for maximum clarity.
 */
public sealed class UIPopupTrigger : Component
{
    [Property, Group( "References" )] public GameObject PopupPrefab { get; set; }
    [Property, Group( "References" )] public float MaxInteractionDistance { get; set; } = 100f;

    /* --- AUDIO CONFIGURATION --- */
    private string _openSound = "sounds/ui/sinvestos_boot.sound";

    [Property, Group( "Audio" ), ResourceType( "sound" )]
    public string OpenSound
    {
	    get => string.IsNullOrEmpty( _openSound ) ? "sounds/ui/sinvestos_boot.sound" : _openSound;
	    set => _openSound = value;
    }

    [Property, Feature( "Tooltip" )] public string TooltipTitle { get; set; } = "PC Station";
    [Property, Feature( "Tooltip" )] public string TooltipIcon { get; set; } = "computer";
    [Property, Feature( "Tooltip" )] public string TooltipDescription { get; set; } = "Open Computer";
    [Property, Feature( "Tooltip" )] public string ToolTipKey { get; set; } = "Enter";

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
	    // 1. Safety Check
	    if ( PopupPrefab == null || ActiveInstance.IsValid() )
		    return;

	    // 2. Clone the Prefab
	    ActiveInstance = PopupPrefab.Clone();
    
	    // If the clone failed for some reason, stop here
	    if ( !ActiveInstance.IsValid() )
		    return;

	    // 3. Move it and play the sound IMMEDIATELY
	    ActiveInstance.WorldPosition = new Vector3( 5000, 0, 0 );
    
	    // We play the sound here because the "Action" of opening was successful
	    Sound.Play( OpenSound );

	    // 4. Try to find the HUD script and open it
	    // Using GetInDescendants in case the component is on a child object
	    var hud = ActiveInstance.Components.Get<SinvestOS>( FindMode.EverythingInDescendants );
	    if ( hud != null )
	    {
		    hud.Open();
	    }
	    else
	    {
		    Log.Warning( $"[UIPopupTrigger] Spawned {ActiveInstance.Name} but couldn't find SinvestOS component!" );
	    }
    }

    protected override void OnDestroy()
    {
        if ( ActiveInstance.IsValid() )
        {
            ActiveInstance.Destroy();
            ActiveInstance = null;
        }
    }
}
