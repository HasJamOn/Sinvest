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
		if ( ActiveNodes == null || ActiveNodes.Count == 0 ) return 0;

		double sumA = ActiveNodes.Sum( n => n.Addition );
		double sumS = ActiveNodes.Sum( n => n.Subtraction );
    
		double prodD = ActiveNodes.Aggregate( 1.0, ( acc, n ) => acc * ( n.Division ) );
		double prodM = ActiveNodes.Aggregate( 1.0, ( acc, n ) => acc * ( n.Multiplier ) );
		double prodT = ActiveNodes.Aggregate( 1.0, ( acc, n ) => acc * ( n.TeamBonus ) );

		// DIAGNOSTIC LOG: If yield is 0 but we have Additions, find out why.
		if ( sumA > 0 )
		{
			if ( prodM == 0 ) Log.Warning( "MATH ERROR: Multiplier Product is 0! Check your Empty nodes." );
			if ( prodT == 0 ) Log.Warning( "MATH ERROR: TeamBonus Product is 0!" );
			if ( prodD == 0 ) Log.Warning( "MATH ERROR: Division Product is 0! (Division by zero risk)" );
		}

		double basePool = Math.Max( 0, sumA - sumS );
		return ( basePool / (prodD == 0 ? 1 : prodD) ) * prodM * prodT;
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
	/// <summary>
	/// Replaces a node at a specific index with a new candidate.
	/// </summary>
	public void SwapNode(int index, IdleMonData candidate)
	{
		if (index < 0 || index >= ActiveNodes.Count) return;

		ActiveNodes[index] = candidate;
		Log.Info($"[ROSTER] Swapped Slot {index} for {candidate.Name}");
    
		// In Step 3, we will trigger the Cloud Sync here!
	}
	/// <summary>
	/// Returns an array of 6 doubles representing the CHANGE in yield for each slot
	/// if it were replaced by the candidate.
	/// </summary>
	public double[] GetSwapDeltas( IdleMonData candidate )
	{
		double[] deltas = new double[6];
    
		// Safety: If the list isn't set up yet, return zeros
		if ( ActiveNodes == null || ActiveNodes.Count < 6 ) 
			return deltas;

		double currentYield = CalculateTotalYield();

		for ( int i = 0; i < 6; i++ )
		{
			// Double check this specific index exists
			if ( i >= ActiveNodes.Count ) continue;

			var previousMon = ActiveNodes[i];
			ActiveNodes[i] = candidate;
        
			double projectedYield = CalculateTotalYield();
			deltas[i] = projectedYield - currentYield;
        
			ActiveNodes[i] = previousMon;
		}

		return deltas;
	}
}
