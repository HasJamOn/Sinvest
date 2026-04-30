using Sandbox;
using System.Collections.Generic;

namespace Sinvest;

public sealed partial class GameSaveSystem : Component
{
	[Property] public int ActiveSlot { get; set; } = 1;
	[Property] public SceneFile WorldScene { get; set; }
    
	public CharacterSession CurrentCharacter { get; private set; } = new();
    
	private string GetPath( int slot ) => $"slot_{slot}.txt";
}
