using Sandbox;
using System;

namespace Sinvest;

[System.Serializable]
public struct IdleMonData
{
	/// <summary>
	/// Unique identifier used to seed deterministic model selection.
	/// Same ID = Same Model from the pool.
	/// </summary>
	public Guid ID;
	public string Name;

	/// <summary>
	/// If set, this specific model is used. 
	/// If null or empty, the system picks deterministically from the folder pool using ID.
	/// </summary>
	public string ModelPath;

	/// <summary>
	/// Optional: Can be used to point to specific sub-folders in the ModelPool 
	/// (e.g., "models/idlemon/rare")
	/// </summary>
	public string PoolCategory;

	public int SteamId;
    
	// --- Primary Stats ---
	[Title( "Addition (A)" )] public double Addition;      
	[Title( "Multiplier (M)" )] public double Multiplier;    
	[Title( "Subtraction (S)" )] public double Subtraction;   
	[Title( "Division (D)" )] public double Division;      
	[Title( "Team Bonus (T)" )] public double TeamBonus;
	[Title( "Luck (L)" )] public double Luck;
	[Title( "Efficiency (E)" )] public double CostEfficiency;
    
	// --- Metadata & Lifecycle ---
	public DateTime RolledAt;
	public float MarketScaleAtBirth { get; set; } 
	public int Generation { get; set; }           

	/// <summary>
	/// Default empty state for vacant roster slots.
	/// </summary>
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
