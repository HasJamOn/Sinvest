using Sandbox; 
using Sinvest;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Sinvest;

/// <summary>
/// The authoritative host-side component responsible for global market synchronization.
/// It uses an external "Oracle" (b4block.org) to generate a deterministic, 
/// non-manipulatable market price based on server headers and time-based math.
/// </summary>
public sealed class MarketServiceSystem : Component
{
    public static MarketServiceSystem Instance { get; private set; }
    
    /* --- Oracle Configuration --- */
    // We use b4block.org's HTTP headers as a source of entropy
    private const string OracleUrl = "https://b4block.org/";
    // A private key used to prevent players from easily reverse-engineering the price formula
    private const int SecretSalt = 9928341; 
    
    /* --- Economic Constants --- */
    private const double BasePrice = 7126.0;
    private const double AnnualGrowth = 0.11; // 11% Average yearly return (Market Trend)
    
    // The "Genesis" date for the Sinvest economy
    private static readonly DateTime MarketEpoch = new DateTime( 2026, 4, 18, 21, 52, 0, DateTimeKind.Utc );

    /// <summary>
    /// Historical seeds used for calculating prices during specific eras of the game.
    /// </summary>
    private static readonly List<(DateTime Start, int Seed)> EraHistory = new List<(DateTime Start, int Seed)>()
    {
        (new DateTime(2025, 1, 1), 1234567), 
    };

    /* --- Networked State --- */
    [Sync] public double SyncedPrice { get; set; }
    [Sync] public int CurrentSeed { get; set; }
    [Sync] public DateTime CurrentEraStart { get; set; }
    
    private TimeUntil _nextSync = 0;
    
    protected override void OnStart()
    {
        // Immediate local calculation so the UI shows a value before the first network sync completes
        double initialPrice = CalculatePrice( DateTime.UtcNow, SecretSalt, false );
        FundinoMarketService.UpdatePrice( initialPrice );
    }
    
    protected override void OnAwake()
    {
        Instance = this;
    }
    
    /// <summary>
    /// Calculates the Market Scale Factor (S).
    /// Used by secondary systems (like IdleMon) to scale rewards based on market strength.
    /// S = 1.0 (Base), S > 1.0 (Bull Market), S < 1.0 (Bear Market).
    /// </summary>
    public static float GetCurrentScale()
    {
        if ( Instance == null ) return 1.0f;
    
        double scale = Instance.SyncedPrice / BasePrice;
        return (float)Math.Max( 0.1, scale ); 
    }

    protected override void OnUpdate()
    {
        // Client-side Logic: Sync local service with the networked property
        if ( IsProxy ) 
        {
           if ( FundinoMarketService.CurrentPrice != SyncedPrice )
           {
              FundinoMarketService.UpdatePrice( SyncedPrice );
           }
           return;
        }

        // Host-side Logic: Polling the Oracle every 15 seconds
        if ( _nextSync )
        {
            _ = PerformGlobalSync();
            _nextSync = 15f; 
        }
    }

    /// <summary>
    /// Connects to the external Oracle, extracts entropy from HTTP headers, 
    /// and triggers a re-calculation of the global price.
    /// </summary>
    private async Task PerformGlobalSync()
    {
        try 
        {
            // We use a HEAD request to save bandwidth while still getting ETag/Date headers
            var response = await Http.RequestAsync( OracleUrl, "HEAD" );
            
            // The ETag serves as our unique, server-provided random seed
            string eTag = response.Headers
                .FirstOrDefault( h => h.Key.Equals( "ETag", StringComparison.OrdinalIgnoreCase ) )
                .Value?.FirstOrDefault();

            // The Date header ensures all clients are synced to the same "World Clock"
            string dateHeader = response.Headers
                .FirstOrDefault( h => h.Key.Equals( "Date", StringComparison.OrdinalIgnoreCase ) )
                .Value?.FirstOrDefault();

            if ( string.IsNullOrEmpty( eTag ) || string.IsNullOrEmpty( dateHeader ) )
            {
                Log.Warning( "[Market] Required headers missing from b4block.org!" );
                return;
            }

            var serverTime = DateTime.Parse( dateHeader ).ToUniversalTime();
            
            // Mix the Oracle's entropy with our SecretSalt
            CurrentSeed = eTag.GetHashCode() ^ SecretSalt;
            CurrentEraStart = MarketEpoch;

            double price = CalculatePrice( serverTime, CurrentSeed, true );

            // Update networked state (broadcasts to all proxies)
            SyncedPrice = price;
            FundinoMarketService.UpdatePrice( price );

             Log.Info( $"[Market] Live: ${SyncedPrice}" );
        }
        catch ( Exception e )
        {
            Log.Error( $"[Market] Sync Error: {e.Message}" );
        }
    }

    /// <summary>
    /// The Core Market Formula. 
    /// Generates a deterministic price based on Trend (Growth) + Swing (Hourly) + Jitter (Noise).
    /// </summary>
    public static double CalculatePrice( DateTime time, int seed, bool applyJitter = false )
    {
        // 1. Trend: Compound interest based on time elapsed since the Epoch
        double yearsElapsed = (time - MarketEpoch).TotalDays / 365.25;
        double trendPrice = BasePrice * Math.Pow( 1 + AnnualGrowth, yearsElapsed );

        // 2. Swing: A +/- 3% hourly fluctuation based on the current hour and seed
        var hourSeed = new DateTime( time.Year, time.Month, time.Day, time.Hour, 0, 0 ).Ticks ^ seed;
        var rng = new Random( (int)(hourSeed % int.MaxValue) );
        double hourlySwing = 1.0 + ((rng.NextDouble() - 0.5) * 0.06);

        // 3. Jitter: Subtle +/- 0.2% noise that updates every 15 seconds to make the UI feel "live"
        double jitter = 1.0;
        if ( applyJitter )
        {
            long window15s = time.Ticks / TimeSpan.FromSeconds( 15 ).Ticks;
            var jitterRng = new Random( (int)(window15s % int.MaxValue) ^ seed );
            jitter = 1.0 + ((jitterRng.NextDouble() - 0.5) * 0.004);
        }

        return Math.Round( trendPrice * hourlySwing * jitter, 2 );
    }

    /// <summary>
    /// Resolves which seed to use for a specific point in time, supporting historical era data.
    /// </summary>
    public static int GetSeedAtTime( DateTime time, int liveSeed, DateTime liveStart )
    {
        if ( time >= liveStart ) return liveSeed;
        
        var era = EraHistory.OrderByDescending( e => e.Start ).FirstOrDefault( e => time >= e.Start );
        
        if ( era.Seed != 0 ) return era.Seed;
        return liveSeed; 
    }
}
