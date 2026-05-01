using Sandbox;
using System.Linq;
using System.Threading.Tasks;
using Sandbox.Network;

namespace Sinvest;

public sealed class StampLandNetworkHelper : Component, Component.INetworkListener
{
	[Property] public bool StartServer { get; set; } = true;
	[Property] public GameObject PlayerPrefab { get; set; }

	protected override async Task OnLoad()
	{
		if ( Scene.IsEditor ) return;

		if ( StartServer && !Networking.IsActive )
		{
			Networking.CreateLobby( new LobbyConfig() );
		}
	}

	public async void OnActive( Connection channel )
	{
		// Safety delay to ensure StampLandManager has finished generating the grid
		await Task.DelayRealtimeSeconds( 0.5f );

		if ( !PlayerPrefab.IsValid() ) return;

		var spawnTransform = FindSpawnLocation();
		var player = PlayerPrefab.Clone( spawnTransform );
       
		// Ensure the player starts with zero velocity so they don't "fast pass" the floor
		var body = player.Components.Get<Rigidbody>( FindMode.EverythingInSelfAndChildren );
		if ( body.IsValid() ) body.Velocity = Vector3.Zero;

		player.NetworkSpawn( channel );
	}

	private Transform FindSpawnLocation()
	{
		// Prioritize designated SpawnPoints in the scene
		var spawnPoint = Scene.GetAllComponents<SpawnPoint>().FirstOrDefault();
		if ( spawnPoint.IsValid() ) return spawnPoint.WorldTransform;

		// Fallback: Spawn 150 units above the NetworkHelper's position 
		// to give the player a safe landing on the generated cubes.
		return WorldTransform.WithPosition( WorldPosition + Vector3.Up * 150 );
	}
}
