using Sandbox;
using System;

namespace Sinvest;

/// <summary>
/// The central gameplay interface for the Sinvest economy. 
/// It coordinates between the MarketServiceSystem and the GameSaveSystem,
/// ensuring that all financial operations are logically valid before being recorded.
/// </summary>
public sealed class EconomyManager : Component
{
    public static EconomyManager Instance { get; private set; }

    /* --- Calculated Views --- */
    // These properties act as bridges. The EconomyManager does not store state;
    // it retrieves the replayed values from the current session.
    public double CurrentMoney => GameSaveSystem.Instance?.CurrentCharacter?.Money ?? 0;
    public double CurrentShares => GameSaveSystem.Instance?.CurrentCharacter?.Shares ?? 0;
    public double CurrentSharePrice => FundinoMarketService.CurrentPrice;

    protected override void OnAwake()
    {
        // Ensure only the local host/client is driving their own economy manager.
        if ( !IsProxy ) Instance = this;
    }

    /// <summary>
    /// Checks liquid funds against the current market price and records a 
    /// dual-transaction (Money OUT, Shares IN) if valid.
    /// </summary>
    public TransactionResult BuyShares( int amount )
    {
        if ( amount <= 0 ) return TransactionResult.SystemError;
        double totalCost = amount * CurrentSharePrice;
    
        // Attempt to deduct cash first. GameSaveSystem will return InsufficientFunds if balance is too low.
        var result = GameSaveSystem.Instance.CommitMoneyTransaction( 
           "OUT", 
           totalCost, 
           $"Bought {amount} Shares" 
        );

        if ( result == TransactionResult.Success )
        {
           // Logically link the share increase to the money deduction.
           GameSaveSystem.Instance.CommitShareTransaction( amount, "Market Purchase" );
        }

        return result;
    }

    /// <summary>
    /// Validates equity ownership and converts shares back into liquid cash 
    /// based on the current live market price.
    /// </summary>
    public TransactionResult SellShares( int amount )
    {
        var character = GameSaveSystem.Instance?.CurrentCharacter;
        if ( character == null || character.Shares < amount ) return TransactionResult.InsufficientFunds;

        double totalGain = amount * CurrentSharePrice;

        // Perform the cash injection.
        var result = GameSaveSystem.Instance.CommitMoneyTransaction( 
           "IN", 
           totalGain, 
           $"Sold {amount} Shares @ {CurrentSharePrice}" 
        );

        if ( result == TransactionResult.Success )
        {
           // Record the negative share delta in the ledger.
           GameSaveSystem.Instance.CommitShareTransaction( -amount, "Market Sale" );
        }

        return result;
    }

    /// <summary>
    /// Direct entry point for Labor rewards. 
    /// Converts time-based job completion into a ledger-backed inflow.
    /// </summary>
    public void AddLaborIncome( double amount, string jobName )
    {
        GameSaveSystem.Instance.CommitMoneyTransaction( "IN", amount, $"Job: {jobName}" );
    }
    
    /// <summary>
    /// Adjusts the session values by calculating the necessary "correction" entries.
    /// This maintains the integrity of the ledger (Balance = Sum of all lines)
    /// even when manually overriding values for testing.
    /// </summary>
    public void DebugSetValues( double targetMoney, double targetShares )
    {
        var save = GameSaveSystem.Instance;
        if ( !save.IsValid() || save.CurrentCharacter == null ) return;

        // 1. Correct Money via Ledger
        // Instead of setting the value directly, we append a delta transaction.
        double currentMoney = save.CurrentCharacter.Money;
        double moneyDiff = targetMoney - currentMoney;

        if ( moneyDiff > 0 )
        {
           save.CommitMoneyTransaction( "IN", moneyDiff, "DEBUG_OVERRIDE" );
        }
        else if ( moneyDiff < 0 )
        {
           // OUT transactions expect a positive magnitude to subtract from the ledger.
           save.CommitMoneyTransaction( "OUT", Math.Abs( moneyDiff ), "DEBUG_OVERRIDE" );
        }

        // 2. Correct Shares
        // Directly updating the cache. Future revisions may move shares to a full ledger model.
        save.CurrentCharacter.Shares = targetShares;
        
        Log.Info( $"[ECONOMY] Debug Override: Adjusted Money by {moneyDiff:+0.##;-0.##}. Shares set to {targetShares}." );
        
        save.NotifyDataChanged();
    }
}
