using System;

namespace Sinvest;

/// <summary>
/// The ASMD Bridge. Derives all transient combat statistics from a persistent
/// IdleMonData struct without reading or writing any fields on it.
///
/// Economic → Combat Mapping:
///   Addition     → Base Health Pool
///   Subtraction  → Base Attack Power (the damage floor)
///   Multiplier   → Boosts one stat per generation cycle (Gen % 6 rotation)
///   Division     → Damage Resistance % (asymptotic, capped at 90%)
///   Luck         → Routes to Crit / Accuracy / Dodge (Gen % 3 rotation)
///   TeamBonus    → Passive multiplier applied to all derived stats
///   CostEfficiency → Max Stamina + Stamina Regen per turn
/// </summary>
public readonly struct CombatStatBlock
{
    // ---- Identity ----
    public readonly Guid   ID;
    public readonly string Name;

    // ---- Derived Combat Values ----
    public readonly double MaxHP;
    public readonly double AttackPower;
    public readonly double DamageResistance; // [0.0 – 0.90]
    public readonly double CritChance;       // [0.0 – 0.75]
    public readonly double AccuracyChance;   // [0.50 – 1.00]
    public readonly double DodgeChance;      // [0.0 – 0.60]
    public readonly double MaxStamina;
    public readonly double StaminaRegen;     // Restored each turn on Guard / end-of-round

    // ---- Scaling Constants ----
    private const double BASE_HP               = 50.0;
    private const double HP_PER_ADDITION       = 10.0;
    private const double BASE_ATTACK           = 5.0;
    private const double ATTACK_PER_SUBTRACTION = 5.0;
    private const double BASE_STAMINA          = 100.0;
    private const double STAMINA_PER_COSTEFF   = 200.0;
    private const double REGEN_PER_COSTEFF     = 50.0;
    private const double BASE_REGEN            = 10.0;
    private const double BASE_CRIT             = 0.05;
    private const double BASE_ACCURACY         = 0.90;
    private const double BASE_DODGE            = 0.05;
    private const double LUCK_TO_CRIT          = 0.04; // +4% crit per Luck point
    private const double LUCK_TO_ACCURACY      = 0.02; // +2% accuracy per Luck point
    private const double LUCK_TO_DODGE         = 0.02; // +2% dodge per Luck point
    private const double MAX_RESISTANCE        = 0.90;

    public CombatStatBlock( IdleMonData data )
    {
        ID   = data.ID;
        Name = data.Name ?? "Unknown";

        // Generation cycles determine which stat Multiplier amplifies.
        //   Gen % 6 == 1 → Addition (HP)
        //   Gen % 6 == 2 → Subtraction (Attack)
        //   Gen % 6 == 3 → Division (Resistance)
        //   Gen % 6 == 4 → Luck
        //   Gen % 6 == 5 → CostEfficiency
        //   Gen % 6 == 0 → Luck (same as 4, wraps)
        int gen     = Math.Max( 1, data.Generation );
        int genMod6 = gen % 6;
        int genMod3 = gen % 3;

        double effectiveAddition    = data.Addition;
        double effectiveSubtraction = data.Subtraction;
        double effectiveDivision    = data.Division;
        double effectiveLuck        = data.Luck;
        double effectiveCostEff     = data.CostEfficiency;

        // Apply generation-based Multiplier bonus to the appropriate stat
        switch ( genMod6 )
        {
            case 1: effectiveAddition    *= data.Multiplier; break;
            case 2: effectiveSubtraction *= data.Multiplier; break;
            case 3: effectiveDivision    *= data.Multiplier; break;
            case 4: effectiveLuck        *= data.Multiplier; break;
            case 5: effectiveCostEff     *= data.Multiplier; break;
            case 0: effectiveLuck        *= data.Multiplier; break;
        }

        // TeamBonus passively scales all derived values (matches economic formula intent)
        double teamScale = Math.Max( 0.0, data.TeamBonus );

        // ---- HP ----
        MaxHP = Math.Max( 1.0, (BASE_HP + effectiveAddition * HP_PER_ADDITION) * teamScale );

        // ---- Attack Power ----
        // Subtraction is a cursed economic stat — in combat it becomes raw damage potential.
        // A "Rusty" asset is a glass cannon: poor income, devastating attacker.
        AttackPower = Math.Max( 1.0, (BASE_ATTACK + effectiveSubtraction * ATTACK_PER_SUBTRACTION) * teamScale );

        // ---- Damage Resistance ----
        // Division 1.0 → 0%, 2.0 → 50%, 10.0 → 90% (asymptotic, never reaches 100%).
        double rawResist = 1.0 - (1.0 / Math.Max( 1.0, effectiveDivision ));
        DamageResistance = Math.Min( MAX_RESISTANCE, rawResist );

        // ---- Luck Routing (Gen % 3 cycle) ----
        //   Mod 1 → Crit Chance
        //   Mod 2 → Accuracy
        //   Mod 0 → Dodge Chance
        CritChance     = BASE_CRIT;
        AccuracyChance = BASE_ACCURACY;
        DodgeChance    = BASE_DODGE;

        switch ( genMod3 )
        {
            case 1: CritChance     += effectiveLuck * LUCK_TO_CRIT;     break;
            case 2: AccuracyChance += effectiveLuck * LUCK_TO_ACCURACY; break;
            case 0: DodgeChance    += effectiveLuck * LUCK_TO_DODGE;    break;
        }

        CritChance     = Math.Clamp( CritChance,     0.0, 0.75 );
        AccuracyChance = Math.Clamp( AccuracyChance, 0.5, 1.00 );
        DodgeChance    = Math.Clamp( DodgeChance,    0.0, 0.60 );

        // ---- Stamina (Special Move Pool) ----
        MaxStamina   = BASE_STAMINA + effectiveCostEff * STAMINA_PER_COSTEFF;
        StaminaRegen = BASE_REGEN   + effectiveCostEff * REGEN_PER_COSTEFF;
    }

    /// <summary>
    /// Returns a human-readable stat summary for debug/editor display.
    /// </summary>
    public string ToDebugString() =>
        $"{Name} | HP:{MaxHP:F1}  ATK:{AttackPower:F1}  RES:{DamageResistance:P0}  " +
        $"CRIT:{CritChance:P0}  ACC:{AccuracyChance:P0}  DODGE:{DodgeChance:P0}  " +
        $"STA:{MaxStamina:F0}({StaminaRegen:F1}/t)";
}
