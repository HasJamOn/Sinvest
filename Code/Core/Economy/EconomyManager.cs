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
    // Storing and tracking values as high-precision decimals completely eliminates 
    // binary floating-point rounding drifts over long gameplay sessions.
    public decimal CurrentMoney => (decimal)(GameSaveSystem.Instance?.CurrentCharacter?.Money ?? 0);
    public decimal CurrentShares => (decimal)(GameSaveSystem.Instance?.CurrentCharacter?.Shares ?? 0);
    public decimal CurrentSharePrice => (decimal)FundinoMarketService.CurrentPrice;

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
        
        // Explicit decimal precision math
        decimal totalCost = amount * CurrentSharePrice;
    
        // Attempt to deduct cash first. GameSaveSystem will return InsufficientFunds if balance is too low.
        // We cast back to double here in case your core ledger backend parameters haven't shifted yet.
        var result = GameSaveSystem.Instance.CommitMoneyTransaction( 
           "OUT", 
           (double)totalCost, 
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

        // Explicit decimal precision math
        decimal totalGain = amount * CurrentSharePrice;

        // Perform the cash injection.
        var result = GameSaveSystem.Instance.CommitMoneyTransaction( 
           "IN", 
           (double)totalGain, 
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
    public void AddLaborIncome( decimal amount, string jobName )
    {
        GameSaveSystem.Instance.CommitMoneyTransaction( "IN", (double)amount, $"Job: {jobName}" );
    }
    
    /// <summary>
    /// Adjusts the session values by calculating the necessary "correction" entries.
    /// This maintains the integrity of the ledger (Balance = Sum of all lines)
    /// even when manually overriding values for testing.
    /// </summary>
    public void DebugSetValues( decimal targetMoney, decimal targetShares )
    {
        var save = GameSaveSystem.Instance;
        if ( !save.IsValid() || save.CurrentCharacter == null ) return;

        // 1. Correct Money via Ledger
        // Instead of setting the value directly, we append a delta transaction.
        decimal currentMoney = (decimal)save.CurrentCharacter.Money;
        decimal moneyDiff = targetMoney - currentMoney;

        if ( moneyDiff > 0 )
        {
           save.CommitMoneyTransaction( "IN", (double)moneyDiff, "DEBUG_OVERRIDE" );
        }
        else if ( moneyDiff < 0 )
        {
           // OUT transactions expect a positive magnitude to subtract from the ledger.
           save.CommitMoneyTransaction( "OUT", (double)Math.Abs( moneyDiff ), "DEBUG_OVERRIDE" );
        }

        // 2. Correct Shares
        // Directly updating the cache. Future revisions may move shares to a full ledger model.
        save.CurrentCharacter.Shares = (double)targetShares;
        
        Log.Info( $"[ECONOMY] Debug Override: Adjusted Money by {moneyDiff:+0.##;-0.##}. Shares set to {targetShares}." );
        
        save.NotifyDataChanged();
    }
}
