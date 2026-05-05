using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Sinvest;

/// <summary>
/// Responsible for the procedural generation of IdleMon assets.
/// Combines file parsing, Steam API metadata fetching, and randomized stat rolling.
/// </summary>
public sealed class AssetGenerator : Component
{
    [Property] public float BasePotential { get; set; } = 10.0f;
    
    private List<int> _cachedSteamIds = new();

    protected override void OnStart()
    {
       LoadBootstrapIds();
    }

    /// <summary>
    /// Parses a flat text file from the shaders directory to populate the valid Steam ID pool.
    /// Supports multiple delimiters for flexibility in data entry.
    /// </summary>
    private void LoadBootstrapIds()
    {
       string path = "shaders/data/bootstrap_ids.txt";
       
       if ( FileSystem.Mounted.FileExists( path ) )
       {
          var content = FileSystem.Mounted.ReadAllText( path );
       
          _cachedSteamIds = content.Split( new[] { ",", "\r\n", "\r", "\n", " " }, StringSplitOptions.RemoveEmptyEntries )
             .Select( x => x.Trim() )
             .Select( x => int.TryParse( x, out int id ) ? id : -1 )
             .Where( id => id != -1 )
             .ToList();
           
          Log.Info( $"[AssetGenerator] Successfully parsed {_cachedSteamIds.Count} Steam IDs." );
       }
       else
       {
          Log.Error( $"[AssetGenerator] Could not find bootstrap_ids.txt at {path}" );
          _cachedSteamIds = new List<int> { 4000 }; // Fallback to Garry's Mod AppID
       }
    }

    /// <summary>
    /// Fetches live application metadata from the Steam Store API.
    /// Uses regex to extract the Name and Price without requiring heavy JSON libraries.
    /// </summary>
    private async Task<(string Title, string Price)> GetSteamMetadata( int steamId )
    {
       try
       {
          var response = await Http.RequestStringAsync( $"https://store.steampowered.com/api/appdetails?appids={steamId}" );
          if ( string.IsNullOrEmpty( response ) ) return (null, null);

          // Lightweight extraction for performance within the s&box runtime
          var nameMatch = System.Text.RegularExpressions.Regex.Match( response, @"""name"":\s*""([^""]+)""" );
          var priceMatch = System.Text.RegularExpressions.Regex.Match( response, @"""final_formatted"":\s*""([^""]+)""" );

          string title = nameMatch.Success ? nameMatch.Groups[1].Value : null;
          string price = priceMatch.Success ? priceMatch.Groups[1].Value : "PRICELESS";

          return (title, price);
       }
       catch ( Exception e )
       {
          Log.Warning( $"[AssetGenerator] Metadata Fetch Fail: {e.Message}" );
          return (null, "PRICELESS");
       }
    }

    /// <summary>
    /// Generates a unique IdleMonData instance.
    /// Stat weightings are influenced by the current MarketScale (S).
    /// </summary>
    public async Task<IdleMonData> RollNewAsset()
    {
        if ( _cachedSteamIds == null || _cachedSteamIds.Count == 0 )
        {
            LoadBootstrapIds();
        }

        var random = new Random();
        
        int rolledSteamId = (_cachedSteamIds != null && _cachedSteamIds.Count > 0) 
            ? _cachedSteamIds[random.Next(_cachedSteamIds.Count)] 
            : 4000;

        // Fetch current economy scale for stat weighting
        float S = MarketServiceSystem.GetCurrentScale();

        var metadata = await GetSteamMetadata( rolledSteamId );
        string finalName;

        if ( !string.IsNullOrEmpty( metadata.Title ) )
        {
            finalName = $"{metadata.Title} {metadata.Price} #{rolledSteamId}";
        }
        else
        {
            string[] prefixes = { "Alpha", "Beta", "Sigma", "Delta", "Omega", "Prime", "Cyber", "Nano", "Void", "Flux" };
            finalName = $"{prefixes[random.Next( prefixes.Length )]}-{random.Next( 100, 999 )} Node";
        }
        
        // --- STAT GENERATION LOGIC ---
        // Addition: Pure PPS increase scaled by Market Potential
        double addition = Math.Round(random.NextDouble() * (BasePotential * S), 2);
        
        // Multiplier: Percentage boost (1.0 to 1.5 base)
        double multiplier = 1.0 + Math.Round(random.NextDouble() * (0.5 * S), 2);
        
        // Subtraction: 40% chance of a "Cursed" flat penalty
        double subtraction = (random.NextDouble() < 0.4) 
            ? Math.Round(random.NextDouble() * (BasePotential * 0.5 * S), 2) 
            : 0;
            
        // Division: 20% chance of an efficiency penalty (Higher is worse)
        double division = (random.NextDouble() < 0.2) 
            ? 1.0 + Math.Round(random.NextDouble() * 0.5, 2) 
            : 1.0;
            
        // TeamBonus: Rare (10%) synergy multiplier
        double teamBonus = (random.NextDouble() < 0.1) 
            ? 1.0 + Math.Round(random.NextDouble() * 0.1, 2) 
            : 1.0;

        return new IdleMonData
        {
            ID = Guid.NewGuid(), // Primary key for roster identity
            Name = finalName,
            SteamId = rolledSteamId,
            
            ModelPath = null, 
            PoolCategory = "standard", 

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
