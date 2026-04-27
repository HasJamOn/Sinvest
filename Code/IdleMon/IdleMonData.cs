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
	[Title("Luck (L)")] public double Luck;
	[Title("Efficiency (E)")] public double CostEfficiency;
    
	public DateTime RolledAt;
	public float MarketScaleAtBirth { get; set; } 
	public int Generation { get; set; }           

	public static IdleMonData Empty => new IdleMonData 
	{ 
		ID = Guid.Empty, 
		Name = "Empty Node",
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
