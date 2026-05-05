using Sandbox;
using System.Linq;

namespace Sinvest;

/// <summary>
/// Manages the player's physical inventory and utility unlocks (e.g., Phone).
/// This class handles the logic for persistent item states and provides 
/// debug overrides for development testing.
/// </summary>
public sealed class PlayerInventoryManager : Component
{
    public static PlayerInventoryManager Instance { get; private set; }

    private bool _hasPhone;
    
    /// <summary>
    /// Returns true if the player owns a phone. 
    /// Includes a debug override to bypass progression requirements during testing.
    /// </summary>
    [Property] public bool HasPhone 
    { 
       get 
       {
          // Check if a DebugManager exists in the scene and has the 'ForceHasPhone' toggle enabled.
          var dbg = Scene.GetAllComponents<SinvestDebugManager>().FirstOrDefault();
          if ( dbg != null && dbg.ForceHasPhone ) return true;
            
          return _hasPhone;
       }
       private set => _hasPhone = value;
    }

    protected override void OnAwake()
    {
       Instance = this;
    }

    protected override void OnStart()
    {
       if ( IsProxy ) return;

       // Reconstruct the player's inventory state from the session data 
       // replayed by the GameSaveSystem during the loading phase.
       RestoreFromSession();
    }

    /// <summary>
    /// Syncs the inventory with the current character's unlocked items hashset.
    /// </summary>
    private void RestoreFromSession()
    {
       var session = GameSaveSystem.Instance?.CurrentCharacter;
       if ( session != null && session.UnlockedItems.Contains( "item_phone" ) )
       {
          SetPhoneEnabled( true );
       }
    }

    /// <summary>
    /// Unlocks the phone utility, updates the live state, and records 
    /// the transaction in the permanent ledger.
    /// </summary>
    public void UnlockPhone()
    {
       // Safety check: Avoid redundant ledger writes if the item is already active in RAM.
       if ( _hasPhone ) return;

       SetPhoneEnabled( true );
   
       // Record the 'ITEM' tag in the authoritative ledger for future sessions.
       GameSaveSystem.Instance?.CommitItemTransaction( "item_phone" );
    }

    /// <summary>
    /// Internal helper to update the backing field and provide logging for state changes.
    /// </summary>
    private void SetPhoneEnabled( bool state )
    {
       _hasPhone = state;
       Log.Info( $"Inventory: Phone status set to {state}" );
    }
}
