using Sandbox;
using Sinvest;
using Sandbox.Services;
using System.Threading.Tasks;
using System.Linq;

namespace Sinvest;

public sealed class EconomyManager : Component
{
    public static EconomyManager Instance { get; private set; }
    
    public double CurrentSharePrice => MarketService.CurrentPrice;

    [Property, ReadOnly] public double CurrentMoney { get; private set; }
    [Property, ReadOnly] public double CurrentShares { get; private set; }

    protected override void OnAwake()
    {
	    if ( !IsProxy ) Instance = this;
    }

    protected override void OnStart()
    {
        if ( IsProxy ) return;

        if ( GameSaveSystem.Instance?.CurrentCharacter != null )
        {
            CurrentMoney = GameSaveSystem.Instance.CurrentCharacter.Money;
            
            string sharesKey = SinvestSession.GetSlotKey( "fundino_shares" );
            var shareStat = Stats.LocalPlayer.Get( sharesKey );
            CurrentShares = shareStat.Value;
        }
    }

    /// <summary>
    /// Debug helper to force specific values from the Debug Manager.
    /// </summary>
    public void DebugSetValues( double targetMoney, double targetShares )
    {
	    var save = GameSaveSystem.Instance;
	    if ( !save.IsValid() || save.CurrentCharacter == null ) return;

	    // Use the SaveSystem as the source of truth for the calculation
	    double currentMoney = save.CurrentCharacter.Money;
	    double currentShares = save.CurrentCharacter.Shares;

	    double moneyDelta = targetMoney - currentMoney;
	    double sharesDelta = targetShares - currentShares;

	    if ( moneyDelta != 0 || sharesDelta != 0 )
	    {
		    Log.Info( $"[DEBUG] Adjusting: Money Δ{moneyDelta}, Shares Δ{sharesDelta}" );
		    CommitTransaction( moneyDelta, sharesDelta );
	    }
    }

    public async void CommitTransaction( double moneyDelta, double sharesDelta )
    {
	    if ( IsProxy ) return;

	    // 1. Update local tracking immediately
	    CurrentMoney += moneyDelta;
	    CurrentShares += sharesDelta;

	    var save = GameSaveSystem.Instance;
	    if ( !save.IsValid() || save.CurrentCharacter == null ) return;

	    // 2. CRITICAL: Update the Session reference for BOTH
	    save.CurrentCharacter.Money = CurrentMoney;
	    save.CurrentCharacter.Shares = CurrentShares;

	    // 3. Handle Persistence
	    if ( save.ShouldUseCloud )
	    {
		    try 
		    {
			    string moneyKey = SinvestSession.GetSlotKey( "money" );
			    string sharesKey = SinvestSession.GetSlotKey( "fundino_shares" );

			    Stats.Increment( moneyKey, moneyDelta );
			    Stats.Increment( sharesKey, sharesDelta );

			    await Stats.FlushAsync();
			    Log.Info( "[ECONOMY] Cloud Sync Success." );
		    }
		    catch ( System.Exception e )
		    {
			    Log.Error( $"[ECONOMY] Cloud Sync Failed: {e.Message}" );
			    // Optional: Fallback to local save if cloud fails
			    save.SetStoredStat( "money", CurrentMoney );
			    save.SetStoredStat( "fundino_shares", CurrentShares );
		    }
	    }
	    else
	    {
		    // 4. LOCAL PERSISTENCE: This ensures Local Mode actually saves to the dictionary
		    // This will also trigger the OnDataChanged event for the UI!
		    save.SetStoredStat( "money", CurrentMoney );
		    save.SetStoredStat( "fundino_shares", CurrentShares );
        
		    Log.Info( "[ECONOMY] Local Transaction Confirmed (Cloud Bypassed)." );
	    }
    }
}
