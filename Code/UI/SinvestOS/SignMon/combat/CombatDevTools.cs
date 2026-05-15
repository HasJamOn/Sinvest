using Sandbox;
using System;
using System.Linq;
using System.Text;

namespace Sinvest;

/// <summary>
/// Development and debugging companion for CombatManager.
/// Add to any GameObject in the scene alongside CombatManager.
///
/// Inspector controls:
///   • Trigger bools to start, reset, and force-end combat
///   • Override flags to force specific phases or outcomes
///   • Diagnostic readout of full team states and combat log
///   • Quick simulate: auto-fill CombatSimulator from current roster
///
/// All trigger bools self-reset after one frame so they behave
/// like momentary buttons in the s&box inspector.
/// </summary>
public sealed class CombatDevTools : Component
{
    // ---- References ----
    [Property] public CombatManager  CombatManager  { get; set; }
    [Property] public CombatSimulator CombatSimulator { get; set; }

    // =========================================================================
    //  TRIGGER BOOLS (momentary inspector "buttons")
    //  Set to true in the Inspector — they fire once and reset to false.
    // =========================================================================

    [Property, Group( "Combat Controls" )]
    [Title( "▶ Start Combat" )]
    public bool TriggerStart { get; set; }

    [Property, Group( "Combat Controls" )]
    [Title( "↺ Reset Combat" )]
    public bool TriggerReset { get; set; }

    [Property, Group( "Combat Controls" )]
    [Title( "⚡ Force Victory" )]
    public bool TriggerForceVictory { get; set; }

    [Property, Group( "Combat Controls" )]
    [Title( "💀 Force Defeat" )]
    public bool TriggerForceDefeat { get; set; }

    [Property, Group( "Combat Controls" )]
    [Title( "⏭ Skip Current Unit Turn" )]
    public bool TriggerSkipTurn { get; set; }

    // ---- Simulator Controls ----
    [Property, Group( "Simulator Controls" )]
    [Title( "📊 Run Balance Simulation" )]
    public bool TriggerRunSimulation { get; set; }

    [Property, Group( "Simulator Controls" )]
    [Title( "📥 Load Roster Slots 0 & 1 Into Simulator" )]
    public bool TriggerLoadRosterIntoSim { get; set; }

    // ---- Override Settings ----
    [Property, Group( "Override Settings" )]
    public bool InstantKillEnemies { get; set; } = false;

    [Property, Group( "Override Settings" )]
    public bool GodModePlayer { get; set; } = false;

    [Property, Group( "Override Settings" )]
    public bool InfiniteStamina { get; set; } = false;

    // =========================================================================
    //  DIAGNOSTIC OUTPUT (read-only, updates every frame during combat)
    // =========================================================================

    [Property, ReadOnly, Group( "Diagnostics" )]
    public string CombatStatus { get; private set; } = "Inactive";

    [Property, ReadOnly, Group( "Diagnostics" )]
    public string PlayerTeamStatus { get; private set; } = "—";

    [Property, ReadOnly, Group( "Diagnostics" )]
    public string EnemyTeamStatus { get; private set; } = "—";

    [Property, ReadOnly, Group( "Diagnostics" )]
    public string LastLogEntry { get; private set; } = "—";

    [Property, ReadOnly, Group( "Diagnostics" )]
    public string CombatLogDump { get; private set; } = "No combat active.";

    // ---- Frame tracking ----
    private bool _wasStartTriggered;
    private bool _wasResetTriggered;

    // =========================================================================
    //  LIFECYCLE
    // =========================================================================

    protected override void OnStart()
    {
        // Auto-wire if not manually assigned
        if ( CombatManager == null )
            CombatManager = Scene.GetAllComponents<CombatManager>().FirstOrDefault();

        if ( CombatSimulator == null )
            CombatSimulator = Scene.GetAllComponents<CombatSimulator>().FirstOrDefault();

        if ( CombatManager != null )
        {
            CombatManager.OnLogEntry += entry => LastLogEntry = entry;
        }
    }

    protected override void OnUpdate()
    {
        ProcessTriggers();
        ApplyOverrides();
        RefreshDiagnostics();
    }

    // =========================================================================
    //  TRIGGER PROCESSING
    // =========================================================================

    private void ProcessTriggers()
    {
        if ( TriggerStart )
        {
            TriggerStart = false;
            CombatManager?.StartCombat();
        }

        if ( TriggerReset )
        {
            TriggerReset = false;
            CombatManager?.ForceReset();
        }

        if ( TriggerForceVictory )
        {
            TriggerForceVictory = false;
            ForceVictory();
        }

        if ( TriggerForceDefeat )
        {
            TriggerForceDefeat = false;
            ForceDefeat();
        }

        if ( TriggerSkipTurn )
        {
            TriggerSkipTurn = false;
            SkipCurrentPlayerTurn();
        }

        if ( TriggerRunSimulation )
        {
            TriggerRunSimulation = false;
            CombatSimulator?.RunSimulation();
        }

        if ( TriggerLoadRosterIntoSim )
        {
            TriggerLoadRosterIntoSim = false;
            LoadRosterIntoSimulator();
        }
    }

    // =========================================================================
    //  OVERRIDE APPLICATION (active every frame during combat)
    // =========================================================================

    private void ApplyOverrides()
    {
        if ( CombatManager == null || CombatManager.Phase == CombatPhase.Inactive ) return;

        if ( InstantKillEnemies )
        {
            foreach ( var e in CombatManager.EnemyUnits )
            {
                if ( e != null && e.IsAlive )
                    e.CurrentHP = 0.1; // Leave at 0.1 so the manager detects the kill on next hit
            }
        }

        if ( GodModePlayer )
        {
            foreach ( var p in CombatManager.PlayerUnits )
            {
                if ( p != null && p.IsAlive )
                    p.CurrentHP = p.Stats.MaxHP; // Restore to full every frame
            }
        }

        if ( InfiniteStamina )
        {
            foreach ( var p in CombatManager.PlayerUnits )
            {
                if ( p != null )
                    p.CurrentStamina = p.Stats.MaxStamina;
            }
        }
    }

    // =========================================================================
    //  FORCE OUTCOMES
    // =========================================================================

    private void ForceVictory()
    {
        if ( CombatManager == null || CombatManager.Phase == CombatPhase.Inactive )
        {
            Log.Warning( "[CombatDevTools] No active combat to force-win." );
            return;
        }

        Log.Info( "[CombatDevTools] Forcing Victory — killing all enemies." );
        foreach ( var e in CombatManager.EnemyUnits )
            if ( e != null ) e.CurrentHP = 0;

        // Trigger a guard action to let the manager detect the win condition
        if ( CombatManager.Phase == CombatPhase.AwaitingPlayerInput )
            CombatManager.PlayerGuard();
    }

    private void ForceDefeat()
    {
        if ( CombatManager == null || CombatManager.Phase == CombatPhase.Inactive )
        {
            Log.Warning( "[CombatDevTools] No active combat to force-lose." );
            return;
        }

        Log.Info( "[CombatDevTools] Forcing Defeat — killing all player units." );
        foreach ( var p in CombatManager.PlayerUnits )
            if ( p != null ) p.CurrentHP = 0;

        if ( CombatManager.Phase == CombatPhase.AwaitingPlayerInput )
            CombatManager.PlayerGuard();
    }

    /// <summary>
    /// Skips the current player unit's turn by issuing a Guard command.
    /// Useful for fast-forwarding through slow combats during testing.
    /// </summary>
    private void SkipCurrentPlayerTurn()
    {
        if ( CombatManager?.Phase != CombatPhase.AwaitingPlayerInput )
        {
            Log.Warning( "[CombatDevTools] SkipTurn: not currently awaiting player input." );
            return;
        }

        Log.Info( $"[CombatDevTools] Skipping turn for unit {CombatManager.ActivePlayerUnitIndex}." );
        CombatManager.PlayerGuard();
    }

    // =========================================================================
    //  SIMULATOR HELPERS
    // =========================================================================

    private void LoadRosterIntoSimulator()
    {
        if ( CombatSimulator == null )
        {
            Log.Warning( "[CombatDevTools] No CombatSimulator found." );
            return;
        }

        var roster = RosterManager.Instance;
        if ( roster == null || roster.ActiveNodes.Count < 2 )
        {
            Log.Warning( "[CombatDevTools] Roster has fewer than 2 Sigmon." );
            return;
        }

        var slot0 = roster.ActiveNodes.FirstOrDefault( n => n.ID != Guid.Empty );
        var slot1 = roster.ActiveNodes.Skip( 1 ).FirstOrDefault( n => n.ID != Guid.Empty );

        if ( slot0.ID == Guid.Empty || slot1.ID == Guid.Empty )
        {
            Log.Warning( "[CombatDevTools] Need at least 2 non-empty roster slots." );
            return;
        }

        CombatSimulator.LoadFromIdleMon( slot0, slot1 );
        Log.Info( $"[CombatDevTools] Loaded {slot0.Name} vs {slot1.Name} into simulator." );
    }

    // =========================================================================
    //  DIAGNOSTICS REFRESH
    // =========================================================================

    private void RefreshDiagnostics()
    {
        if ( CombatManager == null )
        {
            CombatStatus = "No CombatManager found.";
            return;
        }

        var cm = CombatManager;

        CombatStatus = cm.Phase == CombatPhase.Inactive
            ? $"Inactive | Last result: {cm.Result}"
            : $"Phase: {cm.Phase}  Round: {cm.Round}  ActiveUnit: {cm.ActivePlayerUnitIndex}  Result: {cm.Result}";

        PlayerTeamStatus = BuildTeamStatus( cm.PlayerUnits );
        EnemyTeamStatus  = BuildTeamStatus( cm.EnemyUnits );

        if ( cm.CombatLog.Count > 0 )
        {
            // Show last 10 entries in the Inspector dump
            var recent = cm.CombatLog
                .Skip( Math.Max( 0, cm.CombatLog.Count - 10 ) )
                .ToArray();
            CombatLogDump = string.Join( "\n", recent );
        }
    }

    private static string BuildTeamStatus( CombatUnit[] units )
    {
        var sb = new StringBuilder();
        for ( int i = 0; i < units.Length; i++ )
        {
            var u = units[i];
            if ( u == null )
            {
                sb.AppendLine( $"  [{i}] Empty" );
                continue;
            }

            string row    = i < 3 ? "Front" : "Back ";
            string status = u.IsAlive ? $"HP {u.CurrentHP:F0}/{u.Stats.MaxHP:F0}" : "Defeated";
            sb.AppendLine( $"  [{i}] {row} | {u.Stats.Name,-20} {status}  STA:{u.CurrentStamina:F0}" );
        }
        return sb.ToString().TrimEnd();
    }
}
