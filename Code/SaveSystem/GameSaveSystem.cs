using Sandbox; 
using Sinvest;
using Sandbox.Services;
using System;
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
    DevMode = 64
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

    public void LoadActiveSlot()
    {
	    CurrentCharacter = new CharacterSession
	    {
		    Name = GetStoredName(),
		    Money = GetStoredStat( "money" ),
		    Shares = GetStoredStat( "fundino_shares" ), // Load shares here
		    Modifiers = (StartingModifiers)(int)GetStoredStat( "modifiers" )
	    };
    }

    public async System.Threading.Tasks.Task SaveActiveSlotAsync()
    {
       if ( CurrentCharacter == null ) return;

       SetStoredName( CurrentCharacter.Name );
       SetStoredStat( "money", CurrentCharacter.Money );
       SetStoredStat( "modifiers", (int)CurrentCharacter.Modifiers );

       // Explicitly only flush if we are in cloud mode
       if ( ShouldUseCloud )
       {
          await Sandbox.Services.Stats.FlushAsync();
          Log.Info( $"[SAVE SYSTEM] Cloud Sync Complete for {CurrentCharacter.Name}" );
       }
       else
       {
          Log.Info( $"[SAVE SYSTEM] Local Save Confirmed for {CurrentCharacter.Name}" );
       }
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
          _ => null
       };

       if ( string.IsNullOrEmpty( achievementId ) ) return false;

       if ( ShouldUseCloud )
       {
          return Sandbox.Services.Achievements.All.Any( x => x.Name == achievementId && x.IsUnlocked );
       }
       
       return _localAchievements.Contains( achievementId );
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
	    // Always use Cookies so it persists on your PC, even if Cloud is off
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
   
	    if ( ShouldUseCloud ) 
		    Stats.SetValue( key, val );
    
	    // Always save a local copy to the cookie file so it survives Editor reloads
	    Game.Cookies.Set( key, val.ToString() );

	    OnDataChanged?.Invoke();
    }

    public double GetStoredStat( string statName )
    {
	    string key = $"{statName}{SlotSuffix}";
   
	    if ( ShouldUseCloud ) 
	    {
		    return (double)Stats.LocalPlayer.Get( key ).Value;
	    }

	    // Pull from cookie file and parse back to double
	    string val = Game.Cookies.Get( key, "0" );
	    return double.TryParse(val, out double result) ? result : 0;
    }
    
    public string GetStoredNameForSlot( int slot )
    {
       string key = $"name_{slot}";
       if ( ShouldUseCloud ) return Game.Cookies.Get( key, "New Character" );
       return _localCookies.GetValueOrDefault( key, "New Character" );
    }

    public void DeleteSlot( int slot )
    {
	    string suffix = $"_{slot}";
	    if ( ShouldUseCloud )
	    {
		    Game.Cookies.Set( $"name{suffix}", "New Character" );
		    Stats.SetValue( $"money{suffix}", 0 );
		    Stats.SetValue( $"modifiers{suffix}", 0 );
		    Stats.SetValue( $"fundino_shares{suffix}", 0 );
		    Stats.SetValue( $"claimed_startup{suffix}", 0 );

		    Sandbox.Services.Stats.Flush();
	    }
       else
       {
          _localCookies[$"name{suffix}"] = "New Character";
          _localStats[$"money{suffix}"] = 0;
          _localStats[$"modifiers{suffix}"] = 0;
       }
       Log.Info( $"[SAVE SYSTEM] Wiped Slot {slot}" );
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
}
