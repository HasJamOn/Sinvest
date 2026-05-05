using Sandbox;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Sinvest;

/// <summary>
/// Orchestrates the initial synchronization between the scene, the GameSaveSystem, 
/// and the EconomyManager. It ensures the "Starter Kit" is only injected once per save.
/// </summary>
public sealed class PlayerStartupManager : Component
{
    private EconomyManager _economy => Components.Get<EconomyManager>( FindMode.EverythingInSelfAndAncestors ) 
                                    ?? Scene.GetAllComponents<EconomyManager>().FirstOrDefault();

    protected override void OnStart()
    {
       if ( IsProxy ) return;

       // Initialization is asynchronous to account for frame-delays during scene loading.
       _ = InitializeCharacterAsync();
    }

    private async Task InitializeCharacterAsync()
    {
       Log.Info( "[STARTUP] Beginning character sync sequence..." );

       int attempts = 0;
       // Polling loop: Wait for the singleton Instance and the Replay Engine to finish loading data.
       while ( (GameSaveSystem.Instance == null || GameSaveSystem.Instance.CurrentCharacter == null) && attempts < 50 )
       {
          await Task.Delay( 100 );
          attempts++;
       }

       var saveSystem = GameSaveSystem.Instance;

       if ( !saveSystem.IsValid() || saveSystem.CurrentCharacter == null )
       {
          Log.Error( "[STARTUP] Timed out waiting for SaveSystem. UI may stay stuck." );
          saveSystem?.NotifyDataChanged();
          return;
       }

       attempts = 0;
       while ( _economy == null && attempts < 10 )
       {
          await Task.Delay( 100 );
          attempts++;
       }

       RunStartupLogic( saveSystem );
    }

    /// <summary>
    /// Checks if the ledger requires the initial injection of cash/items.
    /// Uses the "Starter Kit" string as a sentinel value in the plaintext file.
    /// </summary>
    private void RunStartupLogic( GameSaveSystem saveSystem )
    {
        var path = $"slot_{saveSystem.ActiveSlot}.txt";
        bool fileExists = FileSystem.Data.FileExists( path );
        string content = fileExists ? FileSystem.Data.ReadAllText( path ) : "";

        // Only apply modifiers if the file is brand new or lacks the starter kit entry.
        if ( !fileExists || !content.Contains( "Starter Kit" ) )
        {
           Log.Info( "[STARTUP] Ledger needs initialization. Applying modifiers..." );
        
           if ( !fileExists )
           {
              // Create the initial META header before appending transactions.
              _ = saveSystem.SaveActiveSlotAsync();
           }

           ApplyModifiers( saveSystem.CurrentCharacter.Modifiers );
        }
        else
        {
           Log.Info( $"[STARTUP] Ledger for {saveSystem.CurrentCharacter.Name} verified. Skipping kit injection." );
           saveSystem.NotifyDataChanged();
        }
    }

    /// <summary>
    /// Logic for processing StartingModifiers. 
    /// Handles the "Nepokid" share injection and the "Destitute" zero-balance restriction.
    /// </summary>
    private void ApplyModifiers( StartingModifiers mods )
    {
        var save = GameSaveSystem.Instance;

        if ( !_economy.IsValid() )
        {
           Log.Error( "[STARTUP] EconomyManager MISSING. Cannot apply starting funds!" );
           save.NotifyDataChanged();
           return;
        }

        Log.Info( $"[STARTUP] Injecting funds for Slot {save.ActiveSlot}..." );

        // Standard Issue: Given to all players unless the 'Destitute' flag is set.
        if ( !mods.HasFlag( StartingModifiers.Destitute ) )
        {
           save.CommitMoneyTransaction( "IN", 1000, "Starter Kit: Cash" );
           save.CommitShareTransaction( 1, "Starter Kit: Share" ); 
        }

        // Modifier: Nepokid. Injects high-value shares into the S&P 500 equivalent.
        if ( mods.HasFlag( StartingModifiers.Nepokid ) )
        {
           save.CommitShareTransaction( 500000, "Modifier: Nepokid" );
        }

        // Modifier: Phoney. Immediately unlocks the mobile interface.
        if ( mods.HasFlag( StartingModifiers.Phoney ) ) GrantItem( "item_phone" );

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
