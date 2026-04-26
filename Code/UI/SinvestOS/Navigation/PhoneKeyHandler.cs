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

		if ( Input.Pressed( "phone" ) )
		{
			TryTogglePhone();
		}
	}

	private void TryTogglePhone()
	{
		// 1. Ownership Check
		if ( InventoryManager.Instance == null || !InventoryManager.Instance.HasPhone )
		{
			Log.Info( "Check failed: No phone owned." );
			return;
		}

		// 2. If the OS is already open (either on PC or Phone), close it
		if ( SinvestOS.Instance != null )
		{
			// If it's already open, we just shut it down via the instance
			SinvestOS.Instance.CloseAndDestroy();
			_spawnedPhoneOS = null;
			return;
		}

		// 3. Spawning Logic (Based on your UIPopupTrigger reference)
		if ( SinvestOSPrefab == null )
		{
			Log.Error( "PhoneKeyHandler: SinvestOSPrefab is not assigned in the Inspector!" );
			return;
		}

		// Clone and Setup exactly like your stationary PC does
		_spawnedPhoneOS = SinvestOSPrefab.Clone();
		_spawnedPhoneOS.WorldPosition = new Vector3( 5000, 0, 0 ); // Move off-map per your design

		var hud = _spawnedPhoneOS.Components.Get<SinvestOS>( FindMode.EverythingInDescendants );
		if ( hud != null )
		{
			hud.Open();
			// Play your boot sound if you want
			Sound.Play( "sounds/ui/sinvestos_boot.sound" );
		}
	}
}
