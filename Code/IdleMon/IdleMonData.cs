using Sandbox;
using System;

namespace Sinvest;

[System.Serializable]
public struct IdleMonData
{
	public Guid ID;
	public string Name;
    
	[Title("Addition (A)")] public double Addition;      
	[Title("Multiplier (M)")] public double Multiplier;    
	[Title("Subtraction (S)")] public double Subtraction;   
	[Title("Division (D)")] public double Division;      
	[Title("Team Bonus (T)")] public double TeamBonus;     
    
	// --- Birth Certificate Metadata ---
	public DateTime RolledAt;
	public float MarketScaleAtBirth { get; set; } // The 'S' factor when created
	public int Generation { get; set; }           // Derived from Era/Seed history

	public static IdleMonData Empty => new IdleMonData 
	{ 
		ID = Guid.Empty, 
		Name = "Empty Node",
		Addition = 0,
		Subtraction = 0,
		Multiplier = 1.0, 
		Division = 1.0,   
		TeamBonus = 1.0,
		Generation = 0
	};
}
