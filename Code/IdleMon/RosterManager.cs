using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sinvest;

public sealed class RosterManager : Component
{
	// The "Source of Truth" for your active 6 IdleMons
	[Property] public List<IdleMonData> ActiveNodes { get; set; } = new();

	protected override void OnStart()
	{
		// Fill the roster with empty nodes if it's a new scene
		if (ActiveNodes.Count == 0)
		{
			for (int i = 0; i < 6; i++)
			{
				ActiveNodes.Add(IdleMonData.Empty);
			}
		}
	}

	public double CalculateTotalYield()
	{
		// G = [max(0, (ΣA - ΣS) / ΠD)] * ΠM * ΠT
		double sumA = ActiveNodes.Sum(n => n.Addition);
		double sumS = ActiveNodes.Sum(n => n.Subtraction);
        
		// We use Math.Max(1.0, ...) to prevent dividing by zero or multiplying by zero
		double prodD = ActiveNodes.Aggregate(1.0, (acc, n) => acc * Math.Max(1.0, n.Division));
		double prodM = ActiveNodes.Aggregate(1.0, (acc, n) => acc * Math.Max(1.0, n.Multiplier));
		double prodT = ActiveNodes.Aggregate(1.0, (acc, n) => acc * Math.Max(1.0, n.TeamBonus));

		double basePool = Math.Max(0, sumA - sumS);
		return (basePool / prodD) * prodM * prodT;
	}

	protected override void OnUpdate()
	{
		// Draw the math result on screen so we can see it live
		double currentG = CalculateTotalYield();
        
		Gizmo.Draw.ScreenText( 
			$"--- IDLEMON.INO DEBUG ---\n" +
			$"Yield (G): {currentG:F2} bucks/sec\n" +
			$"Active Nodes: {ActiveNodes.Count(x => x.ID != Guid.Empty)}/6", 
			new Vector2( 50, 50 ) 
		);
	}
}
