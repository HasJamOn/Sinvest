using Sandbox;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Sinvest;

/// <summary>
/// Handles the "Gacha" mechanics and currency transactions for acquiring new IdleMons.
/// Acts as the bridge between RosterManager (Balance) and AssetGenerator (Creation).
/// </summary>
public sealed class AssetAcquisition : Component
{
    [Property] public double StandardEggCost { get; set; } = 10000;
    
    /// <summary>
    /// Reference to the generation logic. Linked via Inspector.
    /// </summary>
    [Property] public AssetGenerator Generator { get; set; } 
    
    private const double MarketBasePrice = 7126.0;

    /// <summary>
    /// Primary entry point for the "Egg Roll" UI button.
    /// Manages currency validation and triggers the async generation chain.
    /// </summary>
    public void RollStandardEgg()
    {
       var roster = RosterManager.Instance;

       // 1. Validation: Prevent overdraft
       if ( roster.IdleBucks < StandardEggCost )
       {
          Log.Warning( "Insufficient IdleBucks!" );
          return;
       }

       // 2. Component Check: Ensure the generator is wired up in s&box scene
       if ( !Generator.IsValid() )
       {
          Log.Error( "AssetAcquisition: Generator property is not assigned!" );
          return;
       }

       // 3. Execution: Deduct funds immediately (Optimistic UI approach)
       roster.ConsumeIdleBucks( StandardEggCost );

       // 4. Async Chain: Fire-and-forget task to handle metadata fetching without blocking the UI
       _ = HandleRoll();

       async Task HandleRoll()
       {
          // We 'await' the generator because it may be making web requests for Steam metadata
          IdleMonData candidate = await Generator.RollNewAsset();

          Log.Info( $"Rolled: {candidate.Name} (SteamID: {candidate.SteamId})" );
    
          OpenDraftOverlay( candidate );
       }
    }

    [Property, ReadOnly] public bool IsDrafting { get; private set; }
    public IdleMonData CurrentCandidate { get; private set; }

    /// <summary>
    /// Pauses game logic flow to present the player with the "Keep or Discard" decision.
    /// </summary>
    private void OpenDraftOverlay( IdleMonData candidate )
    {
       CurrentCandidate = candidate;
       IsDrafting = true;
       
       // Alert the UI system to refresh and show the draft modal
       GameSaveSystem.OnDataChanged?.Invoke(); 
    }

    /// <summary>
    /// Commits the candidate to a specific slot in the RosterManager.
    /// </summary>
    /// <param name="slotIndex">Target roster slot (0-5)</param>
    public void FinalizeDraft( int slotIndex )
    {
       if ( !IsDrafting ) return;

       // Write the candidate to the global roster
       RosterManager.Instance.ActiveNodes[slotIndex] = CurrentCandidate;
       
       // Close the drafting state
       IsDrafting = false;
       CurrentCandidate = default; 
       
       Log.Info( $"Draft finalized into slot {slotIndex}." );
    }
}
