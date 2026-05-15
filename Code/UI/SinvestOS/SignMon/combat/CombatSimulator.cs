using Sandbox;
using System;
using System.Text;

namespace Sinvest;

/// <summary>
/// Editor balance testing tool. Acts as a non-persistent "Referee."
///
/// Configure two Sigmon stat profiles in the Inspector, set a run count,
/// then call RunSimulation() (via CombatDevTools or directly). Results appear
/// in the Inspector under the Results group.
///
/// Nothing is written to disk. No IdleMonData structs are modified.
/// This component measures Time-to-Kill (TTK) and win rates in real-time
/// so you can evaluate damage formula scaling without playing full combats.
/// </summary>
public sealed class CombatSimulator : Component
{
    // ---- Attacker Profile ----
    [Property, Group( "Attacker Profile" )] public string AttackerName       { get; set; } = "Attacker";
    [Property, Group( "Attacker Profile" )] public double AttackerAddition    { get; set; } = 10.0;
    [Property, Group( "Attacker Profile" )] public double AttackerSubtraction { get; set; } = 5.0;
    [Property, Group( "Attacker Profile" )] public double AttackerMultiplier  { get; set; } = 1.2;
    [Property, Group( "Attacker Profile" )] public double AttackerDivision    { get; set; } = 1.0;
    [Property, Group( "Attacker Profile" )] public double AttackerLuck        { get; set; } = 0.5;
    [Property, Group( "Attacker Profile" )] public double AttackerTeamBonus   { get; set; } = 1.0;
    [Property, Group( "Attacker Profile" )] public int    AttackerGeneration  { get; set; } = 2;

    // ---- Defender Profile ----
    [Property, Group( "Defender Profile" )] public string DefenderName       { get; set; } = "Defender";
    [Property, Group( "Defender Profile" )] public double DefenderAddition    { get; set; } = 8.0;
    [Property, Group( "Defender Profile" )] public double DefenderSubtraction { get; set; } = 3.0;
    [Property, Group( "Defender Profile" )] public double DefenderMultiplier  { get; set; } = 1.0;
    [Property, Group( "Defender Profile" )] public double DefenderDivision    { get; set; } = 1.5;
    [Property, Group( "Defender Profile" )] public double DefenderLuck        { get; set; } = 0.3;
    [Property, Group( "Defender Profile" )] public double DefenderTeamBonus   { get; set; } = 1.0;
    [Property, Group( "Defender Profile" )] public int    DefenderGeneration  { get; set; } = 1;

    // ---- Simulation Settings ----
    [Property, Group( "Simulation Settings" )] public int  SimulationRuns        { get; set; } = 500;
    [Property, Group( "Simulation Settings" )] public bool IncludeSpecialAttack  { get; set; } = false;
    [Property, Group( "Simulation Settings" )] public int  SpecialAttackIndex    { get; set; } = 0;

    // ---- Results (Inspector-visible, read-only) ----
    [Property, ReadOnly, Group( "Results" )] public string AttackerStatSummary  { get; private set; } = "—";
    [Property, ReadOnly, Group( "Results" )] public string DefenderStatSummary  { get; private set; } = "—";
    [Property, ReadOnly, Group( "Results" )] public double AttackerWinRate      { get; private set; }
    [Property, ReadOnly, Group( "Results" )] public double AverageAttackerTTK   { get; private set; }
    [Property, ReadOnly, Group( "Results" )] public double AverageDefenderTTK   { get; private set; }
    [Property, ReadOnly, Group( "Results" )] public double ShortestAtkTTK       { get; private set; }
    [Property, ReadOnly, Group( "Results" )] public double LongestAtkTTK        { get; private set; }
    [Property, ReadOnly, Group( "Results" )] public string BalanceVerdict       { get; private set; } = "Run a simulation first.";

    // =========================================================================
    //  PUBLIC API
    // =========================================================================

    /// <summary>
    /// Runs a headless combat simulation SimulationRuns times.
    /// Call this from CombatDevTools or directly in the Inspector.
    /// </summary>
    public void RunSimulation()
    {
        var atkData = BuildProfile(
            AttackerName, AttackerAddition, AttackerSubtraction,
            AttackerMultiplier, AttackerDivision, AttackerLuck,
            AttackerTeamBonus, AttackerGeneration );

        var defData = BuildProfile(
            DefenderName, DefenderAddition, DefenderSubtraction,
            DefenderMultiplier, DefenderDivision, DefenderLuck,
            DefenderTeamBonus, DefenderGeneration );

        var atkStats = new CombatStatBlock( atkData );
        var defStats = new CombatStatBlock( defData );

        // Expose stat summaries so the designer can quickly see what they're testing
        AttackerStatSummary = atkStats.ToDebugString();
        DefenderStatSummary = defStats.ToDebugString();

        var rng = new Random();

        int    attackerWins  = 0;
        double totalAtkTTK   = 0;
        double totalDefTTK   = 0;
        int    minTTK        = int.MaxValue;
        int    maxTTK        = 0;

        double specialMult = (IncludeSpecialAttack && SpecialAttackIndex < SpecialAttackDefinition.Catalogue.Length)
            ? SpecialAttackDefinition.Catalogue[SpecialAttackIndex].DamageMultiplier
            : 1.0;

        for ( int run = 0; run < SimulationRuns; run++ )
        {
            double atkHP = atkStats.MaxHP;
            double defHP = defStats.MaxHP;
            int    turns = 0;

            while ( atkHP > 0 && defHP > 0 && turns < 2000 )
            {
                turns++;

                // Attacker hits defender (use special multiplier if enabled)
                defHP -= SimHit( atkStats, defStats, rng, specialMult );
                if ( defHP <= 0 ) break;

                // Defender hits attacker (always normal attack for the enemy side)
                atkHP -= SimHit( defStats, atkStats, rng, 1.0 );
            }

            if ( defHP <= 0 )
            {
                attackerWins++;
                totalAtkTTK += turns;
                if ( turns < minTTK ) minTTK = turns;
                if ( turns > maxTTK ) maxTTK = turns;
            }
            else
            {
                totalDefTTK += turns;
            }
        }

        int defenderWins = SimulationRuns - attackerWins;

        AttackerWinRate    = (double)attackerWins / SimulationRuns;
        AverageAttackerTTK = attackerWins > 0 ? Math.Round( totalAtkTTK / attackerWins, 1 ) : 0;
        AverageDefenderTTK = defenderWins > 0 ? Math.Round( totalDefTTK / defenderWins, 1 ) : 0;
        ShortestAtkTTK     = minTTK == int.MaxValue ? 0 : minTTK;
        LongestAtkTTK      = maxTTK;

        BalanceVerdict = DeriveVerdict( AttackerWinRate, AverageAttackerTTK );

        var sb = new StringBuilder();
        sb.AppendLine( $"[CombatSimulator] — {SimulationRuns} runs | Special: {(IncludeSpecialAttack ? $"{specialMult:P0}" : "off")}" );
        sb.AppendLine( $"  Attacker: {AttackerStatSummary}" );
        sb.AppendLine( $"  Defender: {DefenderStatSummary}" );
        sb.AppendLine( $"  Win Rate:    {AttackerWinRate:P1} ({attackerWins}/{SimulationRuns})" );
        sb.AppendLine( $"  Avg TTK:     {AverageAttackerTTK:F1} turns (atk) / {AverageDefenderTTK:F1} turns (def)" );
        sb.AppendLine( $"  TTK Range:   {ShortestAtkTTK} – {LongestAtkTTK} turns" );
        sb.AppendLine( $"  Verdict:     {BalanceVerdict}" );
        Log.Info( sb.ToString() );
    }

    /// <summary>
    /// Pre-fills attacker and defender profiles from two actual IdleMonData sources.
    /// Useful to quickly benchmark two Sigmon from the player's roster.
    /// </summary>
    public void LoadFromIdleMon( IdleMonData attacker, IdleMonData defender )
    {
        AttackerName       = attacker.Name;
        AttackerAddition   = attacker.Addition;
        AttackerSubtraction = attacker.Subtraction;
        AttackerMultiplier = attacker.Multiplier;
        AttackerDivision   = attacker.Division;
        AttackerLuck       = attacker.Luck;
        AttackerTeamBonus  = attacker.TeamBonus;
        AttackerGeneration = attacker.Generation;

        DefenderName       = defender.Name;
        DefenderAddition   = defender.Addition;
        DefenderSubtraction = defender.Subtraction;
        DefenderMultiplier = defender.Multiplier;
        DefenderDivision   = defender.Division;
        DefenderLuck       = defender.Luck;
        DefenderTeamBonus  = defender.TeamBonus;
        DefenderGeneration = defender.Generation;

        Log.Info( $"[CombatSimulator] Profiles loaded: {attacker.Name} vs {defender.Name}" );
    }

    // =========================================================================
    //  INTERNAL HELPERS
    // =========================================================================

    /// <summary>Simulates a single hit using the canonical damage formula.</summary>
    private static double SimHit( CombatStatBlock atk, CombatStatBlock def, Random rng, double mult )
    {
        // Accuracy
        if ( rng.NextDouble() > atk.AccuracyChance ) return 0;
        // Dodge
        if ( rng.NextDouble() < def.DodgeChance     ) return 0;

        double raw        = atk.AttackPower * mult;
        double postResist = raw * (1.0 - def.DamageResistance);
        double critMod    = rng.NextDouble() < atk.CritChance ? 1.5 : 1.0;
        return Math.Max( 1.0, postResist * critMod );
    }

    private static IdleMonData BuildProfile(
        string name, double add, double sub, double mul,
        double div, double luck, double team, int gen )
    {
        return new IdleMonData
        {
            ID             = Guid.NewGuid(),
            Name           = name,
            Addition       = add,
            Subtraction    = sub,
            Multiplier     = mul,
            Division       = div,
            Luck           = luck,
            TeamBonus      = team,
            CostEfficiency = 0,
            Generation     = gen,
        };
    }

    private static string DeriveVerdict( double atkWinRate, double avgTTK )
    {
        if      ( atkWinRate >= 0.90 ) return "⚠️  HEAVILY favours Attacker — consider buffing Defender.";
        else if ( atkWinRate >= 0.70 ) return "⬆  Attacker advantage. Reasonable for a strong unit.";
        else if ( atkWinRate >= 0.45 ) return "✅  Well balanced — both sides viable.";
        else if ( atkWinRate >= 0.25 ) return "⬇  Defender advantage. Reasonable for a tanky unit.";
        else                           return "⚠️  HEAVILY favours Defender — consider buffing Attacker.";
    }
}
