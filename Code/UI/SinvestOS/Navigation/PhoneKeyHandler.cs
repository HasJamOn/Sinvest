using Sandbox;
using System.Linq;

namespace Sinvest;

public sealed class PhoneKeyHandler : Component
{
	[Property] public GameObject SinvestOSPrefab { get; set; }
    
	// We keep a reference to our spawned phone instance 
	// so we don't accidentally close the PC instance
	private GameObject _spawnedPhoneOS;

	protected override void OnUpdate()
	{
		if ( IsProxy ) return;

		// 1. Only check input if the local player actually exists
		if ( Input.Pressed( "phone" ) )
		{
			TryTogglePhone();
		}
	}

	private void TryTogglePhone()
	{
		// 2. Safety: Is the InventoryManager ready?
		if ( InventoryManager.Instance == null )
		{
			Log.Warning( "PhoneKeyHandler: InventoryManager.Instance is not ready yet." );
			return;
		}

		// 3. Logic: Does the player have the phone?
		if ( !InventoryManager.Instance.HasPhone )
		{
			Log.Info( "Phone: You don't have a device to open." );
			return;
		}

		// 4. Toggle: If OS is open, close it.
		if ( SinvestOS.Instance.IsValid() )
		{
			SinvestOS.Instance.CloseAndDestroy();
			return;
		}

		// 5. Spawn: Ensure the prefab is assigned in the Inspector
		if ( SinvestOSPrefab == null )
		{
			Log.Error( "PhoneKeyHandler: SinvestOSPrefab is NULL! Assign it in the Inspector." );
			return;
		}

		var spawned = SinvestOSPrefab.Clone();
		spawned.WorldPosition = new Vector3( 5000, 0, 0 );

		var hud = spawned.Components.Get<SinvestOS>( FindMode.EverythingInDescendants );
		if ( hud != null )
		{
			hud.Open();
			Sound.Play( "sounds/ui/sinvestos_boot.sound" );
		}
	}
}
