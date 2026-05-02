using Sandbox;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Sinvest;

public sealed class PlayerStartupManager : Component
{
    // Changed to property with fallback to ensure we find it across the scene if needed
    private EconomyManager _economy => Components.Get<EconomyManager>( FindMode.EverythingInSelfAndAncestors ) 
                                    ?? Scene.GetAllComponents<EconomyManager>().FirstOrDefault();

    protected override void OnStart()
    {
       if ( IsProxy ) return;

       // Kick off the initialization
       _ = InitializeCharacterAsync();
    }

    private async Task InitializeCharacterAsync()
    {
       Log.Info( "[STARTUP] Beginning character sync sequence..." );

       int attempts = 0;
       // Polling loop for GameSaveSystem and CurrentCharacter
       while ( (GameSaveSystem.Instance == null || GameSaveSystem.Instance.CurrentCharacter == null) && attempts < 50 )
       {
          await Task.Delay( 100 );
          attempts++;
       }

       var saveSystem = GameSaveSystem.Instance;

       if ( !saveSystem.IsValid() || saveSystem.CurrentCharacter == null )
       {
          Log.Error( "[STARTUP] Timed out waiting for SaveSystem. UI may stay stuck." );
          // Force a notification to at least let the UI try to resolve the "Syncing" state
          saveSystem?.NotifyDataChanged();
          return;
       }

       // Ensure the EconomyManager is ready. Scene transitions can be frame-delayed.
       attempts = 0;
       while ( _economy == null && attempts < 10 )
       {
          await Task.Delay( 100 );
          attempts++;
       }

       RunStartupLogic( saveSystem );
    }

    private void RunStartupLogic( GameSaveSystem saveSystem )
    {
        var path = $"slot_{saveSystem.ActiveSlot}.txt";
        bool fileExists = FileSystem.Data.FileExists( path );
        string content = fileExists ? FileSystem.Data.ReadAllText( path ) : "";

        // If file is missing OR brand-new (only has META tag, no kit transactions)
        if ( !fileExists || !content.Contains( "Starter Kit" ) )
        {
           Log.Info( "[STARTUP] Ledger needs initialization. Applying modifiers..." );
        
           // If the file didn't exist, SaveActiveSlotAsync creates the META header
           if ( !fileExists )
           {
              // We don't await this as it's a simple write, but we want it done before transactions
              _ = saveSystem.SaveActiveSlotAsync();
           }

           ApplyModifiers( saveSystem.CurrentCharacter.Modifiers );
        }
        else
        {
           Log.Info( $"[STARTUP] Ledger for {saveSystem.CurrentCharacter.Name} verified. Skipping kit injection." );
           // Even if we skip injection, we notify data changed to clear any UI loading states
           saveSystem.NotifyDataChanged();
        }
    }

    private void ApplyModifiers( StartingModifiers mods )
    {
        var save = GameSaveSystem.Instance;

        if ( !_economy.IsValid() )
        {
           Log.Error( "[STARTUP] EconomyManager MISSING. Cannot apply starting funds!" );
           // Critical failure - notify UI anyway so it doesn't hang
           save.NotifyDataChanged();
           return;
        }

        Log.Info( $"[STARTUP] Injecting funds for Slot {save.ActiveSlot}..." );

        // Standard Issue: Applied unless Destitute
        if ( !mods.HasFlag( StartingModifiers.Destitute ) )
        {
           save.CommitMoneyTransaction( "IN", 1000, "Starter Kit: Cash" );
           save.CommitShareTransaction( 1, "Starter Kit: Share" ); 
        }

        // Conditional Modifier: Nepokid
        if ( mods.HasFlag( StartingModifiers.Nepokid ) )
        {
           save.CommitShareTransaction( 500000, "Modifier: Nepokid" );
        }

        // Handle other items...
        if ( mods.HasFlag( StartingModifiers.Phoney ) ) GrantItem( "item_phone" );

        // Finalize and broadcast
        save.NotifyDataChanged();
        Log.Info( "[STARTUP] Sync sequence complete." );
    }

    private void GrantItem( string itemTag )
    {
       if ( itemTag == "item_phone" )
       {
          var inv = Components.Get<PlayerInventoryManager>( FindMode.EverythingInSelfAndAncestors );
          if ( inv.IsValid() )
          {
             inv.UnlockPhone();
          }
       }
    }
}
