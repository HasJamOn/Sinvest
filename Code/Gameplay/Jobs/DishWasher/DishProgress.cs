using System.Collections.Generic;
using Sandbox;

namespace Sinvest;

public class DishProgress
{
	public List<float> AlphaValues = new();
	public List<Vector3> LocalPoints = new();

	/// <summary>
	/// Saved so the HUD can show correct progress after switching back to a dish.
	/// </summary>
	public int SavedScrubsApplied;
	public int SavedScrubsNeeded;

	public bool IsInitialized = false;
}
