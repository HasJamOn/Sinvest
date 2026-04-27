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

	    var save = GameSaveSystem.Instance;
	    if ( save?.CurrentCharacter != null )
	    {
		    // ALWAYS use the values already loaded into the character session
		    CurrentMoney = save.CurrentCharacter.Money;
		    CurrentShares = save.CurrentCharacter.Shares;
        
		    Log.Info($"[ECONOMY] Initialized with ${CurrentMoney} and {CurrentShares} shares.");
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

    public void CommitTransaction( double moneyDelta, double sharesDelta )
    {
	    if ( IsProxy ) return;

	    // 1. UPDATE LOCALLY IMMEDIATELY (Player sees this instantly)
	    CurrentMoney += moneyDelta;
	    CurrentShares += sharesDelta;

	    var save = GameSaveSystem.Instance;
	    if ( !save.IsValid() || save.CurrentCharacter == null ) return;

	    save.CurrentCharacter.Money = CurrentMoney;
	    save.CurrentCharacter.Shares = CurrentShares;
    
	    // Notify the UI right now!
	    GameSaveSystem.OnDataChanged?.Invoke();

	    // 2. SYNC IN THE BACKGROUND
	    // We remove 'await' here so the function finishes instantly
	    _ = SyncToCloud( moneyDelta, sharesDelta ); 
    }

    private async Task SyncToCloud( double moneyDelta, double sharesDelta )
    {
	    var save = GameSaveSystem.Instance;
	    if ( !save.ShouldUseCloud ) 
	    {
		    // If local mode, just save to cookies and exit
		    save.SetStoredStat( "money", CurrentMoney );
		    save.SetStoredStat( "fundino_shares", CurrentShares );
		    return;
	    }

	    try 
	    {
		    string moneyKey = SinvestSession.GetSlotKey( "money" );
		    string sharesKey = SinvestSession.GetSlotKey( "fundino_shares" );

		    Stats.Increment( moneyKey, moneyDelta );
		    Stats.Increment( sharesKey, sharesDelta );

		    // We don't call FlushAsync every single flip. 
		    // Steam handles batching automatically if we just set values.
		    // Only flush every few minutes or on quit.
		    Log.Info( "[ECONOMY] Cloud stats updated." );
	    }
	    catch ( System.Exception e )
	    {
		    Log.Error( $"[ECONOMY] Cloud background sync failed: {e.Message}" );
	    }
    }
}
