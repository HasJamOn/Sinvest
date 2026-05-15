namespace Sinvest;

/// <summary>
/// Defines how a special attack chooses its targets.
/// </summary>
public enum TargetMode
{
    /// <summary>Player picks a specific enemy slot.</summary>
    SingleEnemy,

    /// <summary>Hits a randomly chosen living enemy slot.</summary>
    RandomEnemy,

    /// <summary>Hits every living enemy unit simultaneously.</summary>
    AllEnemies,

    /// <summary>Targets the user (heals, buffs, etc.).</summary>
    Self,
}

/// <summary>
/// Defines a named special attack entry in the combat skill menu.
///
/// Design intent: this catalogue is the single place to register new skills.
/// CombatManager reads from Catalogue[] — no code changes needed elsewhere
/// to add new entries.
/// </summary>
public sealed class SpecialAttackDefinition
{
    public string     Name             { get; }
    public string     Description      { get; }

    /// <summary>
    /// Multiplier applied to the attacker's base AttackPower.
    /// 1.0 = 100% (normal hit), 1.2 = 120% (Power Strike), etc.
    /// </summary>
    public double DamageMultiplier { get; }

    /// <summary>Stamina cost deducted from the user before the attack resolves.</summary>
    public double StaminaCost      { get; }

    public TargetMode Targeting    { get; }

    public SpecialAttackDefinition(
        string name,
        string description,
        double damageMultiplier,
        double staminaCost,
        TargetMode targeting = TargetMode.SingleEnemy )
    {
        Name             = name;
        Description      = description;
        DamageMultiplier = damageMultiplier;
        StaminaCost      = staminaCost;
        Targeting        = targeting;
    }

    // -------------------------------------------------------------------------
    //  SPECIAL ATTACK CATALOGUE
    //  Add new entries here. CombatManager and UI iterate this array at runtime.
    // -------------------------------------------------------------------------
    public static readonly SpecialAttackDefinition[] Catalogue = new[]
    {
        // ---- Proof of Concept ----
        new SpecialAttackDefinition(
            name:             "Power Strike",
            description:      "A focused, devastating strike that deals 120% normal damage.",
            damageMultiplier: 1.20,
            staminaCost:      20.0,
            targeting:        TargetMode.SingleEnemy
        ),

        // ---- Placeholder slots for future expansion ----
        // new SpecialAttackDefinition(
        //     "Scatter Shot",
        //     "Unleashes a volley that hits all enemies for 70% damage.",
        //     0.70, 35.0, TargetMode.AllEnemies ),

        // new SpecialAttackDefinition(
        //     "Chaos Bolt",
        //     "Fires a chaotic blast at a random target for 150% damage.",
        //     1.50, 45.0, TargetMode.RandomEnemy ),

        // new SpecialAttackDefinition(
        //     "Rally",
        //     "The user steels itself, recovering 40 stamina.",
        //     0.0, 0.0, TargetMode.Self ),
    };
}
