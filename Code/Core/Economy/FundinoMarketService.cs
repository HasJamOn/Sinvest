using Sandbox; 
using Sinvest;
using System;
using System.Linq;
using System.Collections.Generic;

namespace Sinvest;

/// <summary>
/// A client-side service that tracks the Fundino share price. 
/// It acts as a middleware between the MarketServerSystem and the UI, 
/// providing a historical window of price data for graphing.
/// </summary>
public static class FundinoMarketService
{
    /// <summary>
    /// The most recently synchronized share price from the server.
    /// Used by the EconomyManager to calculate transaction totals.
    /// </summary>
    public static double CurrentPrice { get; set; }

    /// <summary>
    /// A rolling buffer of the last 100 price points. 
    /// This provides the data source for UI trend graphs and volatility displays.
    /// </summary>
    public static List<double> PriceHistory { get; set; } = new List<double>();

    /// <summary>
    /// Indicates if the market has successfully synchronized with the server 
    /// and is ready to process transactions.
    /// </summary>
    public static bool IsReady => CurrentPrice > 0;

    /// <summary>
    /// Receives new price data from the MarketServerSystem.
    /// Updates the current state and maintains the rolling history buffer.
    /// </summary>
    /// <param name="newPrice">The new price point sent by the server.</param>
    public static void UpdatePrice( double newPrice )
    {
       // Performance Optimization: Ignore updates that don't change the price.
       // We use a small epsilon (0.001) to account for floating-point precision noise.
       if ( Math.Abs( CurrentPrice - newPrice ) < 0.001 && PriceHistory.Count > 0 ) 
          return;

       CurrentPrice = newPrice;
       PriceHistory.Add( newPrice );

       // Keep the history window capped at 100 entries to prevent memory growth.
       // This ensures the UI graph remains performant even after long sessions.
       if ( PriceHistory.Count > 100 ) 
       {
          PriceHistory.RemoveAt( 0 );
       }
    }
}
