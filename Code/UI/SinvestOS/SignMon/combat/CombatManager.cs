using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sinvest;

// ---- State Enums ----

public enum CombatPhase
{
    /// <summary>No active combat.</summary>
    Inactive,

    /// <summary>Waiting for the player to issue a command for the current acting unit.</summary>
    AwaitingPlayerInput,

    /// <summary>All enemy units are resolving their actions automatically.</summary>
    EnemyTurn,

    /// <summary>Combat has concluded (Victory or Defeat). Pools are being restored.</summary>
    CombatEnd,
}

public enum CombatResult
{
    None,
    Victory,
    Defeat,
}

/// <summary>
/// Orchestrates a full turn-based combat encounter.
///
/// Turn order:
///   1. All 6 player Sigmon act in sequence (slots 0→1→2→3→4→5), front row first.
///      Dead or empty slots are skipped automatically.
///   2. After all player units have acted, all living enemy units act automatically.
///   3. Repeat until one side is fully defeated.
///
/// Persistence contract:
///   - Transient HP / Stamina live ONLY in CombatUnit instances (scene memory).
///   - IdleMonData is only touched to apply kill bonuses and victory bonuses.
///   - All pools are restored on EndCombat() per spec.
///
/// Damage formula:
///   RawDmg      = AttackPower × DamageMultiplier
///   PostResist  = RawDmg × (1 − DefenderResistance)
///   CritMod     = rng &lt; CritChance ? 1.5 : 1.0
///   FinalDamage = max(1, PostResist × CritMod)
///   Hit/Miss:   resolved via AccuracyChance vs DodgeChance beforehand.
/// </summary>
public sealed class CombatManager : Component
{
    // ---- Singleton ----
    public static CombatManager Instance { get; private set; }

    // ---- Combat State (Inspector-visible) ----
    [Property, ReadOnly] public CombatPhase Phase  { get; private set; } = CombatPhase.Inactive;
    [Property, ReadOnly] public CombatResult Result { get; private set; } = CombatResult.None;
    [Property, ReadOnly] public int Round           { get; private set; } = 0;

    /// <summary>Index (0-5) of the player unit currently awaiting input. -1 if not player's turn.</summary>
    [Property, ReadOnly] public int ActivePlayerUnitIndex { get; private set; } = -1;

    // ---- Unit Arrays (0-2 = front row, 3-5 = back row) ----
    public CombatUnit[] PlayerUnits { get; private set; } = new CombatUnit[6];
    public CombatUnit[] EnemyUnits  { get; private set; } = new CombatUnit[6];

    /// <summary>Shortcut to the player unit that is currently taking its turn.</summary>
    public CombatUnit ActivePlayerUnit =>
        ActivePlayerUnitIndex >= 0 && ActivePlayerUnitIndex < 6
            ? PlayerUnits[ActivePlayerUnitIndex]
            : null;

    // ---- Combat Log ----
    public List<string> CombatLog { get; private set; } = new();
    private const int MAX_LOG_ENTRIES = 200;

    // ---- Events (UI hooks) ----
    public Action         OnStateChanged;
    public Action<string> OnLogEntry;

    // ---- RNG ----
    private readonly Random _rng = new();

    // ---- Lifecycle ----

    protected override void OnAwake()
    {
        Instance = this;
    }

    // =========================================================================
    //  PUBLIC API — called by UI / DevTools
    // =========================================================================

    /// <summary>
    /// Begins a new combat encounter using the current RosterManager roster
    /// as the player team and a procedurally generated enemy team.
    /// </summary>
    public void StartCombat()
    {
        if ( Phase != CombatPhase.Inactive )
        {
            Log.Warning( "[CombatManager] Cannot start: a combat is already active." );
            return;
        }

        var roster = RosterManager.Instance;
        if ( roster == null )
        {
            Log.Error( "[CombatManager] RosterManager not found — cannot start combat." );
            return;
        }

        CombatLog.Clear();
        Result = CombatResult.None;
        Round  = 0;

        BuildPlayerTeam( roster );
        BuildEnemyTeam();

        LogEntry( "⚔️  Combat started!" );
        BeginPlayerSequence();
    }

    /// <summary>
    /// Resets all state back to Inactive without restoring pools (hard reset for debug use).
    /// </summary>
    public void ForceReset()
    {
        Phase                  = CombatPhase.Inactive;
        Result                 = CombatResult.None;
        Round                  = 0;
        ActivePlayerUnitIndex  = -1;
        PlayerUnits            = new CombatUnit[6];
        EnemyUnits             = new CombatUnit[6];
        CombatLog.Clear();

        OnStateChanged?.Invoke();
        Log.Info( "[CombatManager] Force reset." );
    }

    // ---- Player Commands ----

    /// <summary>
    /// Issues a basic attack against an enemy slot.
    /// No-op if it is not currently the player's turn.
    /// </summary>
    /// <param name="enemySlotIndex">Target slot (0-5). Must be alive.</param>
    public void PlayerAttack( int enemySlotIndex )
    {
        if ( !AssertPlayerTurn( nameof(PlayerAttack) ) ) return;

        var attacker = PlayerUnits[ActivePlayerUnitIndex];
        var defender = GetLivingEnemyAt( enemySlotIndex );

        if ( defender == null )
        {
            LogEntry( $"⚠️ Slot {enemySlotIndex} is empty or already defeated. Pick another target." );
            OnStateChanged?.Invoke();
            return;
        }

        ExecuteAttack( attacker, defender, 1.0 );
        TryAwardKillBonus( attacker, defender );
        
        if ( !CheckCombatEnd() )
            AdvanceToNextPlayerUnit();
    }

    /// <summary>
    /// Uses a special attack from the catalogue.
    /// Stamina is deducted before resolving; if insufficient, the turn is NOT consumed.
    /// </summary>
    /// <param name="specialIndex">Index into SpecialAttackDefinition.Catalogue.</param>
    /// <param name="primaryTargetSlot">Target slot for SingleEnemy attacks (ignored for others).</param>
    public void PlayerUseSpecial( int specialIndex, int primaryTargetSlot )
    {
        if ( !AssertPlayerTurn( nameof(PlayerUseSpecial) ) ) return;

        if ( specialIndex < 0 || specialIndex >= SpecialAttackDefinition.Catalogue.Length )
        {
            Log.Warning( $"[CombatManager] Invalid special index {specialIndex}." );
            return;
        }

        var attacker = PlayerUnits[ActivePlayerUnitIndex];
        var special  = SpecialAttackDefinition.Catalogue[specialIndex];

        // Validate stamina before consuming the turn
        if ( attacker.CurrentStamina < special.StaminaCost )
        {
            LogEntry( $"⚠️ {attacker.Stats.Name} needs {special.StaminaCost:F0} stamina " +
                      $"for {special.Name} but only has {attacker.CurrentStamina:F0}. Guard to recover!" );
            OnStateChanged?.Invoke();
            return; // Turn NOT consumed — player must pick another action
        }

        attacker.CurrentStamina -= special.StaminaCost;
        LogEntry( $"✨ {attacker.Stats.Name} uses {special.Name}!" );

        switch ( special.Targeting )
        {
            case TargetMode.SingleEnemy:
            {
                var target = GetLivingEnemyAt( primaryTargetSlot );
                if ( target == null )
                {
                    LogEntry( $"⚠️ Slot {primaryTargetSlot} is empty. Pick another target." );
                    // Refund stamina and don't consume the turn
                    attacker.CurrentStamina += special.StaminaCost;
                    OnStateChanged?.Invoke();
                    return;
                }
                ExecuteAttack( attacker, target, special.DamageMultiplier );
                TryAwardKillBonus( attacker, target );
                break;
            }

            case TargetMode.AllEnemies:
            {
                foreach ( var enemy in EnemyUnits )
                {
                    if ( enemy != null && enemy.IsAlive )
                    {
                        ExecuteAttack( attacker, enemy, special.DamageMultiplier );
                        TryAwardKillBonus( attacker, enemy );
                    }
                }
                break;
            }

            case TargetMode.RandomEnemy:
            {
                var alive = EnemyUnits.Where( e => e != null && e.IsAlive ).ToArray();
                if ( alive.Length > 0 )
                {
                    var target = alive[_rng.Next( alive.Length )];
                    ExecuteAttack( attacker, target, special.DamageMultiplier );
                    TryAwardKillBonus( attacker, target );
                }
                break;
            }

            case TargetMode.Self:
            {
                // Placeholder — heal or buff logic goes here
                LogEntry( $"🛡 {attacker.Stats.Name} used {special.Name} on itself." );
                break;
            }
        }

        if ( !CheckCombatEnd() )
            AdvanceToNextPlayerUnit();
    }

    /// <summary>
    /// The active Sigmon guards, recovering stamina without attacking.
    /// Useful when the unit is low on stamina or you want to stall.
    /// </summary>
    public void PlayerGuard()
    {
        if ( !AssertPlayerTurn( nameof(PlayerGuard) ) ) return;

        var unit = PlayerUnits[ActivePlayerUnitIndex];
        if ( unit == null ) return;

        unit.RegenStamina();
        LogEntry( $"🛡 {unit.Stats.Name} guards. (Stamina: {unit.CurrentStamina:F0}/{unit.Stats.MaxStamina:F0})" );

        AdvanceToNextPlayerUnit();
    }

    // =========================================================================
    //  TEAM CONSTRUCTION
    // =========================================================================

    private void BuildPlayerTeam( RosterManager roster )
    {
        for ( int i = 0; i < 6; i++ )
        {
            var data = roster.ActiveNodes.Count > i ? roster.ActiveNodes[i] : IdleMonData.Empty;
            PlayerUnits[i] = data.ID != Guid.Empty ? new CombatUnit( data, i, isEnemy: false ) : null;
        }

        int count = PlayerUnits.Count( u => u != null );
        LogEntry( $"Player team ready: {count} Sigmon." );
    }

    private void BuildEnemyTeam()
    {
        float S = MarketServiceSystem.GetCurrentScale();

        // Reference the player's roster stats to scale enemies proportionally
        double[] playerAdditions = PlayerUnits
            .Where( u => u != null )
            .Select( u => u.Source.Addition )
            .ToArray();

        double avgAddition = playerAdditions.Length > 0 ? playerAdditions.Average() : 5.0;
        double maxAddition = playerAdditions.Length > 0 ? playerAdditions.Max()     : 5.0;

        for ( int i = 0; i < 6; i++ )
        {
            EnemyUnits[i] = new CombatUnit( GenerateEnemyData( i, avgAddition, maxAddition, S ), i, isEnemy: true );
        }

        LogEntry( $"Enemy team generated (Market Scale: {S:F2})." );
    }

    private IdleMonData GenerateEnemyData( int slot, double avgAddition, double maxAddition, float marketScale )
    {
        // Back-row enemies are 25% weaker — front row provides the real threat
        double rowScale = slot < 3 ? 1.0 : 0.75;

        // Spread enemies across a range to avoid all-or-nothing encounters
        double spreadFactor = 0.65 + _rng.NextDouble() * 0.55; // [0.65 – 1.20]

        double addition    = Math.Round( avgAddition * rowScale * spreadFactor * marketScale, 2 );
        double subtraction = Math.Round( addition * (0.35 + _rng.NextDouble() * 0.30), 2 );
        double division    = Math.Round( 1.0 + _rng.NextDouble() * 0.6, 2 );
        double multiplier  = Math.Round( 1.0 + _rng.NextDouble() * 0.35, 2 );
        double luck        = Math.Round( _rng.NextDouble() * marketScale * 0.5, 2 );

        return new IdleMonData
        {
            ID             = Guid.NewGuid(),
            Name           = $"Foe {(char)('A' + slot)}",
            Addition       = addition,
            Subtraction    = subtraction,
            Division       = division,
            Multiplier     = multiplier,
            TeamBonus      = 1.0,
            Luck           = luck,
            CostEfficiency = 0,
            Generation     = 1 + (slot % 3), // Gen 1, 2, or 3 — varied Luck routing
        };
    }

    // =========================================================================
    //  TURN SEQUENCING
    // =========================================================================

    private void BeginPlayerSequence()
    {
        Round++;
        LogEntry( $"── Round {Round} ──" );
        ActivePlayerUnitIndex = -1;
        Phase = CombatPhase.AwaitingPlayerInput;
        AdvanceToNextPlayerUnit();
    }

    private void AdvanceToNextPlayerUnit()
    {
        int next = ActivePlayerUnitIndex + 1;

        // Skip null and dead slots
        while ( next < 6 && (PlayerUnits[next] == null || !PlayerUnits[next].IsAlive) )
            next++;

        if ( next >= 6 )
        {
            // All player units have had their turn — hand over to the enemy
            RunEnemyTurn();
            return;
        }

        ActivePlayerUnitIndex = next;
        Phase = CombatPhase.AwaitingPlayerInput;

        var unit = PlayerUnits[ActivePlayerUnitIndex];
        LogEntry( $"▶ {unit.Stats.Name}'s turn (Slot {ActivePlayerUnitIndex}) — " +
                  $"HP:{unit.CurrentHP:F1}/{unit.Stats.MaxHP:F1}  STA:{unit.CurrentStamina:F0}" );

        OnStateChanged?.Invoke();
    }

    // =========================================================================
    //  ENEMY AI
    // =========================================================================

    private void RunEnemyTurn()
    {
        Phase                 = CombatPhase.EnemyTurn;
        ActivePlayerUnitIndex = -1;
        LogEntry( "── Enemy Phase ──" );
        OnStateChanged?.Invoke();

        // Front row acts first (slots 0→5)
        var actingEnemies = EnemyUnits
            .Where( e => e != null && e.IsAlive )
            .OrderBy( e => e.SlotIndex )
            .ToList();

        foreach ( var enemy in actingEnemies )
        {
            if ( CheckCombatEnd() ) return;

            var target = SelectTargetForEnemy();
            if ( target == null ) break;

            ExecuteAttack( enemy, target, 1.0 );
        }

        if ( CheckCombatEnd() ) return;

        // Regen stamina for all living player units at end of the round
        foreach ( var u in PlayerUnits )
            u?.RegenStamina();

        BeginPlayerSequence();
    }

    /// <summary>
    /// Enemy AI targeting strategy.
    ///
    /// Design goal: keep the experience fun, not punishing.
    ///
    /// Approach: weight each candidate by current HP so healthier Sigmon are
    /// more likely targeted (protecting wounded units). A jitter term adds
    /// unpredictability so it never feels robotic — the enemy will occasionally
    /// poke low-HP targets, keeping the player slightly on edge without
    /// relentlessly finishing off their weakest unit.
    /// </summary>
    private CombatUnit SelectTargetForEnemy()
    {
        var candidates = PlayerUnits
            .Where( u => u != null && u.IsAlive )
            .ToArray();

        if ( candidates.Length == 0 ) return null;

        // Base weight = current HP (higher HP → more likely to be targeted)
        double[] weights = new double[candidates.Length];
        for ( int i = 0; i < candidates.Length; i++ )
        {
            double hpWeight = candidates[i].CurrentHP;

            // Jitter: up to ±15% of the unit's max HP — prevents pure determinism
            double jitter = (_rng.NextDouble() - 0.5) * candidates[i].Stats.MaxHP * 0.30;

            weights[i] = Math.Max( 1.0, hpWeight + jitter );
        }

        // Weighted random selection
        double total      = weights.Sum();
        double roll       = _rng.NextDouble() * total;
        double cumulative = 0;

        for ( int i = 0; i < candidates.Length; i++ )
        {
            cumulative += weights[i];
            if ( roll <= cumulative ) return candidates[i];
        }

        return candidates[candidates.Length - 1];
    }

    // =========================================================================
    //  CORE DAMAGE FORMULA
    // =========================================================================

    /// <summary>
    /// Resolves a single attack between two units.
    ///
    /// Formula:
    ///   1. Accuracy check (attacker) — miss if failed
    ///   2. Dodge check (defender)   — dodge if succeeded
    ///   3. RawDamage    = AttackPower × damageMultiplier
    ///   4. PostResist   = RawDamage × (1 − DamageResistance)
    ///   5. CritMod      = rng &lt; CritChance ? 1.5 : 1.0
    ///   6. FinalDamage  = max(1, PostResist × CritMod)
    /// </summary>
    private void ExecuteAttack( CombatUnit attacker, CombatUnit defender, double damageMultiplier )
    {
        if ( !attacker.IsAlive || !defender.IsAlive ) return;

        var atkS = attacker.Stats;
        var defS = defender.Stats;

        // 1. Accuracy
        if ( _rng.NextDouble() > atkS.AccuracyChance )
        {
            LogEntry( $"  ↩ {atkS.Name} missed {defS.Name}." );
            return;
        }

        // 2. Dodge
        if ( _rng.NextDouble() < defS.DodgeChance )
        {
            LogEntry( $"  ↩ {defS.Name} dodged {atkS.Name}'s attack!" );
            return;
        }

        // 3. Raw damage
        double raw = atkS.AttackPower * damageMultiplier;

        // 4. Resistance reduction
        double afterResist = raw * (1.0 - defS.DamageResistance);

        // 5. Critical hit
        bool   isCrit   = _rng.NextDouble() < atkS.CritChance;
        double critMod  = isCrit ? 1.5 : 1.0;

        // 6. Final (floored at 1)
        double finalDmg = Math.Max( 1.0, Math.Round( afterResist * critMod, 1 ) );

        defender.CurrentHP = Math.Max( 0.0, defender.CurrentHP - finalDmg );

        string critTag  = isCrit ? " 💥 CRIT!" : "";
        string sideTag  = attacker.IsEnemy ? "  👾" : "  ▶";
        LogEntry( $"{sideTag} {atkS.Name} → {defS.Name}: -{finalDmg:F1}{critTag} " +
                  $"[HP {defender.CurrentHP:F1}/{defS.MaxHP:F1}]" );

        OnStateChanged?.Invoke();
    }

    // =========================================================================
    //  KILL BONUS
    // =========================================================================

    /// <summary>
    /// If the defender just died, awards a small flat stat bonus to the killer.
    /// Stat chosen at random from the victim's stat pool.
    /// Only applies when the killer is a player unit (spec: player roster grows).
    /// </summary>
    private void TryAwardKillBonus( CombatUnit killer, CombatUnit victim )
    {
        if ( victim.IsAlive || killer.IsEnemy ) return;

        killer.KillCount++;
        LogEntry( $"  💀 {killer.Stats.Name} defeated {victim.Stats.Name}!" );

        var roster = RosterManager.Instance;
        if ( roster == null ) return;

        int rosterIndex = roster.ActiveNodes.FindIndex( n => n.ID == killer.Source.ID );
        if ( rosterIndex < 0 ) return;

        var data     = roster.ActiveNodes[rosterIndex];
        int statRoll = _rng.Next( 5 );
        string statName;
        double bonus;

        switch ( statRoll )
        {
            case 0:
                bonus    = Math.Max( 0.01, victim.Source.Addition * 0.01 );
                data.Addition += bonus;
                statName = "Addition";
                break;
            case 1:
                bonus    = Math.Max( 0.01, victim.Source.Subtraction * 0.01 );
                data.Subtraction += bonus;
                statName = "Subtraction";
                break;
            case 2:
                bonus    = data.Multiplier * 0.001;
                data.Multiplier += bonus;
                statName = "Multiplier";
                break;
            case 3:
                bonus    = Math.Max( 0.001, (victim.Source.Division - 1.0) * 0.01 );
                data.Division  += bonus;
                statName = "Division";
                break;
            default:
                bonus    = Math.Max( 0.001, victim.Source.Luck * 0.01 );
                data.Luck += bonus;
                statName = "Luck";
                break;
        }

        roster.ActiveNodes[rosterIndex] = data;
        LogEntry( $"  ⬆ {killer.Stats.Name} gained +{bonus:F4} {statName} from the kill!" );
    }

    // =========================================================================
    //  COMBAT END
    // =========================================================================

    /// <returns>True if combat ended this check.</returns>
    private bool CheckCombatEnd()
    {
        bool allEnemiesDead  = EnemyUnits.All( u => u == null || !u.IsAlive );
        bool allPlayersDead  = PlayerUnits.All( u => u == null || !u.IsAlive );

        if ( allEnemiesDead )
        {
            Result = CombatResult.Victory;
            Phase  = CombatPhase.CombatEnd;
            LogEntry( "🎉 Victory! All enemies defeated." );
            AwardVictoryBonus();
            EndCombat();
            return true;
        }

        if ( allPlayersDead )
        {
            Result = CombatResult.Defeat;
            Phase  = CombatPhase.CombatEnd;
            LogEntry( "💀 Defeat. All Sigmon have fallen." );
            EndCombat();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Per spec: "Each combat victory lets the player choose a stat to increase
    /// by a tiny percentage on the active signmon (something like x1.01)."
    ///
    /// Auto-applies x1.01 to Addition on all surviving player units for now.
    /// TODO: Replace with a UI prompt so the player can choose which stat to boost.
    /// </summary>
    private void AwardVictoryBonus()
    {
        var roster = RosterManager.Instance;
        if ( roster == null ) return;

        int bonusCount = 0;

        foreach ( var unit in PlayerUnits )
        {
            if ( unit == null || !unit.IsAlive ) continue;

            int idx = roster.ActiveNodes.FindIndex( n => n.ID == unit.Source.ID );
            if ( idx < 0 ) continue;

            var data    = roster.ActiveNodes[idx];
            data.Addition *= 1.01; // x1.01 per spec
            roster.ActiveNodes[idx] = data;
            bonusCount++;
        }

        if ( bonusCount > 0 )
        {
            roster.SaveRoster();
            LogEntry( $"  ⬆ Victory bonus: ×1.01 Addition applied to {bonusCount} surviving Sigmon." );
            LogEntry( "  [TODO] Wire to player-choice UI for stat selection." );
        }
    }

    /// <summary>
    /// Wraps up combat: restores all transient pools, clears active unit index.
    /// </summary>
    private void EndCombat()
    {
        ActivePlayerUnitIndex = -1;

        // Restore HP and Stamina pools (spec: "Sigmon's pools get restored after each combat")
        foreach ( var u in PlayerUnits ) u?.RestoreAfterCombat();
        foreach ( var u in EnemyUnits  ) u?.RestoreAfterCombat();

        LogEntry( $"Combat over — pools restored. Result: {Result}" );
        OnStateChanged?.Invoke();
    }

    // =========================================================================
    //  HELPERS
    // =========================================================================

    private bool AssertPlayerTurn( string caller )
    {
        if ( Phase != CombatPhase.AwaitingPlayerInput )
        {
            Log.Warning( $"[CombatManager] {caller} called during wrong phase: {Phase}" );
            return false;
        }
        return true;
    }

    private CombatUnit GetLivingEnemyAt( int slotIndex )
    {
        if ( slotIndex < 0 || slotIndex >= 6 ) return null;
        var unit = EnemyUnits[slotIndex];
        return (unit != null && unit.IsAlive) ? unit : null;
    }

    private void LogEntry( string message )
    {
        if ( CombatLog.Count >= MAX_LOG_ENTRIES )
            CombatLog.RemoveAt( 0 );

        CombatLog.Add( message );
        OnLogEntry?.Invoke( message );
        Log.Info( $"[Combat] {message}" );
    }
}
