using Sandbox;
using Sandbox.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sandbox;

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
	public StartingModifiers Modifiers { get; set; }
}

public sealed class GameSaveSystem : Component
{
	public static GameSaveSystem Instance { get; private set; }

	[Property] public bool UseCloud { get; set; } = false;
	[Property] public bool UnlockAllModifiers { get; set; } = false;
	[Property] public int ActiveSlot { get; set; } = 1;

	public CharacterSession CurrentCharacter { get; private set; }

	private Dictionary<string, string> _localCookies = new();
	private Dictionary<string, double> _localStats = new();

	protected override void OnAwake()
	{
		Instance = this;
		GameObject.Flags |= GameObjectFlags.NotSaved;
		GameObject.Parent = null;
	}

	public string SlotSuffix => $"_{ActiveSlot}";

	public void LoadActiveSlot()
	{
		CurrentCharacter = new CharacterSession
		{
			Name = GetStoredName(),
			Money = GetStoredStat( "money" ),
			Modifiers = (StartingModifiers)(int)GetStoredStat( "modifiers" )
		};

		Log.Info( $"[SAVE SYSTEM] Loaded {CurrentCharacter.Name} (Slot {ActiveSlot})" );
	}

	public async System.Threading.Tasks.Task SaveActiveSlotAsync()
	{
		if ( CurrentCharacter == null ) return;

		SetStoredName( CurrentCharacter.Name );
		SetStoredStat( "money", CurrentCharacter.Money );
		SetStoredStat( "modifiers", (int)CurrentCharacter.Modifiers );

		if ( UseCloud )
		{
			// Pause this method until the cloud confirms receipt.
			// The game itself keeps rendering at 60+ FPS while we wait.
			await Sandbox.Services.Stats.FlushAsync();
		}

		Log.Info( $"[SAVE SYSTEM] Save confirmed for {CurrentCharacter.Name}" );
	}

	private HashSet<string> _localAchievements = new();

	public bool IsModifierUnlocked( StartingModifiers modifier )
	{
		if ( UnlockAllModifiers ) return true;
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

		// Check Cloud vs Local
		if ( UseCloud )
		{
			return Sandbox.Services.Achievements.All.Any( x => x.Name == achievementId && x.IsUnlocked );
		}
		else
		{
			return _localAchievements.Contains( achievementId );
		}
	}

	//a helper to "fake" an unlock for testing
	public void DebugUnlockAchievement( string id )
	{
		if ( UseCloud ) return; // Don't allow debug unlocks to touch the cloud
		_localAchievements.Add( id );
		Log.Info( $"[SAVE SYSTEM] Debug Unlocked: {id}" );
	}

	private void SetStoredName( string name )
	{
		string key = $"name{SlotSuffix}";
		if ( UseCloud ) Game.Cookies.Set( key, name );
		else _localCookies[key] = name;
	}

	private string GetStoredName()
	{
		string key = $"name{SlotSuffix}";
		return UseCloud ? Game.Cookies.Get( key, "New Character" ) : _localCookies.GetValueOrDefault( key, "New Character" );
	}

	private void SetStoredStat( string statName, double val )
	{
		string key = $"{statName}{SlotSuffix}";
		if ( UseCloud ) Stats.SetValue( key, val );
		else _localStats[key] = val;
	}

	private double GetStoredStat( string statName )
	{
		string key = $"{statName}{SlotSuffix}";
		if ( UseCloud ) return Stats.LocalPlayer.Get( key ).Value;
		return _localStats.GetValueOrDefault( key, 0 );
	}
	
	public string GetStoredNameForSlot( int slot )
	{
		string key = $"name_{slot}";
		// We use Game.Cookies/Stats here because this is the SaveSystem script
		return UseCloud ? Game.Cookies.Get( key, "New Character" ) : _localCookies.GetValueOrDefault( key, "New Character" );
	}

	public void DeleteSlot( int slot )
	{
		string suffix = $"_{slot}";
		if ( UseCloud )
		{
			Game.Cookies.Set( $"name{suffix}", "New Character" );
			Sandbox.Services.Stats.SetValue( $"money{suffix}", 0 );
			Sandbox.Services.Stats.SetValue( $"modifiers{suffix}", 0 );
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
		// If we are unlocking all, or in local mode and have the fake ID
		if ( UnlockAllModifiers ) return true;
    
		const string id = "graduated";
    
		if ( UseCloud )
		{
			return Sandbox.Services.Achievements.All.Any( x => x.Name == id && x.IsUnlocked );
		}
		else
		{
			return _localAchievements.Contains( id );
		}
	}
}
