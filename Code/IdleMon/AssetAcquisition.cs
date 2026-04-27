using Sandbox;
using System;
using System.Linq;

namespace Sinvest;

public sealed class AssetAcquisition : Component
{
    [Property] public double StandardEggCost { get; set; } = 10000;
    
    // The Base Price from your MarketServerSystem
    private const double MarketBasePrice = 7126.0;

    public void RollStandardEgg()
    {
        var econ = EconomyManager.Instance;
        var roster = RosterManager.Instance;

        // 1. Check Funds (Using IdleBucks from RosterManager)
        if ( roster.IdleBucks < StandardEggCost )
        {
            Log.Warning( "Insufficient IdleBucks!" );
            return;
        }

        // 2. Deduct Cost
        roster.ConsumeIdleBucks( StandardEggCost );

        // 3. Calculate Market Scale (S)
        float currentS = (float)(MarketService.CurrentPrice / MarketBasePrice);

        // 4. Generate the "Candidate"
        IdleMonData candidate = GenerateRandomNode( currentS );

        // 5. Open the Draft UI
        // We'll hook this into your UI layer next
        Log.Info( $"Rolled: {candidate.Name} with S Factor: {currentS:F2}" );
        
        // For now, let's just force add it if there's space, or trigger Draft
        OpenDraftOverlay( candidate );
    }

    private IdleMonData GenerateRandomNode( float sFactor )
    {
        var random = new Random();
        
        // Use S to scale the "Potential" of the roll
        // High S = Higher ceiling for Addition and Multiplier
        double potentialA = (random.NextDouble() * 5.0 + 1.0) * sFactor;
        double potentialM = (random.NextDouble() * 0.5 + 1.0) * sFactor;

        return new IdleMonData
        {
            ID = Guid.NewGuid(),
            Name = "Node-" + random.Next( 1000, 9999 ),
            Addition = potentialA,
            Multiplier = potentialM,
            Subtraction = random.NextDouble() > 0.8 ? random.NextDouble() * 2.0 : 0, // 20% chance for "Cursed"
            Division = 1.0,
            TeamBonus = 1.0,
            RolledAt = DateTime.UtcNow,
            MarketScaleAtBirth = sFactor,
            Generation = 1
        };
    }

    private void OpenDraftOverlay( IdleMonData candidate )
    {
        // This is where we trigger the UI State B (Asset Acquisition)
        // We will pass the 'candidate' to the UI to show the +/- Delta
    }
}
