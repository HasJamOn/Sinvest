using Sandbox; 
using Sinvest;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Sinvest;

public sealed class MarketServerSystem : Component
{
	public static MarketServerSystem Instance { get; private set; }
	
    private const string OracleUrl = "https://b4block.org/";
    private const int SecretSalt = 9928341; 
    private const double BasePrice = 7126.0;
    private const double AnnualGrowth = 0.11;
    
    private static readonly DateTime MarketEpoch = new DateTime( 2026, 4, 18, 21, 52, 0, DateTimeKind.Utc );

    // FIXED: Explicitly typed list for s&box compiler compatibility
    private static readonly List<(DateTime Start, int Seed)> EraHistory = new List<(DateTime Start, int Seed)>()
    {
        (new DateTime(2025, 1, 1), 1234567), 
    };

    [Sync] public double SyncedPrice { get; set; }
    [Sync] public int CurrentSeed { get; set; }
    [Sync] public DateTime CurrentEraStart { get; set; }
    
    private TimeUntil _nextSync = 0;
    
    protected override void OnAwake()
    {
	    Instance = this;
    }
    
    /// <summary>
    /// Returns the current Market Scale Factor (S).
    /// 1.0 means the market is at BasePrice. 2.0 means it has doubled.
    /// </summary>
    public static float GetCurrentScale()
    {
	    if ( Instance == null ) return 1.0f;
    
	    // S = CurrentPrice / BasePrice
	    double scale = Instance.SyncedPrice / BasePrice;
    
	    // We cast to float for storage efficiency in the IdleMonData struct
	    return (float)Math.Max( 0.1, scale ); 
    }

    protected override void OnUpdate()
    {
	    if ( IsProxy ) 
	    {
		    // Only update if the networked price changed
		    if ( FundinoMarketService.CurrentPrice != SyncedPrice )
		    {
			    FundinoMarketService.UpdatePrice( SyncedPrice );
		    }
		    return;
	    }

        if ( _nextSync )
        {
            _ = PerformGlobalSync();
            _nextSync = 15f; 
        }
    }

    private async Task PerformGlobalSync()
    {
        try 
        {
            var response = await Http.RequestAsync( OracleUrl, "HEAD" );
            
            string eTag = response.Headers
                .FirstOrDefault( h => h.Key.Equals( "ETag", StringComparison.OrdinalIgnoreCase ) )
                .Value?.FirstOrDefault();

            string dateHeader = response.Headers
                .FirstOrDefault( h => h.Key.Equals( "Date", StringComparison.OrdinalIgnoreCase ) )
                .Value?.FirstOrDefault();

            if ( string.IsNullOrEmpty( eTag ) || string.IsNullOrEmpty( dateHeader ) )
            {
                Log.Warning( "[Market] Required headers missing from b4block.org!" );
                return;
            }

            var serverTime = DateTime.Parse( dateHeader ).ToUniversalTime();
            
            CurrentSeed = eTag.GetHashCode() ^ SecretSalt;
            CurrentEraStart = MarketEpoch;

            double price = CalculatePrice( serverTime, CurrentSeed, true );

            SyncedPrice = price;
            FundinoMarketService.UpdatePrice( price );

             Log.Info( $"[Market] Live: ${SyncedPrice}" );
        }
        catch ( Exception e )
        {
            Log.Error( $"[Market] Sync Error: {e.Message}" );
        }
    }

    public static double CalculatePrice( DateTime time, int seed, bool applyJitter = false )
    {
        double yearsElapsed = (time - MarketEpoch).TotalDays / 365.25;
        double trendPrice = BasePrice * Math.Pow( 1 + AnnualGrowth, yearsElapsed );

        var hourSeed = new DateTime( time.Year, time.Month, time.Day, time.Hour, 0, 0 ).Ticks ^ seed;
        var rng = new Random( (int)(hourSeed % int.MaxValue) );
        double hourlySwing = 1.0 + ((rng.NextDouble() - 0.5) * 0.06);

        double jitter = 1.0;
        if ( applyJitter )
        {
            long window15s = time.Ticks / TimeSpan.FromSeconds( 15 ).Ticks;
            var jitterRng = new Random( (int)(window15s % int.MaxValue) ^ seed );
            jitter = 1.0 + ((jitterRng.NextDouble() - 0.5) * 0.004);
        }

        return Math.Round( trendPrice * hourlySwing * jitter, 2 );
    }

    public static int GetSeedAtTime( DateTime time, int liveSeed, DateTime liveStart )
    {
        if ( time >= liveStart ) return liveSeed;
        
        // FIXED: Added check to prevent errors if EraHistory is accessed incorrectly
        var era = EraHistory.OrderByDescending( e => e.Start ).FirstOrDefault( e => time >= e.Start );
        
        if ( era.Seed != 0 ) return era.Seed;
        return liveSeed; // Fallback to live if no era found
    }
}
