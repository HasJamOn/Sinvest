using Sandbox;
using Sandbox.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Sinvest;

public sealed class PlayerStartupManager : Component
{
    // Local sibling reference
    private EconomyManager _economy => Components.Get<EconomyManager>( FindMode.EverythingInSelfAndAncestors );

    protected override void OnStart()
    {
       if ( IsProxy ) return;

       // Fire and forget the initialization task
       _ = InitializeCharacterAsync();
    }

    private async Task InitializeCharacterAsync()
    {
       // 1. POLLING: Wait for the singleton to settle in the new scene
       // We'll check every 100ms for up to 3 seconds.
       int attempts = 0;
       while ( (GameSaveSystem.Instance == null || GameSaveSystem.Instance.CurrentCharacter == null) && attempts < 30 )
       {
          await Task.Delay( 100 );
          attempts++;
       }

       var saveSystem = GameSaveSystem.Instance;

       // 2. SAFETY: If it's still not here after 3 seconds, something is actually broken
       if ( !saveSystem.IsValid() || saveSystem.CurrentCharacter == null )
       {
          Log.Error( "[STARTUP] Timed out waiting for SaveSystem. Character modifiers failed to apply." );
          return;
       }

       // 3. PROCEED: Everything is ready
       RunStartupLogic( saveSystem );
    }

    private void RunStartupLogic( GameSaveSystem saveSystem )
    {
       var session = saveSystem.CurrentCharacter;
       string claimKey = SinvestSession.GetSlotKey( "claimed_startup" );
       
       bool hasClaimed = false;
       if ( saveSystem.ShouldUseCloud )
       {
          try 
          {
             hasClaimed = Stats.LocalPlayer.Get( claimKey ).Value > 0;
          }
          catch { hasClaimed = false; }
       }

       if ( !hasClaimed )
       {
          ApplyModifiers( session.Modifiers, claimKey );
       }
       else
       {
          Log.Info( $"[STARTUP] Modifiers already claimed for {session.Name}. Skipping injection." );
       }
    }

    private void ApplyModifiers( StartingModifiers mods, string claimKey )
    {
	    if ( !_economy.IsValid() )
	    {
		    Log.Error( "[STARTUP] EconomyManager not found on Player Prefab!" );
		    return;
	    }

	    Log.Info( $"[STARTUP] Initializing modifiers for {SinvestSession.ActiveSlot}..." );

	    // --- DEFAULT STARTER KIT ---
	    // Only grant if the player does NOT have the Destitute modifier
	    if ( !mods.HasFlag( StartingModifiers.Destitute ) )
	    {
		    _economy.CommitTransaction( 1000, 1 ); 
		    Log.Info( "Standard Issue: $1000 and 1 Share granted." );
	    }
	    else
	    {
		    Log.Warning( "Modifier Active: Destitute. Starting with $0 and 0 shares. Good luck." );
	    }

	    // --- CONDITIONAL MODIFIERS ---
   
	    // Nepokid: Starts with $500k in shares 
	    // (Note: If a player is both Destitute AND Nepokid, they'd get 500k but not the base 1000)
	    if ( mods.HasFlag( StartingModifiers.Nepokid ) )
	    {
		    _economy.CommitTransaction( 0, 500000 ); 
		    Log.Info( "Modifier Applied: Nepokid (+ $500k shares)" );
	    }

	    // 2. Phoney: Start with a Phone
	    if ( mods.HasFlag( StartingModifiers.Phoney ) )
	    {
		    GrantItem( "item_phone" );
	    }

	    // 3. Internity: Unlock all jobs
	    if ( mods.HasFlag( StartingModifiers.Internity ) )
	    {
		    UnlockAllJobs();
	    }

	    // 4. DevMode: For testing huge amounts
	    if ( mods.HasFlag( StartingModifiers.DevMode ) )
	    {
		    _economy.CommitTransaction( 100000000, 0 );
	    }

	    // --- FINALIZE CLAIM ---
	    // We mark this slot as "Initialized" so they can't get the $1000 again on reload.
	    if ( GameSaveSystem.Instance.ShouldUseCloud )
	    {
		    Stats.SetValue( claimKey, 1 );
		    _ = Stats.FlushAsync();
	    }
   
	    Log.Info( "[STARTUP] Character initialization complete." );
    }

    private void GrantItem( string itemTag )
    {
       if ( itemTag == "item_phone" )
       {
          var inv = Components.Get<PlayerInventoryManager>( FindMode.EverythingInSelfAndAncestors );
          if ( inv.IsValid() )
          {
             inv.UnlockPhone();
             Log.Info( "Modifier Applied: Phoney (Phone Unlocked)" );
          }
       }
    }

    private void UnlockAllJobs()
    {
       Log.Info( "Modifier Applied: Internity (Unlocked)" );
    }
}
