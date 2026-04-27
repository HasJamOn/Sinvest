using Sandbox;
using System;
using System.Linq;

namespace Sinvest;

public sealed class AssetGenerator : Component
{
    [Property] public float BasePotential { get; set; } = 10.0f;

    public IdleMonData RollNewAsset()
{
    var random = new Random();
    
    // 1. Market Scale (S)
    double currentPrice = MarketService.CurrentPrice;
    float S = (currentPrice > 0) ? (float)(currentPrice / 7126.0) : 1.0f;

    // 2. Name & Metadata
    string[] prefixes = { "Alpha", "Beta", "Sigma", "Delta", "Omega", "Prime", "Cyber", "Nano", "Void", "Flux" };
    string[] suffixes = { "Node", "Link", "Core", "Unit", "Asset", "Matrix", "Pulse", "Array", "Vault" };
    string randomName = $"{prefixes[random.Next(prefixes.Length)]}-{random.Next(100, 999)} {suffixes[random.Next(suffixes.Length)]}";

    // 3. Stat Generation Logic
    // Addition: Scaled by S
    double addition = Math.Round(random.NextDouble() * (BasePotential * S), 2);
    
    // Multiplier: 1.0 + (0 to 0.5 * S)
    double multiplier = 1.0 + Math.Round(random.NextDouble() * (0.5 * S), 2);

    // Subtraction (Cursed A): 40% chance to roll a penalty
    double subtraction = (random.NextDouble() < 0.4) 
        ? Math.Round(random.NextDouble() * (BasePotential * 0.5 * S), 2) 
        : 0;

    // Division (Cursed M): 20% chance to roll a divisor penalty (1.0 to 1.5)
    double division = (random.NextDouble() < 0.2) 
        ? 1.0 + Math.Round(random.NextDouble() * 0.5, 2) 
        : 1.0;

    // Team Bonus: 10% chance to roll a synergy boost (1.01 to 1.10)
    double teamBonus = (random.NextDouble() < 0.1) 
        ? 1.0 + Math.Round(random.NextDouble() * 0.1, 2) 
        : 1.0;

    return new IdleMonData
    {
        ID = Guid.NewGuid(),
        Name = randomName,
        RolledAt = DateTime.UtcNow,
        MarketScaleAtBirth = S,
        Generation = 2, // Era-linked in GDD
        
        Addition = addition,
        Multiplier = multiplier,
        Subtraction = subtraction,
        Division = division,
        TeamBonus = teamBonus,
        
        // Missing Utility Stats from GDD (Luck/Efficiency)
        Luck = Math.Round(random.NextDouble() * S, 2),
        CostEfficiency = (random.NextDouble() < 0.05) ? 0.05 : 0 // Rare 5% discount
    };
}
}
