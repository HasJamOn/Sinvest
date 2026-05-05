using Sandbox;
using System.Linq;

namespace Sinvest;

/// <summary>
/// Manages the lifecycle of the SinvestOS UI when triggered by the "phone" input.
/// Logic: Verifies ownership via Inventory before cloning the OS Prefab.
/// </summary>
public sealed class PhoneKeyHandler : Component
{
    [Property] public GameObject SinvestOSPrefab { get; set; }
    
    /// <summary>
    /// Reference to the spawned instance. 
    /// Note: SinvestOS.Instance (Singleton) handles the internal UI state.
    /// </summary>
    private GameObject _spawnedPhoneOS;

    protected override void OnUpdate()
    {
       if ( IsProxy ) return;

       // Input Logic: Maps to the "phone" action defined in the Project Settings.
       if ( Input.Pressed( "phone" ) )
       {
          TryTogglePhone();
       }
    }

    private void TryTogglePhone()
    {
       // 1. Singleton Validation: Ensure the manager responsible for item state is initialized.
       if ( PlayerInventoryManager.Instance == null )
       {
          Log.Warning( "PhoneKeyHandler: InventoryManager.Instance is not ready yet." );
          return;
       }

       // 2. Progression Check: Persistence check against the current player session.
       if ( !PlayerInventoryManager.Instance.HasPhone )
       {
          Log.Info( "Phone: You don't have a device to open." );
          return;
       }

       // 3. Toggle Logic: If the Singleton is valid, the OS is already active in the scene.
       // Calling CloseAndDestroy() handles the Roster saving and object cleanup.
       if ( SinvestOS.Instance.IsValid() )
       {
          SinvestOS.Instance.CloseAndDestroy();
          return;
       }

       // 4. Prefab Safety: Prevents null reference if the Inspector assignment is missing.
       if ( SinvestOSPrefab == null )
       {
          Log.Error( "PhoneKeyHandler: SinvestOSPrefab is NULL! Assign it in the Inspector." );
          return;
       }

       // 5. Instantiation: Clones the UI hierarchy. 
       // Positioned far from the world origin (5000, 0, 0) to isolate the 3D Render ScenePanels.
       var spawned = SinvestOSPrefab.Clone();
       spawned.WorldPosition = new Vector3( 5000, 0, 0 );

       // 6. Initialization: Fetches the OS component to trigger the boot sequence and audio.
       var hud = spawned.Components.Get<SinvestOS>( FindMode.EverythingInDescendants );
       if ( hud != null )
       {
          hud.Open();
          Sound.Play( "sounds/ui/sinvestos_boot.sound" );
       }
    }
}
