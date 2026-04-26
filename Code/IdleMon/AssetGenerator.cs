using Sandbox;
using System;

namespace Sinvest;

public sealed class AssetGenerator : Component
{
	[Property] public float BasePotential { get; set; } = 10.0f;

	/// <summary>
	/// Creates a procedurally generated IdleMon.
	/// In a real scenario, this would factor in the Market Scale (S).
	/// </summary>
	public IdleMonData RollNewAsset()
	{
		// For testing, we'll use a simple random range
		var random = new Random();

		// Procedural Name Generation (Simple for now)
		string[] prefixes = { "Alpha", "Beta", "Sigma", "Delta", "Omega", "Prime" };
		string[] suffixes = { "Node", "Link", "Core", "Unit", "Asset", "Void" };
		string randomName = $"{prefixes[random.Next(prefixes.Length)]} {suffixes[random.Next(suffixes.Length)]}";

		return new IdleMonData
		{
			ID = Guid.NewGuid(),
			Name = randomName,
			RolledAt = DateTime.UtcNow,
            
			// Random Stats (Balance these for your GDD!)
			Addition = Math.Round(random.NextDouble() * BasePotential, 2),
			Multiplier = 1.0 + Math.Round(random.NextDouble() * 0.5, 2), // 1.0 to 1.5x
			Subtraction = random.Next(0, 10) > 7 ? random.Next(1, 5) : 0, // 30% chance for "Cursed" sub
			Division = 1.0,
			TeamBonus = 1.0
		};
	}
}
