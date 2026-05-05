using Sandbox;
using System;

namespace Sinvest;

/// <summary>
/// A persistent, static session manager that tracks the active character slot.
/// This utility bridges the gap between the Main Menu selection and the 
/// authoritative ledger system across scene transitions.
/// </summary>
public static class SinvestSession
{
    /// <summary> 
    /// The currently active save slot (1, 2, or 3). 
    /// Defaults to 1 to ensure a valid path is always available for the FileSystem.
    /// </summary>
    public static int ActiveSlot { get; set; } = 1;

    // Shared with MarketServiceSystem for deterministic calculations.
    private const int FallbackSalt = 9928341;
    
    /// <summary>
    /// Creates a hexadecimal hash of the current session state.
    /// This is used as a lightweight integrity check to detect if the local 
    /// plaintext ledger has been manually altered without a corresponding signature update.
    /// </summary>
    public static string GenerateSignature( double money, double shares, double idleBucks, int seed )
    {
       // Ensure we never perform bitwise operations against a zero seed.
       if ( seed == 0 ) seed = FallbackSalt;

       // Use prime-number multipliers to create a unique bit-spread for the signature.
       long combined = (long)(money * 13.37) ^ 
                       (long)(shares * 17.11) ^ 
                       (long)(idleBucks * 19.99) ^ 
                       (long)seed ^ 
                       ActiveSlot; // Salt the hash with the Slot ID to prevent cross-slot copying.

       return combined.ToString( "X" );
    }

    /// <summary>
    /// Formats a string key for cloud storage or local lookups based on the ActiveSlot.
    /// Useful for Phase 2: Cloud Mirroring.
    /// </summary>
    public static string GetSlotKey( string baseKey )
    {
       return $"{baseKey}_{ActiveSlot}";
    }

    /// <summary>
    /// Generates a slot-specific key for any index. 
    /// Primary use case: Populating the "Character Select" UI with data from all 3 slots.
    /// </summary>
    public static string GetKeyForSlot( string baseKey, int slotIndex )
    {
       // Sinvest strictly supports 3 character slots.
       slotIndex = Math.Clamp( slotIndex, 1, 3 );
       return $"{baseKey}_{slotIndex}";
    }

    /// <summary>
    /// Resets the session state. Should be called when the player exits to the 
    /// main menu to prevent data bleed into the next session.
    /// </summary>
    public static void ClearSession()
    {
       ActiveSlot = 1;
    }
}
