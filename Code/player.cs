using Sandbox;
using System.Linq;

public sealed class Player : Component
{
	[Property] public bool WantsHideHud { get; set; } = false;

	public static Player FindLocalPlayer()
	{
		return Game.ActiveScene.GetAllComponents<Player>()
			.FirstOrDefault( x => x.Network.IsOwner );
	}
}
