using Sandbox;

namespace Sinvest;

public partial class GameSaveSystem
{
	public bool HasGraduated() => false; 

	public bool IsModifierUnlocked( StartingModifiers mod )
	{
		if ( mod == StartingModifiers.DevMode ) return Game.IsEditor;
		if ( mod == StartingModifiers.MentorDad && !HasGraduated() ) return true;

		// Check persistent achievement stats here
		return false;
	}
}
