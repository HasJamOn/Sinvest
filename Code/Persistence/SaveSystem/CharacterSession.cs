using System;

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

public enum TransactionResult
{
	Success,
	InsufficientFunds,
	SystemError
}

public class CharacterSession
{
	public string Name { get; set; } = "New Character";
	public double Money { get; set; }
	public double Shares { get; set; }
	public StartingModifiers Modifiers { get; set; }
    
	// Cache for IdleMon Data
	public Dictionary<string, double> IdleMonStats { get; set; } = new();
	public Dictionary<string, string> IdleMonMetadata { get; set; } = new();

	public bool HasModifier( StartingModifiers mod ) => Modifiers.HasFlag( mod );
}
