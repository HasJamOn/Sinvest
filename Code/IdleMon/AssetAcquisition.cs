using Sandbox;
using System;
using System.Linq;

namespace Sinvest;

public sealed class AssetAcquisition : Component
{
	[Property] public double StandardEggCost { get; set; } = 10000;
	// Link this in the inspector to the GameObject that has AssetGenerator
	[Property] public AssetGenerator Generator { get; set; } 
    
	private const double MarketBasePrice = 7126.0;

	public void RollStandardEgg()
	{
		var roster = RosterManager.Instance;

		if ( roster.IdleBucks < StandardEggCost )
		{
			Log.Warning( "Insufficient IdleBucks!" );
			return;
		}

		// Use the Generator component if we have it
		if ( !Generator.IsValid() )
		{
			Log.Error( "AssetAcquisition: Generator property is not assigned!" );
			return;
		}

		roster.ConsumeIdleBucks( StandardEggCost );

		// FIX: Call the actual Generator that reads from bootstrap_ids.txt
		IdleMonData candidate = Generator.RollNewAsset();

		Log.Info( $"Rolled: {candidate.Name} (SteamID: {candidate.SteamId})" );
        
		OpenDraftOverlay( candidate );
	}

	[Property] public bool IsDrafting { get; private set; }
	public IdleMonData CurrentCandidate { get; private set; }

	private void OpenDraftOverlay( IdleMonData candidate )
	{
		CurrentCandidate = candidate;
		IsDrafting = true;
		GameSaveSystem.OnDataChanged?.Invoke(); 
	}

	public void FinalizeDraft( int slotIndex )
	{
		if ( !IsDrafting ) return;
		RosterManager.Instance.ActiveNodes[slotIndex] = CurrentCandidate;
		IsDrafting = false;
		// Reset to a blank state
		CurrentCandidate = default; 
	}
}
