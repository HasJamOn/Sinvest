using Sandbox;
using System;

public sealed class StampLandPlayer : Component, Component.ITriggerListener
{
	private StampLandCube _lastTouchedCube;

	public void OnTriggerEnter( Collider other )
	{
		if ( IsProxy ) return;

		var cube = other.GameObject.Components.GetInAncestorsOrSelf<StampLandCube>();

		if ( cube.IsValid() )
		{
			// Calculate the world Z position of the top of this specific cube/layer
			// other.GameObject is the specific layer hit; WorldPosition is its center.
			// We add half the CubeHeight (25) to find the top surface.
			float topSurfaceZ = other.WorldPosition.z + 25f;

			// Only trigger if the player's feet (WorldPosition.z) are at or above the top surface
			// We use a small epsilon (margin of error) to account for physics jitter
			if ( WorldPosition.z >= topSurfaceZ - 5f )
			{
				_lastTouchedCube = cube;
				NotifyHostOfLabor( cube.GameObject, Connection.Local.Id );
         
				Log.Info( $"[STAMP] Top-face contact confirmed on: {cube.GridPosition}" );
			}
			else
			{
				Log.Info( "[LABOR] Side-swipe ignored. Must stand on top." );
			}
		}
	}

	[Rpc.Broadcast]
	public void NotifyHostOfLabor( GameObject cubeObj, Guid playerId )
	{
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

		// Check ancestors again to make sure we are exiting the same logical tower
		var cube = other.GameObject.Components.GetInAncestorsOrSelf<StampLandCube>();

		if ( _lastTouchedCube.IsValid() && cube == _lastTouchedCube )
		{
			Log.Info( $"[LABOR] Transaction Finalized: {_lastTouchedCube.GridPosition}" );
			_lastTouchedCube = null;
		}
	}
}
