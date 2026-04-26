using Sandbox;
using System;
using System.Linq;

namespace Sinvest;

public sealed class TestInputHandler : Component
{
    [Property] public AssetGenerator Generator { get; set; }
    [Property] public RosterManager Roster { get; set; }

    private IdleMonData _currentCandidate;
    private bool _hasCandidate = false;

    protected override void OnUpdate()
    {
       if ( !Generator.IsValid() || !Roster.IsValid() ) return;

       // Press 'R' (Reload) to roll a new Mon
       if ( Input.Pressed( "reload" ) ) 
       {
          _currentCandidate = Generator.RollNewAsset();
          _hasCandidate = true;
            
          Log.Info( $"--- DRAFTING: {_currentCandidate.Name} ---" );
          Log.Info( $"Cert: Gen {_currentCandidate.Generation} | Market Scale: {_currentCandidate.MarketScaleAtBirth:F2}x" );
          Log.Info( $"Stats: A:{_currentCandidate.Addition} | M:{_currentCandidate.Multiplier:F2}x | S:{_currentCandidate.Subtraction:F1}" );
            
          // Using your Delta logic to see if this roll is actually worth it
          var deltas = Roster.GetSwapDeltas( _currentCandidate );

          if ( deltas != null )
          {
             Log.Info( "YIELD PROJECTIONS:" );
             for ( int i = 0; i < deltas.Length; i++ )
             {
                string sign = deltas[i] >= 0 ? "+" : "";
                string color = deltas[i] >= 0 ? "green" : "red";
                // Color formatting for the s&box console
                Log.Info( $"Slot {i}: [{color}]{sign}{deltas[i]:F2} IB/sec[/]" );
             }
          }
          Log.Info( "Press [1-6] to Overwrite Node, or [R] to discard and re-roll." );
       }

       // Check for Keys 1 through 6 to confirm the swap
       if ( _hasCandidate )
       {
          if ( Input.Pressed( "slot1" ) ) AcceptDraft( 0 );
          if ( Input.Pressed( "slot2" ) ) AcceptDraft( 1 );
          if ( Input.Pressed( "slot3" ) ) AcceptDraft( 2 );
          if ( Input.Pressed( "slot4" ) ) AcceptDraft( 3 );
          if ( Input.Pressed( "slot5" ) ) AcceptDraft( 4 );
          if ( Input.Pressed( "slot6" ) ) AcceptDraft( 5 );
       }
    }

    private void AcceptDraft( int index )
    {
       // Stamp the candidate into the roster
       Roster.SwapNode( index, _currentCandidate );
       _hasCandidate = false;
       
       Log.Info( $"[SYSTEM] Node {index} updated: {_currentCandidate.Name} is now live." );
       Log.Info( $"New Total Yield: {Roster.CalculateTotalYield():F2} IB/sec" );
    }
}
