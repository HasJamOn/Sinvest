using System;
using System.Collections.Generic;

namespace Sinvest;

/// <summary>
/// Defines unique character traits and perks chosen at the start of a playthrough.
/// These are stackable bitflags with no hard restrictions, allowing for highly 
/// customized starting conditions.
/// </summary>
[Flags]
public enum StartingModifiers : int
{
    None = 0,
    
    // Start with $500K locked in S&P 500 for long-term passive growth.
    Nepokid = 1,
    
    // +20% Luck for jobs and solo casino; disabled in multiplayer for fairness.
    LadyLuck = 2,
    
    // Immediate access to the Sinvest app, bypassing the internet cafe phase.
    Phoney = 4,
    
    // Grants access to the entire job roster immediately upon starting.
    Internity = 8,
    
    // Massive +1000% promotion chance and includes a unique cosmetic reward.
    BrownNose = 16,
    
    // Enables the unskippable tutorial guide. Toggling off removes the guide for the run.
    MentorDad = 32,
    
    // Toggles internal testing tools and "cheats." 
    // Currently for developer use; potential unlockable cheat mode for players post-launch.
    DevMode = 64,
    
    // The "Hardcore" start. Overrides all starting bonuses, 
    // forcing the character to begin with $0 cash and 0 shares.
    Destitute = 128
}

/// <summary>
/// Standardized return types for financial operations, used to drive UI feedback
/// and handle logic branches for successful or failed transactions.
/// </summary>
public enum TransactionResult
{
    Success,
    InsufficientFunds,
    SystemError
}

/// <summary>
/// Represents the live state of a player's character during a game session.
/// This acts as a volatile cache of the ledger, providing quick access to 
/// totals like Money and Shares without re-parsing the file constantly.
/// </summary>
public class CharacterSession
{
    /// <summary> The display name of the character. </summary>
    public string Name { get; set; } = "New Character";

    /// <summary> 
    /// Current liquid cash. In the authoritative ledger model, 
    /// this value represents: Inflow.Sum - Outflow.Sum. 
    /// </summary>
    public double Money { get; set; }

    /// <summary> Total quantity of Fundino shares currently held. </summary>
    public double Shares { get; set; }

    /// <summary> The bitmask of active perks/modifiers selected during character creation. </summary>
    public StartingModifiers Modifiers { get; set; }
    
    /// <summary> 
    /// Tracks unique identifiers for permanent unlocks to prevent duplicate recording.
    /// </summary>
    public HashSet<string> UnlockedItems { get; set; } = new();
    
    /// <summary> 
    /// Local cache for numerical IdleMon progress (Levels, XP, Stats). 
    /// </summary>
    public Dictionary<string, double> IdleMonStats { get; set; } = new();

    /// <summary> 
    /// Local cache for string-based IdleMon context (Custom names, status effects). 
    /// </summary>
    public Dictionary<string, string> IdleMonMetadata { get; set; } = new();

    /// <summary>
    /// Helper method to check if a specific StartingModifier bit is active.
    /// </summary>
    public bool HasModifier( StartingModifiers mod ) => Modifiers.HasFlag( mod );
}
