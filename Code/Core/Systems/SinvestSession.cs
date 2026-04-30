using Sandbox;
using System;

namespace Sinvest;

/// <summary>
/// Persistent session manager that survives scene transitions.
/// Tracks which character slot the local player is currently using.
/// </summary>
public static class SinvestSession
{
	// The currently selected character slot. Default to 1 to prevent uninitialized errors.
	public static int ActiveSlot { get; set; } = 1;
	private const int FallbackSalt = 9928341;
	
	/// <summary>
	/// Generates a tamper-proof signature based on the core economy variables and a specific seed.
	/// </summary>
	public static string GenerateSignature( double money, double shares, double idleBucks, int seed )
	{
		// If somehow a 0 seed is passed, use the fallback to prevent zero-math errors
		if ( seed == 0 ) seed = FallbackSalt;

		long combined = (long)(money * 13.37) ^ 
		                (long)(shares * 17.11) ^ 
		                (long)(idleBucks * 19.99) ^ 
		                (long)seed ^ 
		                ActiveSlot; // Anchors the save to this specific slot

		return combined.ToString( "X" );
	}

	/// <summary>
	/// Appends the active slot suffix to a base cloud key.
	/// Example: GetSlotKey("money") returns "money_1" if ActiveSlot is 1.
	/// </summary>
	public static string GetSlotKey( string baseKey )
	{
		return $"{baseKey}_{ActiveSlot}";
	}

	/// <summary>
	/// Generates a suffix key for a specific slot, independent of the ActiveSlot.
	/// This is heavily used by the Razor UI to preview all slots before selecting one.
	/// </summary>
	public static string GetKeyForSlot( string baseKey, int slotIndex )
	{
		// Enforce the 3-slot capacity constraint 
		slotIndex = Math.Clamp( slotIndex, 1, 3 );
		return $"{baseKey}_{slotIndex}";
	}

	/// <summary>
	/// Call this when the player disconnects or returns to the main menu 
	/// to ensure the state is clean for the next session.
	/// </summary>
	public static void ClearSession()
	{
		ActiveSlot = 1;
	}
}
