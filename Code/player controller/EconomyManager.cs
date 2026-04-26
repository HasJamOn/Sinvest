using Sandbox;
using Sinvest;
using Sandbox.Services;
using System.Threading.Tasks;

namespace Sinvest;

public sealed class EconomyManager : Component
{
    public static EconomyManager Instance { get; private set; }
    
    public double CurrentSharePrice => MarketService.CurrentPrice;

    [Property, ReadOnly] public double CurrentMoney { get; private set; }
    [Property, ReadOnly] public double CurrentShares { get; private set; }

    protected override void OnAwake()
    {
        Instance = this;
    }

    protected override void OnStart()
    {
        if ( IsProxy ) return;

        // 1. First, prioritize the data passed from the GameSaveSystem (Menu Selection)
        if ( GameSaveSystem.Instance?.CurrentCharacter != null )
        {
            CurrentMoney = GameSaveSystem.Instance.CurrentCharacter.Money;
            
            // 2. Since the CharacterSession only had Name/Money/Mods, 
            // let's fetch the Shares directly from the Cloud for this slot.
            string sharesKey = SinvestSession.GetSlotKey( "fundino_shares" );
            
            // FIX: Using the correct property/method from your source snippet
            var shareStat = Stats.LocalPlayer.Get( sharesKey );
            CurrentShares = shareStat.Value;
        }
    }

    /// <summary>
    /// Handles slot-aware communication with s&box cloud services.
    /// </summary>
    public async void CommitTransaction( double moneyDelta, double sharesDelta )
    {
        if ( IsProxy ) return;

        string moneyKey = SinvestSession.GetSlotKey( "money" );
        string sharesKey = SinvestSession.GetSlotKey( "fundino_shares" );

        // These methods exist in your source snippet and work correctly
        Stats.Increment( moneyKey, moneyDelta );
        Stats.Increment( sharesKey, sharesDelta );

        CurrentMoney += moneyDelta;
        CurrentShares += sharesDelta;

        if ( GameSaveSystem.Instance?.CurrentCharacter != null )
        {
            GameSaveSystem.Instance.CurrentCharacter.Money = CurrentMoney;
        }

        // Using FlushAsync as defined in your source snippet
        await Stats.FlushAsync();
    }
}
