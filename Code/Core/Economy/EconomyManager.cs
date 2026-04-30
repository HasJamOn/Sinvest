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
    
	    var result = GameSaveSystem.Instance.CommitMoneyTransaction( 
		    "OUT", 
		    totalCost, 
		    $"Bought {amount} Shares" 
	    );

	    if ( result == TransactionResult.Success )
	    {
		    // Use the transaction method instead of direct +=
		    GameSaveSystem.Instance.CommitShareTransaction( amount, "Market Purchase" );
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

	    var result = GameSaveSystem.Instance.CommitMoneyTransaction( 
		    "IN", 
		    totalGain, 
		    $"Sold {amount} Shares @ {CurrentSharePrice}" 
	    );

	    if ( result == TransactionResult.Success )
	    {
		    // Use the transaction method instead of direct -=
		    GameSaveSystem.Instance.CommitShareTransaction( -amount, "Market Sale" );
	    }

	    return result;
    }

    /// <summary>
    /// Records income from jobs (Labor).
    /// </summary>
    public void AddLaborIncome( double amount, string jobName )
    {
        GameSaveSystem.Instance.CommitMoneyTransaction( "IN", amount, $"Job: {jobName}" );
    }
    
    /// <summary>
    /// Debug override to force a specific balance.
    /// Calculated as a ledger correction to maintain authoritative integrity.
    /// </summary>
    public void DebugSetValues( double targetMoney, double targetShares )
    {
	    var save = GameSaveSystem.Instance;
	    if ( !save.IsValid() || save.CurrentCharacter == null ) return;

	    // 1. Correct Money via Ledger
	    // We calculate the delta so the Inflow/Outflow sum matches the target
	    double currentMoney = save.CurrentCharacter.Money;
	    double moneyDiff = targetMoney - currentMoney;

	    if ( moneyDiff > 0 )
	    {
		    save.CommitMoneyTransaction( "IN", moneyDiff, "DEBUG_OVERRIDE" );
	    }
	    else if ( moneyDiff < 0 )
	    {
		    // Use absolute value for OUT transaction
		    save.CommitMoneyTransaction( "OUT", Math.Abs( moneyDiff ), "DEBUG_OVERRIDE" );
	    }

	    // 2. Correct Shares
	    // Note: If you want shares to be ledger-authoritative, 
	    // you'd add SHARE_BUY/SELL logic here. For now, we set the session value.
	    save.CurrentCharacter.Shares = targetShares;
        
	    Log.Info( $"[ECONOMY] Debug Override: Adjusted Money by {moneyDiff:+0.##;-0.##}. Shares set to {targetShares}." );
        
	    // Notify UI and Achievements
	    save.NotifyDataChanged();
    }
}
