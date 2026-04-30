using Sandbox;
using System;

namespace Sinvest;

public sealed class EconomyManager : Component
{
    public static EconomyManager Instance { get; private set; }

    // Logic-driven getters: The EconomyManager no longer "owns" the data
    public double CurrentMoney => GameSaveSystem.Instance?.CurrentCharacter?.Money ?? 0;
    public double CurrentShares => GameSaveSystem.Instance?.CurrentCharacter?.Shares ?? 0;
    public double CurrentSharePrice => FundinoMarketService.CurrentPrice;

    protected override void OnAwake()
    {
        if ( !IsProxy ) Instance = this;
    }

    /// <summary>
    /// Processes a purchase of Fundino shares using the Ledger.
    /// </summary>
    public TransactionResult BuyShares( int amount )
    {
        if ( amount <= 0 ) return TransactionResult.SystemError;

        double totalCost = amount * CurrentSharePrice;

        // Commit the outflow to the ledger
        var result = GameSaveSystem.Instance.CommitTransaction( 
            "OUT", 
            totalCost, 
            $"Bought {amount} Shares @ {CurrentSharePrice}" 
        );

        if ( result == TransactionResult.Success )
        {
            // Update the state in the session (Shares aren't currently in your Ledger logic)
            GameSaveSystem.Instance.CurrentCharacter.Shares += amount;
            Log.Info( $"[ECONOMY] Purchased {amount} shares for ${totalCost}" );
        }

        return result;
    }

    /// <summary>
    /// Sells Fundino shares and records the inflow in the ledger.
    /// </summary>
    public TransactionResult SellShares( int amount )
    {
        var character = GameSaveSystem.Instance?.CurrentCharacter;
        if ( character == null || character.Shares < amount ) return TransactionResult.InsufficientFunds;

        double totalGain = amount * CurrentSharePrice;

        var result = GameSaveSystem.Instance.CommitTransaction( 
            "IN", 
            totalGain, 
            $"Sold {amount} Shares @ {CurrentSharePrice}" 
        );

        if ( result == TransactionResult.Success )
        {
            character.Shares -= amount;
            Log.Info( $"[ECONOMY] Sold {amount} shares for ${totalGain}" );
        }

        return result;
    }

    /// <summary>
    /// Records income from jobs (Labor).
    /// </summary>
    public void AddLaborIncome( double amount, string jobName )
    {
        GameSaveSystem.Instance.CommitTransaction( "IN", amount, $"Job: {jobName}" );
    }
}
