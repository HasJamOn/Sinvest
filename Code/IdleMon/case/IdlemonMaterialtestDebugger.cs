using Sandbox;
using System;
using Sinvest;

public sealed class IdlemonDebugger : Component
{
	[Property, Group( "Target" )] public IdlemonCase TargetCase { get; set; }

	[Header( "Steam Identity" )]
	[Property] public int SteamId { get; set; } = 4000;

	[Header( "Core Formula Stats" )]
	[Property, Range( 0, 1000 )] public double Addition { get; set; } = 0;
	[Property, Range( 0, 1000 )] public double Subtraction { get; set; } = 0;
	[Property, Range( 1, 20 )] public double Multiplier { get; set; } = 1.0;
	[Property, Range( 1, 20 )] public double Division { get; set; } = 1.0;
	[Property, Range( 1, 5 )] public double TeamBonus { get; set; } = 1.0;

	[Header( "Metadata & Rarity" )]
	[Property, Range( 0, 100 )] public double Luck { get; set; } = 50;
	[Property, Range( 0, 100 )] public double CostEfficiency { get; set; } = 50;
	[Property, Range( 1, 9999 )] public int Generation { get; set; } = 1;

	protected override void OnUpdate()
	{
		if ( !TargetCase.IsValid() ) return;

		// Package every single datapoint into the struct
		var mock = new IdleMonData
		{
			ID = Guid.Empty, // Placeholder for debug
			Name = "DebugMon",
			ModelPath = "models/dev/box.vmdl",
			
			Addition = Addition,
			Multiplier = Multiplier,
			Subtraction = Subtraction,
			Division = Division,
			TeamBonus = TeamBonus,
			
			Luck = Luck,
			CostEfficiency = CostEfficiency,
			Generation = Generation,
			
			RolledAt = DateTime.Now
		};

		// Push the full payload to the case
		TargetCase.UpdateAllStats( SteamId, mock );
	}
}
