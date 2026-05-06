using Sandbox;
using System.Linq;
using System.Threading.Tasks;
using Sandbox.Network;

namespace Sinvest;

/// <summary>
/// Handles lobby creation and player instantiation for the Wage Wars multiplayer mode.
/// Implements INetworkListener to react to player connections.
/// </summary>
public sealed class WageWarsNetworkHelper : Component, Component.INetworkListener
{
    [Property] public bool StartServer { get; set; } = true;
    [Property] public GameObject PlayerPrefab { get; set; }

    protected override async Task OnLoad()
    {
       // Prevents the editor from automatically creating lobbies during scene editing.
       if ( Scene.IsEditor ) return;

       // Automatically initializes a networking session if configured as a server/host.
       if ( StartServer && !Networking.IsActive )
       {
          Networking.CreateLobby( new LobbyConfig() );
       }
    }

    /// <summary>
    /// Called when a client has successfully joined and is ready to inhabit the scene.
    /// </summary>
    public async void OnActive( Connection channel )
    {
       // Safety delay to ensure WageWarsManager has finished generating the cube grid.
       // Mechanical Necessity: Prevents players from falling through the world before cubes exist.
       await Task.DelayRealtimeSeconds( 0.5f );

       if ( !PlayerPrefab.IsValid() ) return;

       var spawnTransform = FindSpawnLocation();
       
       // Creates a local instance of the player at the designated spawn point.
       var player = PlayerPrefab.Clone( spawnTransform );
       
       // Physics Reset: Prevents inherited velocity from causing "tunnelling" through thin cube colliders.
       var body = player.Components.Get<Rigidbody>( FindMode.EverythingInSelfAndChildren );
       if ( body.IsValid() ) body.Velocity = Vector3.Zero;

       // Assigns ownership of the GameObject to the connecting channel and synchronizes it.
       player.NetworkSpawn( channel );
    }

    /// <summary>
    /// Determines the optimal starting position for a new player.
    /// </summary>
    private Transform FindSpawnLocation()
    {
       // Prioritize designated SpawnPoints placed manually in the Hammer editor or Scene.
       var spawnPoint = Scene.GetAllComponents<SpawnPoint>().FirstOrDefault();
       if ( spawnPoint.IsValid() ) return spawnPoint.WorldTransform;

       // Fallback: Spawns the player 150 units above this component's position.
       // Logic: Provides enough height to clear the initial "Level 1" cube layer.
       return WorldTransform.WithPosition( WorldPosition + Vector3.Up * 150 );
    }
}
