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
		var random = new Random();
    
		// 1. Calculate Market Scale (S)
		// S = Current Price / Base Price (7126.0)
		double currentPrice = MarketService.CurrentPrice;
		float S = (currentPrice > 0) ? (float)(currentPrice / 7126.0) : 1.0f;

		// 2. Determine Generation
		// We count the EraHistory + 1 for the current live era.
		int gen = 1;
		var marketSystem = Scene.GetAll<MarketServerSystem>().FirstOrDefault();
		if (marketSystem.IsValid())
		{
			// Generation is the current Era index. 
			// If EraHistory is empty, we are Gen 1. 
			// Note: You can expand this logic based on how often your seeds change.
			gen = 2; // For now, representing the current active Era after the 2025 entry.
		}

		// 3. Name Generation
		string[] prefixes = { "Alpha", "Beta", "Sigma", "Delta", "Omega", "Prime", "Cyber", "Nano" };
		string[] suffixes = { "Node", "Link", "Core", "Unit", "Asset", "Void", "Matrix", "Pulse" };
		string randomName = $"{prefixes[random.Next(prefixes.Length)]} {suffixes[random.Next(suffixes.Length)]}";

		// 4. Scaled Stat Calculation
		// Potential scales with S, meaning newer generations are naturally more powerful.
		double scaledPotential = BasePotential * S;

		return new IdleMonData
		{
			ID = Guid.NewGuid(),
			Name = randomName,
			RolledAt = DateTime.UtcNow,
			MarketScaleAtBirth = S,
			Generation = gen,
           
			// Stats: Addition and Multiplier ceiling grows with the Market (S)
			Addition = Math.Round(random.NextDouble() * scaledPotential, 2),
			Multiplier = 1.0 + Math.Round(random.NextDouble() * (0.5 * S), 2), 
       
			// Cursed Stats: Subtraction also scales up, making "Cursed" units riskier in high markets
			Subtraction = random.Next(0, 10) > 7 ? Math.Round(random.NextDouble() * (5.0 * S), 2) : 0,
       
			Division = 1.0,
			TeamBonus = 1.0
		};
	}
}
