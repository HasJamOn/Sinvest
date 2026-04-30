using Sandbox;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Sinvest;

public sealed class PlayerStartupManager : Component
{
    private EconomyManager _economy => Components.Get<EconomyManager>( FindMode.EverythingInSelfAndAncestors );

    protected override void OnStart()
    {
       if ( IsProxy ) return;

       // Initialize the character data and apply modifiers
       _ = InitializeCharacterAsync();
    }

    private async Task InitializeCharacterAsync()
    {
       // 1. POLLING: Wait for the save system singleton and character session to be ready
       int attempts = 0;
       while ( (GameSaveSystem.Instance == null || GameSaveSystem.Instance.CurrentCharacter == null) && attempts < 30 )
       {
          await Task.Delay( 100 );
          attempts++;
       }

       var saveSystem = GameSaveSystem.Instance;

       if ( !saveSystem.IsValid() || saveSystem.CurrentCharacter == null )
       {
          Log.Error( "[STARTUP] Timed out waiting for SaveSystem. Character initialization failed." );
          return;
       }

       RunStartupLogic( saveSystem );
    }

    private void RunStartupLogic( GameSaveSystem saveSystem )
    {
       var session = saveSystem.CurrentCharacter;

       // 2. CHECK: Only apply modifiers if this is a fresh character (Balance = 0)
       // This prevents re-injecting the $1000 every time the scene is reloaded.
       if ( session.Money == 0 && session.Shares == 0 )
       {
          ApplyModifiers( session.Modifiers );
       }
       else
       {
          Log.Info( $"[STARTUP] Active character {session.Name} already has history. Skipping modifier injection." );
       }
    }

    private void ApplyModifiers( StartingModifiers mods )
    {
        var save = GameSaveSystem.Instance;

        if ( !_economy.IsValid() )
        {
           Log.Error( "[STARTUP] EconomyManager not found on Player Prefab!" );
           return;
        }

        Log.Info( $"[STARTUP] Initializing modifiers for Slot {save.ActiveSlot}..." );

        // --- DEFAULT STARTER KIT ---
        if ( !mods.HasFlag( StartingModifiers.Destitute ) )
        {
           save.CommitTransaction( "IN", 1000, "Starter Kit: Cash" );
           save.CurrentCharacter.Shares += 1; 
           Log.Info( "Standard Issue: $1000 and 1 Share granted." );
        }
        else
        {
           Log.Warning( "Modifier Active: Destitute. Starting with $0 and 0 shares." );
        }

        // --- CONDITIONAL MODIFIERS ---
        if ( mods.HasFlag( StartingModifiers.Nepokid ) )
        {
           save.CurrentCharacter.Shares += 500000; 
           Log.Info( "Modifier Applied: Nepokid (+ 500k shares)" );
        }

        if ( mods.HasFlag( StartingModifiers.Phoney ) )
        {
           GrantItem( "item_phone" );
        }

        if ( mods.HasFlag( StartingModifiers.Internity ) )
        {
           UnlockAllJobs();
        }

        if ( mods.HasFlag( StartingModifiers.DevMode ) )
        {
           save.CommitTransaction( "IN", 100000000, "DevMode Injection" );
        }

        // 3. FINALIZE: Trigger a data change event so UI updates immediately
        save.NotifyDataChanged();
   
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
       Log.Info( "Modifier Applied: Internity (All Jobs Unlocked)" );
    }
}
