using Sandbox;
using Sandbox.Movement;
using System;

namespace Sinvest;

[Title( "Pumper Seat" )]
[Category( "Sinvest" )]
[Icon( "chair" )]
public sealed class PumperSeat : Component
{
	[Property] public GameObject SeatPoint { get; set; } 
    
	private BaseChair _internalChair;
	public string TooltipTitle => _internalChair?.TooltipTitle ?? "Oil Pump";
	public string TooltipIcon => _internalChair?.TooltipIcon ?? "chair";
	public string TooltipDescription => _internalChair?.TooltipDescription ?? "Start Pumping";

	protected override void OnStart()
	{
		_internalChair = Components.Get<BaseChair>( FindMode.EverythingInSelfAndDescendants );
		
		if ( !_internalChair.IsValid() )
		{
			Log.Warning( $"{GameObject.Name} has a PumperSeat but no BaseChair was found!" );
		}
	}

	public GameObject Occupant 
	{
		get 
		{
			if ( !_internalChair.IsValid() ) return null;

			// Check for a real PlayerController first
			var realPlayer = _internalChair.GetOccupant();
			if ( realPlayer.IsValid() ) return realPlayer.GameObject;

			// FALLBACK FOR DUMMIES: 
			// If no PlayerController is found, check if a dummy object was parented to the seat
			var seatPos = _internalChair.SeatPosition ?? _internalChair.GameObject;
			return seatPos.Children.FirstOrDefault( x => x.Name.Contains("DUMMY") );
		}
	}
    
	public bool IsOccupied => Occupant.IsValid();

	public void Sit( GameObject player )
	{
		if ( IsOccupied || !_internalChair.IsValid() ) return;

		var controller = player.Components.Get<PlayerController>();
		
		if ( controller.IsValid() )
		{
			// Handle real player via BaseChair logic
			_internalChair.Sit( controller );
		}
		else
		{
			// Handle Dummy: Manually parent and reset transform since BaseChair won't
			var seatPos = _internalChair.SeatPosition ?? _internalChair.GameObject;
			player.SetParent( seatPos, false );
			player.LocalTransform = global::Transform.Zero;
			
			Log.Info( $"PumperSeat: Dummy {player.Name} seated manually." );
		}
	}

	public void Leave()
	{
		if ( !_internalChair.IsValid() ) return;

		var player = Occupant;
		if ( !player.IsValid() ) return;

		var controller = player.Components.Get<PlayerController>();
		if ( controller.IsValid() )
		{
			_internalChair.Eject( controller );
		}
		else
		{
			// Handle Dummy: Unparent manually
			player.SetParent( null, true );
			
			// Move to best exit point if possible
			player.WorldPosition = _internalChair.FindBestExitPoint();
		}
	}
}
