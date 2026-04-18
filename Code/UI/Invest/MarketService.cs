using Sandbox;
using Sandbox.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Sandbox;

public static class MarketService
{
    private const string OracleUrl = "https://b4block.org/";
    private const double BasePrice = 7126.0;
    private const double AnnualGrowth = 0.11; // 11% target
    private static readonly DateTime MarketEpoch = new DateTime(2026, 4, 18, 21, 52, 0, DateTimeKind.Utc);

    public static double CurrentPrice { get; private set; }
    public static bool IsReady { get; private set; } = false;

    /// <summary>
    /// Syncs with your Debian server to get the real UTC time, bypassing local clock spoofing.
    /// </summary>
    public static async Task Sync()
    {
        try
        {
            var response = await Http.RequestAsync(OracleUrl, "HEAD");
            if (response.Headers.TryGetValues("Date", out var values))
            {
                var serverTime = DateTime.Parse(values.First()).ToUniversalTime();
                
                // 1. Calculate the steady trend price
                double yearsElapsed = (serverTime - MarketEpoch).TotalDays / 365.25;
                double trendPrice = BasePrice * Math.Pow(1 + AnnualGrowth, yearsElapsed);

                // 2. Add deterministic "Noise" based on the current hour
                // This ensures every server has the same "dip" or "spike" each hour
                var hourSeed = new DateTime(serverTime.Year, serverTime.Month, serverTime.Day, serverTime.Hour, 0, 0).Ticks;
                var rng = new Random((int)(hourSeed % int.MaxValue));
                double hourlySwing = 1.0 + ((rng.NextDouble() - 0.5) * 0.06); // +/- 3%

                // 3. Apply a synchronized 15-second jitter for visual movement
                long window15s = serverTime.Ticks / TimeSpan.FromSeconds(15).Ticks;
                var jitterRng = new Random((int)(window15s % int.MaxValue));
                double jitter = 1.0 + ((jitterRng.NextDouble() - 0.5) * 0.004); // +/- 0.2%

                CurrentPrice = Math.Round(trendPrice * hourlySwing * jitter, 2);
                IsReady = true;
            }
        }
        catch (Exception e)
        {
            Log.Warning($"Fund.ino: Oracle Sync Failed. Check b4block.org SSL. {e.Message}");
            IsReady = false;
        }
    }
}
