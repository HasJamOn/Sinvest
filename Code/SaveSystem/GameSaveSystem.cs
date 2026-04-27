using Sandbox; 
using Sinvest;
using Sandbox.Services;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace Sinvest;

[Flags]
public enum StartingModifiers : int
{
    None = 0,
    Nepokid = 1,
    LadyLuck = 2,
    Phoney = 4,
    Internity = 8,
    BrownNose = 16,
    MentorDad = 32,
    DevMode = 64,
    Destitute = 128
}

public class CharacterSession
{
	public string Name { get; set; }
	public double Money { get; set; }
	public double Shares { get; set; }
	public StartingModifiers Modifiers { get; set; }
}

public sealed partial class GameSaveSystem : Component
{
    public static GameSaveSystem Instance { get; private set; }
    
    public static Action OnDataChanged { get; set; }

    [Property] public bool UseCloud { get; set; } = false;
    [Property] public bool UnlockAllModifiers { get; set; } = false;
    [Property] public int ActiveSlot { get; set; } = 1;

    // --- Hardened Debug Helper ---
    // We use a getter that searches the active scene to ensure we find the manager
    // even if this script awakes before the DebugManager.
    private SinvestDebugManager _debug => Scene.GetAll<SinvestDebugManager>().FirstOrDefault();
    
    /// <summary>
    /// Global toggle: Returns false if Debug Manager is forcing Local Mode, 
    /// otherwise falls back to the inspector 'UseCloud' setting.
    /// </summary>
    public bool ShouldUseCloud => (_debug.IsValid() && _debug.UseLocalSaveOnly) ? false : UseCloud;
    
    /// <summary>
    /// Logic gate for character creation and progression unlocks.
    /// </summary>
    private bool ShouldUnlockAll => UnlockAllModifiers || (_debug.IsValid() && _debug.ResetStatsOnStart);

    // --- State & Storage ---
    public CharacterSession CurrentCharacter { get; private set; }

    private Dictionary<string, string> _localCookies = new();
    private Dictionary<string, double> _localStats = new();
    private HashSet<string> _localAchievements = new();
    
    public async Task SyncAndVerify()
    {
	    // 1. Get the 'True' value from the Cloud
	    double cloudMoney = GetStoredStat( "money" ); // This pulls from Sandbox.Services.Stats
    
	    // 2. Get the 'Fast' value from the local Cookie
	    string cookieVal = Game.Cookies.Get( $"money{SlotSuffix}", "0" );
	    double localMoney = double.TryParse(cookieVal, out double res) ? res : 0;

	    // 3. The Security Check
	    if ( Math.Abs(cloudMoney - localMoney) > 0.01 )
	    {
		    Log.Warning( "[SECURITY] Local money differs from Cloud! Resetting to Cloud value." );
        
		    // Force the local session to match the Cloud
		    if ( CurrentCharacter != null )
			    CurrentCharacter.Money = cloudMoney;
            
		    // Overwrite the 'dirty' local cookie with the 'clean' cloud data
		    Game.Cookies.Set( $"money{SlotSuffix}", cloudMoney.ToString() );
	    }
    }

    protected override void OnAwake()
    {
	    // 1. Singleton Guard: Ensure only one Save System exists across scenes
	    if ( Instance.IsValid() && Instance != this )
	    {
		    Log.Info( "[SAVE SYSTEM] Duplicate detected during scene load, destroying..." );
		    GameObject.Destroy();
		    return;
	    }

	    Instance = this;

	    // 2. Persistence Logic: The specific combination for your API version
	    // DontDestroyOnLoad (128): The crucial flag to survive scene cleaning
	    // NotSaved (2): Prevents this runtime instance from being baked into scene files
	    GameObject.Flags |= GameObjectFlags.DontDestroyOnLoad;
	    GameObject.Flags |= GameObjectFlags.NotSaved;

	    // 3. Hierarchy Detachment: Move to root so it isn't killed with a parent object
	    GameObject.Parent = null;

	    Log.Info( "[SAVE SYSTEM] Initialized and marked as Persistent." );
    }

    public string SlotSuffix => $"_{ActiveSlot}";

    public async Task LoadActiveSlot() // Changed to async Task
    {
	    CurrentCharacter = new CharacterSession
	    {
		    Name = GetStoredName(),
		    Money = GetStoredStat( "money" ),
		    Shares = GetStoredStat( "fundino_shares" ), 
		    Modifiers = (StartingModifiers)(int)GetStoredStat( "modifiers" )
	    };

	    await SyncAndVerify(); // Now we can safely await the verification
	    ValidateSaveIntegrity();
    }

    public async Task SaveActiveSlotAsync()
    {
	    if ( CurrentCharacter == null ) return;

	    // 1. Save the specific slot data
	    SetStoredName( CurrentCharacter.Name );
	    SetStoredStat( "money", CurrentCharacter.Money );
	    SetStoredStat( "modifiers", (int)CurrentCharacter.Modifiers );
	    // Ensure IdleBucks or other stats are saved here too if needed

	    // 2. UPDATE TOTALS (Aggregate across all slots)
	    UpdateGlobalTotals();

	    if ( ShouldUseCloud )
	    {
		    await Sandbox.Services.Stats.FlushAsync();
		    Log.Info( $"[SAVE SYSTEM] Cloud Sync Complete (Totals Updated)" );
	    }
    }

    private void UpdateGlobalTotals()
    {
	    double totalMoney = 0;
	    double totalShares = 0;
	    double totalBucks = 0;

	    // Iterate through your available slots (assuming 1-3 based on standard UI)
	    for ( int i = 1; i <= 3; i++ )
	    {
		    totalMoney += GetStoredStatForSlot( i, "money" );
		    totalShares += GetStoredStatForSlot( i, "fundino_shares" );
		    totalBucks += GetStoredStatForSlot( i, "idlemon_bucks" );
	    }

	    // Save these to a "Global" key (no suffix)
	    if ( ShouldUseCloud )
	    {
		    Stats.SetValue( "total_money", totalMoney );
		    Stats.SetValue( "total_shares", totalShares );
		    Stats.SetValue( "total_idlemon_bucks", totalBucks );
	    }

	    // Also update local cookies for immediate retrieval
	    Game.Cookies.Set( "total_money", totalMoney.ToString() );
	    Game.Cookies.Set( "total_shares", totalShares.ToString() );
	    Game.Cookies.Set( "total_idlemon_bucks", totalBucks.ToString() );
    }

    public bool IsModifierUnlocked( StartingModifiers modifier )
    {
       if ( ShouldUnlockAll ) return true;
       if ( modifier == StartingModifiers.MentorDad || modifier == StartingModifiers.None ) return true;

       string achievementId = modifier switch
       {
          StartingModifiers.Nepokid => "the_inheritance",
          StartingModifiers.LadyLuck => "high_roller",
          StartingModifiers.Phoney => "disconnected",
          StartingModifiers.Internity => "career_maniac",
          StartingModifiers.BrownNose => "employee_of_the_month",
          StartingModifiers.Destitute => "rock_bottom",
          _ => null
       };

       if ( string.IsNullOrEmpty( achievementId ) ) return false;

       if ( ShouldUseCloud )
       {
          return Sandbox.Services.Achievements.All.Any( x => x.Name == achievementId && x.IsUnlocked );
       }
       
       return _localAchievements.Contains( achievementId );
    }
    
    /// <summary>
    /// Returns the combined value across all character slots.
    /// </summary>
    public double GetGlobalTotal( string statName )
    {
	    string key = $"total_{statName}";

	    if ( ShouldUseCloud )
	    {
		    return (double)Stats.LocalPlayer.Get( key ).Value;
	    }

	    string val = Game.Cookies.Get( key, "0" );
	    return double.TryParse( val, out double result ) ? result : 0;
    }

    public void DebugUnlockAchievement( string id )
    {
       if ( ShouldUseCloud ) return; 
       _localAchievements.Add( id );
       Log.Info( $"[SAVE SYSTEM] Debug Unlocked Achievement: {id}" );
    }

    // --- Internal Get/Set Methods (Standardized to ShouldUseCloud) ---

    private void SetStoredName( string name )
    {
	    string key = $"name{SlotSuffix}";
	    Game.Cookies.Set( key, name );
    }

    private string GetStoredName()
    {
	    string key = $"name{SlotSuffix}";
	    return Game.Cookies.Get( key, "New Character" );
    }

    public void SetStoredStat( string statName, double val )
    {
	    string key = $"{statName}{SlotSuffix}";

	    // 1. UPDATE THE LIVE SESSION (So UI sees it instantly)
	    if ( CurrentCharacter != null )
	    {
		    if ( statName == "money" ) CurrentCharacter.Money = val;
		    if ( statName == "fundino_shares" ) CurrentCharacter.Shares = val;
	    }

	    // 2. PERSISTENCE
	    if ( ShouldUseCloud ) 
		    Stats.SetValue( key, val );

	    Game.Cookies.Set( key, val.ToString() );

	    // 3. ALERT THE UI
	    OnDataChanged?.Invoke();
    }

    public double GetStoredStat( string statName )
    {
	    string key = $"{statName}{SlotSuffix}";

	    // If Cloud is active, pull from Steam/Facepunch stats
	    if ( ShouldUseCloud ) 
	    {
		    return (double)Stats.LocalPlayer.Get( key ).Value;
	    }

	    // Otherwise pull from local cookie file
	    string val = Game.Cookies.Get( key, "0" );
	    return double.TryParse(val, out double result) ? result : 0;
    }
    
    public string GetStoredNameForSlot( int slot )
    {
	    string key = $"name_{slot}";

	    if ( ShouldUseCloud ) 
	    {
		    // 1. Peek at existing competitive stats instead of a dedicated "active" flag
		    var moneyVal = Stats.LocalPlayer.Get( $"money_{slot}" ).Value;
		    var yieldVal = Stats.LocalPlayer.Get( $"current_yield_pps_{slot}" ).Value;
		    var bucksVal = Stats.LocalPlayer.Get( $"idlemon_bucks_{slot}" ).Value;
        
		    // 2. If any of these are > 0, the slot is used. 
		    // We pull the name from Cookies (which are synced via Steam Cloud).
		    if ( moneyVal > 0 || yieldVal > 0 || bucksVal > 0 )
		    {
			    return Game.Cookies.Get( key, "Active Career" );
		    }

		    return "New Character";
	    }

	    // Local fallback
	    return _localCookies.GetValueOrDefault( key, "New Character" );
    }
    
    /// <summary>
    /// Peeks at a specific slot's stat without switching the active session.
    /// </summary>
    public double GetStoredStatForSlot( int slot, string statName )
    {
	    // Redirect legacy 'active' requests to check yield instead of the retired occupation flag.
	    // If yield is > 0, the game logic considers the slot "Active".
	    string key = (statName == "active") ? $"current_yield_pps_{slot}" : $"{statName}_{slot}";

	    if ( ShouldUseCloud )
	    {
		    return (double)Stats.LocalPlayer.Get( key ).Value;
	    }

	    // Local Fallback
	    string val = Game.Cookies.Get( key, "0" );
	    return double.TryParse( val, out double result ) ? result : 0;
    }

    public void DeleteSlot( int slot )
    {
	    string suffix = $"_{slot}";
    
	    if ( ShouldUseCloud )
	    {
		    // 1. Reset the Name in Cloud Cookies
		    Game.Cookies.Set( $"name{suffix}", "New Character" );

		    // 2. Wipe all Competitive Stats
		    // Zeroing these out ensures GetStoredNameForSlot recognizes the slot as empty.
		    Stats.SetValue( $"money{suffix}", 0 );
		    Stats.SetValue( $"idlemon_bucks{suffix}", 0 );
		    Stats.SetValue( $"current_yield_pps{suffix}", 0 );
		    Stats.SetValue( $"roster_max_luck{suffix}", 0 );

		    // 3. Wipe Internal Progression Stats
		    Stats.SetValue( $"modifiers{suffix}", 0 );
		    Stats.SetValue( $"fundino_shares{suffix}", 0 );
		    Stats.SetValue( $"claimed_startup{suffix}", 0 );

		    // 4. Force immediate sync
		    Sandbox.Services.Stats.Flush();
        
		    // 5. Clear IdleMon JSON Data from Cookies
		    Game.Cookies.Set( $"idlemon_roster{suffix}", "" );
		    Game.Cookies.Set( $"idlemon_last_timestamp{suffix}", "" );
	    }
	    else
	    {
		    // Local Fallback
		    _localCookies[$"name{suffix}"] = "New Character";
		    _localStats[$"money{suffix}"] = 0;
		    _localStats[$"modifiers{suffix}"] = 0;
		    _localStats[$"idlemon_bucks{suffix}"] = 0;
	    }

	    UpdateGlobalTotals();
    
	    Log.Info( $"[SAVE SYSTEM] Wiped Slot {slot} and recalculated global totals." );
    }

    public bool HasGraduated()
    {
       if ( ShouldUnlockAll ) return true;
    
       const string id = "graduated";
    
       if ( ShouldUseCloud )
       {
          return Sandbox.Services.Achievements.All.Any( x => x.Name == id && x.IsUnlocked );
       }
       
       return _localAchievements.Contains( id );
    }
    
    public bool IsSaveTrusted { get; private set; } = true;
    
    /// <summary>
/// Called whenever Money, Shares, or IdleBucks are saved.
/// Generates the signature and saves both the signature AND the seed used.
/// </summary>
public void UpdateSecuritySignature()
{
    if ( CurrentCharacter == null ) return;

    // 1. Gather the current state from the managers
    double m = CurrentCharacter.Money;
    double s = CurrentCharacter.Shares;
    
    // Safely get IdleBucks (defaults to 0 if manager isn't awake yet)
    double ib = RosterManager.Instance?.IdleBucks ?? 0;

    // 2. Determine the seed to use
    int currentSeed = 9928341; // Default
    if ( MarketServerSystem.Instance != null && MarketServerSystem.Instance.CurrentSeed != 0 )
    {
        currentSeed = MarketServerSystem.Instance.CurrentSeed;
    }

    // 3. Generate and store
    string sig = SinvestSession.GenerateSignature( m, s, ib, currentSeed );
    
    // Store in Cookies so it stays private and doesn't eat your Stat quota
    Game.Cookies.Set( $"save_signature{SlotSuffix}", sig );
    Game.Cookies.Set( $"save_seed{SlotSuffix}", currentSeed.ToString() );
}

/// <summary>
/// Called immediately after loading a character's stats.
/// </summary>
	public void ValidateSaveIntegrity()
	{
	    double m = GetStoredStat( "money" );
	    double s = GetStoredStat( "fundino_shares" );
	    double ib = GetStoredStat( "idlemon_bucks" );
	    
	    string storedSig = Game.Cookies.Get( $"save_signature{SlotSuffix}", "" );
	    string seedStr = Game.Cookies.Get( $"save_seed{SlotSuffix}", "9928341" );
	    int storedSeed = int.TryParse( seedStr, out int res ) ? res : 9928341;

	    // If it's a brand new character, trust it automatically
	    if ( string.IsNullOrEmpty( storedSig ) && m == 0 && s == 0 && ib == 0 )
	    {
	        IsSaveTrusted = true;
	        return;
	    }

	    // Calculate what the signature SHOULD be, using the seed they saved with
	    string calculatedSig = SinvestSession.GenerateSignature( m, s, ib, storedSeed );

	    if ( storedSig != calculatedSig )
	    {
	        Log.Warning( $"[SECURITY] Checksum mismatch on Slot {ActiveSlot}. Flagging save as Untrusted." );
	        IsSaveTrusted = false;
	    }
	    else
	    {
	        IsSaveTrusted = true;
	    }
	}	
}
