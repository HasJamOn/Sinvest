using Sandbox;
using System;

public sealed class StampLandPlayer : Component, Component.ITriggerListener
{
	private StampLandCube _lastTouchedCube;

	public void OnTriggerEnter( Collider other )
	{
		// Only the local player (the one actually walking) initiates the labor
		if ( IsProxy ) return;

		var cube = other.GameObject.Components.Get<StampLandCube>();

		if ( cube.IsValid() )
		{
			_lastTouchedCube = cube;
            
			// Send the request to the Host. 
			// In s&box, [Rpc.Broadcast] with a Host check is a standard way 
			// to ensure the Host receives the signal from a Client.
			NotifyHostOfLabor( cube.GameObject, Connection.Local.Id );
            
			Log.Info( $"[LABOR] Transaction Initiated: {cube.GameObject.Name}" );
		}
	}

	[Rpc.Broadcast]
	public void NotifyHostOfLabor( GameObject cubeObj, Guid playerId )
	{
		// Internal Firewall: Only the Host modifies the authoritative state
		if ( !Networking.IsHost ) return;
		if ( !cubeObj.IsValid() ) return;

		var cube = cubeObj.Components.Get<StampLandCube>();
		if ( cube.IsValid() )
		{
			// This is the bridge to your Ledger logic
			cube.ProcessTouch( playerId );
		}
	}

	public void OnTriggerExit( Collider other ) 
	{
		if ( IsProxy ) return;

		if ( _lastTouchedCube.IsValid() && _lastTouchedCube.GameObject == other.GameObject )
		{
			Log.Info( $"[LABOR] Transaction Finalized: {_lastTouchedCube.GameObject.Name}" );
			_lastTouchedCube = null;
		}
	}
}
