using Sandbox;
using System.Linq;

public sealed class UIPopupTrigger : Component
{
	[Property] public GameObject PopupPrefab { get; set; }
	[Property] public float MaxInteractionDistance { get; set; } = 100f;
    
	private GameObject _activeInstance;

	protected override void OnUpdate()
	{
		// 1. Check if the custom action "sinvestos" was pressed
		if ( Input.Pressed( "sinvestos" ) )
		{
			// 2. Only proceed if we are looking at this specific object
			if ( IsPlayerLookingAtMe() )
			{
				TogglePopup();
			}
		}
	}

	private bool IsPlayerLookingAtMe()
	{
		// Trace from the player's camera/eyes forward
		var ray = Scene.Camera.ScreenNormalToRay( 0.5f );
		var tr = Scene.Trace.Ray( ray, MaxInteractionDistance )
			.WithTag( "solid" ) // Ensure your computer/collider has the 'solid' tag
			.Run();

		// Check if the trace hit this GameObject or any of its children
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
