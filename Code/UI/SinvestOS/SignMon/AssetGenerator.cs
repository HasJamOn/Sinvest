using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Sinvest;

public sealed class AssetGenerator : Component
{
	[Property] public float BasePotential { get; set; } = 10.0f;
    
	// Cache the IDs so we don't read the file every single time we roll
	private List<int> _cachedSteamIds = new();

	protected override void OnStart()
	{
		LoadBootstrapIds();
	}

	private void LoadBootstrapIds()
	{
		string path = "Code/Gameplay/Steam/SteamIDData/bootstrap_ids.txt";
       
		if ( FileSystem.Mounted.FileExists( path ) )
		{
			var content = FileSystem.Mounted.ReadAllText( path );
       
			// Updated Split to include commas and space
			_cachedSteamIds = content.Split( new[] { ",", "\r\n", "\r", "\n", " " }, StringSplitOptions.RemoveEmptyEntries )
				.Select( x => x.Trim() )
				.Select( x => int.TryParse( x, out int id ) ? id : -1 )
				.Where( id => id != -1 )
				.ToList();
           
			Log.Info( $"[AssetGenerator] Successfully parsed {_cachedSteamIds.Count} Steam IDs from CSV/List format." );
		}
		else
		{
			Log.Error( $"[AssetGenerator] Could not find bootstrap_ids.txt at {path}" );
			_cachedSteamIds = new List<int> { 4000 }; 
		}
	}
	private async Task<(string Title, string Price)> GetSteamMetadata( int steamId )
	{
		try
		{
			var response = await Http.RequestStringAsync( $"https://store.steampowered.com/api/appdetails?appids={steamId}" );
			if ( string.IsNullOrEmpty( response ) ) return (null, null);

			var nameMatch = System.Text.RegularExpressions.Regex.Match( response, @"""name"":\s*""([^""]+)""" );
			var priceMatch = System.Text.RegularExpressions.Regex.Match( response, @"""final_formatted"":\s*""([^""]+)""" );

			string title = nameMatch.Success ? nameMatch.Groups[1].Value : null;
        
			// If priceMatch fails, it's likely a free game or unpriced asset
			string price = priceMatch.Success ? priceMatch.Groups[1].Value : "PRICELESS";

			return (title, price);
		}
		catch ( Exception e )
		{
			Log.Warning( $"[AssetGenerator] Metadata Fetch Fail: {e.Message}" );
			return (null, "PRICELESS");
		}
	}
	public async Task<IdleMonData> RollNewAsset()
{
    // 1. Emergency Reload Check
    // If the list is empty, try to load it right now before proceeding.
    if ( _cachedSteamIds == null || _cachedSteamIds.Count == 0 )
    {
        Log.Warning( "⚠[AssetGenerator] List was empty at roll time! Attempting emergency reload..." );
        LoadBootstrapIds();
    }

    Log.Info( $" ROLL TRIGGERED! Pool Size: {_cachedSteamIds.Count} IDs." );

    var random = new Random();
    
    // 2. Pick a random ID from our bootstrap list
    int rolledSteamId = (_cachedSteamIds != null && _cachedSteamIds.Count > 0) 
        ? _cachedSteamIds[random.Next(_cachedSteamIds.Count)] 
        : 4000; // Hard fallback to GMod if still 0

    if ( rolledSteamId == 4000 && _cachedSteamIds.Count > 0 && !_cachedSteamIds.Contains(4000) )
    {
        Log.Error( "[AssetGenerator] Logic Error: Rolled 4000 but it's not in the list!" );
    }

    // 3. Market Scale (S) - Driven by the Global Market System
    float S = MarketServerSystem.GetCurrentScale();

    // 4. Dynamic Name & Price Generation (Flavor Only)
    var metadata = await GetSteamMetadata( rolledSteamId );
    string finalName;

    if ( !string.IsNullOrEmpty( metadata.Title ) )
    {
	    // Example: "Garry's Mod $9.99 #4000"
	    finalName = $"{metadata.Title} {metadata.Price} #{rolledSteamId}";
    }
    else
    {
	    // Fallback for missing data
	    string[] prefixes = { "Alpha", "Beta", "Sigma", "Delta", "Omega", "Prime", "Cyber", "Nano", "Void", "Flux" };
	    finalName = $"{prefixes[random.Next( prefixes.Length )]}-{random.Next( 100, 999 )} Node";
    }
    
    // 5. Stat Generation Logic
    double addition = Math.Round(random.NextDouble() * (BasePotential * S), 2);
    double multiplier = 1.0 + Math.Round(random.NextDouble() * (0.5 * S), 2);
    double subtraction = (random.NextDouble() < 0.4) 
        ? Math.Round(random.NextDouble() * (BasePotential * 0.5 * S), 2) 
        : 0;
    double division = (random.NextDouble() < 0.2) 
        ? 1.0 + Math.Round(random.NextDouble() * 0.5, 2) 
        : 1.0;
    double teamBonus = (random.NextDouble() < 0.1) 
        ? 1.0 + Math.Round(random.NextDouble() * 0.1, 2) 
        : 1.0;

    Log.Info( $" [AssetGenerator] Successfully rolled {finalName} with SteamID: {rolledSteamId}" );

    return new IdleMonData
    {
        ID = Guid.NewGuid(),
        Name = finalName,
        SteamId = rolledSteamId,
        ModelPath = "models/dev/box.vmdl",
        RolledAt = DateTime.UtcNow,
        MarketScaleAtBirth = S,
        Generation = 2,
        
        Addition = addition,
        Multiplier = multiplier,
        Subtraction = subtraction,
        Division = division,
        TeamBonus = teamBonus,
        Luck = Math.Round(random.NextDouble() * S, 2),
        CostEfficiency = (random.NextDouble() < 0.05) ? 0.05 : 0 
    };
}
}
