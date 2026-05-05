using Sandbox;
using System;

namespace Sinvest;

/// <summary>
/// The core data structure representing an "IdleMon" asset.
/// Follows a strict Addition/Subtraction/Multiplier/Division (ASMD) math model.
/// </summary>
[System.Serializable]
public struct IdleMonData
{
	// --- IDENTIFICATION ---
	public Guid ID;             // Unique instance identifier for roster persistence
	public string Name;         // Human-readable title (typically injected via Steam API)
	public string ModelPath;    // Specific model override (null if using Pooled Visuals)
	public string PoolCategory; // Tags for visual variance (e.g., "standard", "gold", "void")
	public int SteamId;         // The AppID used for shader parameters and icon fetching
    
	// --- ECONOMIC ATTRIBUTES (The ASMD Engine) ---
	// Logic Order: ((Base + Sum(A)) - Sum(S)) / Prod(D) * Prod(M) * Prod(T)

	[Title( "Addition (A)" )] 
	public double Addition;      // Flat increase to base generation (PPS)

	[Title( "Multiplier (M)" )] 
	public double Multiplier;    // Percentage-based boost applied after base calculations

	[Title( "Subtraction (S)" )] 
	public double Subtraction;   // Flat penalty (Negative traits or "Rusty" assets)

	[Title( "Division (D)" )] 
	public double Division;      // Efficiency tax (divisor); must be >= 1.0 to avoid bugs

	[Title( "Team Bonus (T)" )] 
	public double TeamBonus;    // Contextual multiplier applied if requirements are met

	[Title( "Luck (L)" )] 
	public double Luck;          // Influences roll quality in the AssetGenerator

	[Title( "Efficiency (E)" )] 
	public double CostEfficiency; // Reduction in upgrade or maintenance costs
    
	// --- LIFECYCLE METADATA ---
	public DateTime RolledAt;         // Birth timestamp for age-based rewards
	public float MarketScaleAtBirth { get; set; } // Global multiplier (S) at time of creation
	public int Generation { get; set; }           // Determines stat caps and rarity tier

	// --- FALLBACKS ---
	public static IdleMonData Empty => new IdleMonData 
	{ 
		ID = Guid.Empty, 
		Name = "Empty Node",
		ModelPath = null,
		PoolCategory = null,
		Addition = 0,
		Subtraction = 0,
		Multiplier = 1.0, 
		Division = 1.0, 
		TeamBonus = 1.0,
		Luck = 0,             
		CostEfficiency = 0,    
		Generation = 0,
		RolledAt = DateTime.MinValue
	};
}
