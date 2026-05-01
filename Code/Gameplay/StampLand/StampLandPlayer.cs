using Sandbox;
using System;

public sealed class StampLandPlayer : Component, Component.ITriggerListener
{
	private StampLandCube _lastTouchedCube;

	public void OnTriggerEnter( Collider other )
	{
		// We only want the local player's inputs to trigger the RPC
		if ( IsProxy ) return;

		var cube = other.GameObject.Components.Get<StampLandCube>();

		if ( cube.IsValid() )
		{
			_lastTouchedCube = cube;
            
			// FIX: Use Connection.Local.Id. This is the persistent Guid 
			// that matches Connection.Local.Id in the cube's UpdateVisuals check.
			Guid myPlayerId = Connection.Local.Id;
            
			// Notify the Host to process the labor
			UpdateCubeOnHost( cube.GameObject, myPlayerId );
            
			Log.Info( $"[LABOR] Entered {cube.GameObject.Name} with ID: {myPlayerId}" );
		}
	}

	// Broadcast ensures the call reaches the Host from any client
	[Rpc.Broadcast]
	public void UpdateCubeOnHost( GameObject cubeObj, Guid playerId )
	{
		// Only the Host modifies the 'Ledger' (the synced Value and OwnerId)
		if ( !Networking.IsHost ) return;
		if ( !cubeObj.IsValid() ) return;

		var cube = cubeObj.Components.Get<StampLandCube>();
		if ( cube.IsValid() )
		{
			cube.ProcessTouch( playerId );
		}
	}

	public void OnTriggerExit( Collider other ) 
	{
		if ( IsProxy ) return;

		if ( _lastTouchedCube.IsValid() && _lastTouchedCube.GameObject == other.GameObject )
		{
			Log.Info( $"[LABOR] Left {_lastTouchedCube.GameObject.Name}" );
			_lastTouchedCube = null;
		}
	}
}
