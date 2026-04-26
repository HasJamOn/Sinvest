using Sandbox;
using System;

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
			Log.Info( $"Stats: A:{_currentCandidate.Addition} | M:{_currentCandidate.Multiplier}x" );
            
			// Using your newly implemented Delta logic!
			var deltas = Roster.GetSwapDeltas( _currentCandidate );

			// Ensure we don't loop out of bounds of what was actually returned
			if ( deltas != null )
			{
				for ( int i = 0; i < deltas.Length; i++ )
				{
					string sign = deltas[i] >= 0 ? "+" : "";
					Log.Info( $"Slot {i}: {sign}{deltas[i]:F2} Yield Change" );
				}
			}
			Log.Info( "Press 1-6 to Swap, or R to re-roll." );
		}

		// Check for Keys 1 through 6
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
		Roster.SwapNode( index, _currentCandidate );
		_hasCandidate = false;
		Log.Info( $"Successfully installed {_currentCandidate.Name} into Node {index}" );
	}
}
