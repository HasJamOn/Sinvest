using Sandbox;
using System;

namespace Sinvest;

// [System.Serializable] allows the Inspector to show these fields
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
    
	public DateTime RolledAt;

	// This makes sure new slots aren't "0" (which would break math)
	public static IdleMonData Empty => new IdleMonData 
	{ 
		ID = Guid.Empty, 
		Name = "Empty Node",
		Addition = 0,
		Subtraction = 0,
		Multiplier = 1.0, // Identity
		Division = 1.0,   // Identity
		TeamBonus = 1.0   // Identity
	};
}
