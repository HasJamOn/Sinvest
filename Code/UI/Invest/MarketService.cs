using Sandbox;
using System;
using System.Linq;
using System.Collections.Generic;

namespace Sandbox;

public static class MarketService
{
	/// <summary>
	/// The current price synced from the MarketServerSystem.
	/// </summary>
	public static double CurrentPrice { get; set; }

	/// <summary>
	/// A rolling history of the last 100 price points for the UI graph.
	/// </summary>
	public static List<double> PriceHistory { get; set; } = new List<double>();

	/// <summary>
	/// Returns true if we have received a valid price from the server.
	/// </summary>
	public static bool IsReady => CurrentPrice > 0;

	/// <summary>
	/// Called by MarketServerSystem to push new data to the client.
	/// </summary>
	public static void UpdatePrice( double newPrice )
	{
		// Don't add to history if the price hasn't changed
		if ( CurrentPrice == newPrice && PriceHistory.Count > 0 ) 
			return;

		CurrentPrice = newPrice;
        
		PriceHistory.Add( newPrice );

		// Keep the history buffer at a fixed size of 100 points
		if ( PriceHistory.Count > 100 ) 
		{
			PriceHistory.RemoveAt( 0 );
		}
	}
}
