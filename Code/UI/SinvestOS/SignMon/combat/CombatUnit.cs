using System;

namespace Sinvest;

/// <summary>
/// Transient runtime representation of a Sigmon inside an active combat encounter.
/// Holds all mutable combat state (current HP, stamina, alive status).
///
/// Persistence contract: this class is NEVER written to disk.
/// IdleMonData (the authoritative source) is only modified for post-combat
/// kill bonuses and victory bonuses — never for live HP tracking.
/// </summary>
public class CombatUnit
{
    // ---- Immutable References ----

    /// <summary>The persistent data snapshot this unit was created from.</summary>
    public IdleMonData Source { get; }

    /// <summary>Derived combat stats (computed once on construction).</summary>
    public CombatStatBlock Stats { get; }

    /// <summary>Position in the 6-slot grid (0-2 = front row, 3-5 = back row).</summary>
    public int SlotIndex { get; }

    /// <summary>True if this slot belongs to the enemy team.</summary>
    public bool IsEnemy { get; }

    // ---- Transient Mutable State ----

    public double CurrentHP      { get; set; }
    public double CurrentStamina { get; set; }

    /// <summary>A unit is alive while it has HP remaining.</summary>
    public bool IsAlive => CurrentHP > 0;

    /// <summary>Slots 0-2 are front row; slots 3-5 are back row.</summary>
    public bool IsFrontRow => SlotIndex < 3;

    /// <summary>
    /// How many kill-blows this unit delivered this combat.
    /// Used to determine eligibility for the victory stat bonus.
    /// </summary>
    public int KillCount { get; set; }

    // ---- Constructor ----

    public CombatUnit( IdleMonData source, int slotIndex, bool isEnemy = false )
    {
        Source   = source;
        SlotIndex = slotIndex;
        IsEnemy  = isEnemy;
        Stats    = new CombatStatBlock( source );

        CurrentHP      = Stats.MaxHP;
        CurrentStamina = Stats.MaxStamina;
        KillCount      = 0;
    }

    // ---- Post-Combat Reset ----

    /// <summary>
    /// Restores all transient pools after combat ends.
    /// Per spec: "Sigmon's pools get restored after each combat."
    /// The persistent IdleMonData is NOT touched here.
    /// </summary>
    public void RestoreAfterCombat()
    {
        CurrentHP      = Stats.MaxHP;
        CurrentStamina = Stats.MaxStamina;
        KillCount      = 0;
    }

    /// <summary>Regenerates stamina up to the pool cap. Called at end of each round.</summary>
    public void RegenStamina()
    {
        CurrentStamina = Math.Min( Stats.MaxStamina, CurrentStamina + Stats.StaminaRegen );
    }

    public override string ToString() =>
        $"[{(IsEnemy ? "Enemy" : "Player")} Slot {SlotIndex}] {Stats.Name} " +
        $"HP:{CurrentHP:F1}/{Stats.MaxHP:F1} STA:{CurrentStamina:F0}/{Stats.MaxStamina:F0}";
}
