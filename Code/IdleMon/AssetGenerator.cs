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
    
       // 1. Calculate Market Scale (S)
       double currentPrice = MarketService.CurrentPrice;
       float S = (currentPrice > 0) ? (float)(currentPrice / 7126.0) : 1.0f;

       // 2. Dynamic Generation/Era Check
       // Using the singleton pattern we established for the other systems
       var marketSystem = Scene.GetAllComponents<MarketServerSystem>().FirstOrDefault();
       int gen = 1;
       
       if (marketSystem.IsValid())
       {
          // If you ever add more Eras to your MarketServerSystem.EraHistory, 
          // this will automatically increment the Generation of new rolls.
          // gen = marketSystem.EraHistory.Count + 1; 
          gen = 2; 
       }

       // 3. Name Generation
       string[] prefixes = { "Alpha", "Beta", "Sigma", "Delta", "Omega", "Prime", "Cyber", "Nano" };
       string[] suffixes = { "Node", "Link", "Core", "Unit", "Asset", "Void", "Matrix", "Pulse" };
       string randomName = $"{prefixes[random.Next(prefixes.Length)]}-{random.Next(100, 999)} {suffixes[random.Next(suffixes.Length)]}";

       // 4. Scaled Stat Calculation
       // The 'Addition' ceiling is now directly tied to the current market power.
       double scaledPotential = BasePotential * S;

       return new IdleMonData
       {
          ID = Guid.NewGuid(),
          Name = randomName,
          RolledAt = DateTime.UtcNow,
          MarketScaleAtBirth = S,
          Generation = gen,
           
          // Stats: A and M ceilings grow with S.
          Addition = Math.Round(random.NextDouble() * scaledPotential, 2),
          Multiplier = 1.0 + Math.Round(random.NextDouble() * (0.5 * S), 2), 
       
          // Cursed Stats: 30% chance to roll a subtraction penalty
          Subtraction = random.Next(0, 10) > 7 ? Math.Round(random.NextDouble() * (5.0 * S), 2) : 0,
       
          Division = 1.0,
          TeamBonus = 1.0,
          //Luck = 0 // Future: Tied to Egg Tiers
       };
    }
}
