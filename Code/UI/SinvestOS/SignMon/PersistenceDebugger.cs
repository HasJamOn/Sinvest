using Sandbox;
using System;
using System.Linq;

namespace Sinvest;

public sealed class PersistenceDebugger : Component
{
    protected override void OnUpdate()
    {
       var save = GameSaveSystem.Instance;
       var roster = Scene.GetAllComponents<RosterManager>().FirstOrDefault();

       // 1. Check for the Save System Instance
       // Ensures the singleton is active in the scene before attempting data access.
       if ( !save.IsValid() )
       {
          Gizmo.Draw.Color = Color.Red;
          Gizmo.Draw.ScreenText( "CRITICAL: GameSaveSystem NOT FOUND", new Vector2( 50, 150 ) );
          return;
       }

       string status = $"--- IDLEMON PERSISTENCE DEBUG ---\n";
       status += $"Active Slot: {save.ActiveSlot}\n";
        
       // 2. Check the Character State
       // Reflects the Phase 2 Cloud mirror state if the session has successfully initialized.
       if ( save.CurrentCharacter == null )
       {
          status += "Character: NULL (Session Not Started)\n";
       }
       else
       {
          status += $"Character: {save.CurrentCharacter.Name}\n";
          status += $"Money: {save.CurrentCharacter.Money:C}\n";
       }

       // 3. Check the Cookie directly
       // Validation for Phase 1: Local-first plaintext ledger.
       // This confirms data exists on the client disk before it is processed into the balance.
       string cookieKey = $"name_{save.ActiveSlot}";
       status += $"Cookie ({cookieKey}): {Game.Cookies.Get( cookieKey, "EMPTY/NOT_FOUND" )}\n";
        
       // 4. Check the Roster
       // Monitors the 'Live' state of the ledger.
       // Balance is derived from Inflow.Sum - Outflow.Sum; this displays the resulting calculation.
       if ( roster.IsValid() )
       {
          status += $"Roster Nodes: {roster.ActiveNodes?.Count ?? 0}\n";
          status += $"IdleBucks: {roster.IdleBucks:F2}\n";
          status += $"Flush Interval: {roster.FlushInterval}s\n";
       }
       else
       {
          status += "ROSTER MANAGER: NOT FOUND IN SCENE\n";
       }

       // 5. Render to screen
       // Uses the Gizmo system for a non-invasive debug overlay that bypasses Panorama CSS constraints.
       Gizmo.Draw.Color = Color.Yellow;
       Gizmo.Draw.ScreenText( status, new Vector2( 50, 180 ) );
    }
}
