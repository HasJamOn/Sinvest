using Sandbox; 
using Sinvest;
using Sandbox.Services;
using System.Threading.Tasks;

namespace Sinvest;

public sealed class EconomyManager : Component
{
	// Allows ComputerHUD to find this component easily
	public static EconomyManager Instance { get; private set; }
	public double CurrentSharePrice => MarketService.CurrentPrice;

	protected override void OnAwake()
	{
		Instance = this;
	}

	/// <summary>
	/// This is the method the HUD is looking for.
	/// It handles the actual communication with s&box cloud services.
	/// </summary>
	public async void CommitTransaction(double moneyDelta, double sharesDelta)
	{
		Stats.Increment("money", moneyDelta);
		Stats.Increment("fundino_shares", sharesDelta);

		// In s&box, if FlushAsync fails, it logs to the console anyway.
		// Removing the manual try-catch here stops the ExceptionDispatchInfo error.
		await Stats.FlushAsync();
	}
}
