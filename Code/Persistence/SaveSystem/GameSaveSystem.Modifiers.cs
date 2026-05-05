using Sandbox;

namespace Sinvest;

/// <summary>
/// This partial handles progression logic and modifier validation.
/// It determines which starting perks are available to the player based on their
/// global achievements, editor status, or specific "graduation" milestones.
/// </summary>
public partial class GameSaveSystem
{
	/// <summary>
	/// Placeholder for a "Prestige" or "End-Game" check. 
	/// Returning true would signify the player has completed a primary loop 
	/// and potentially unlocked secondary rewards.
	/// </summary>
	public bool HasGraduated() => false; 

	/// <summary>
	/// Evaluates if a specific StartingModifier is available for selection.
	/// This acts as a gatekeeper for specialized playstyles (like DevMode or MentorDad).
	/// </summary>
	public bool IsModifierUnlocked( StartingModifiers mod )
	{
		// System-level check: DevMode is only available if the game is running in the s&box Editor.
		if ( mod == StartingModifiers.DevMode ) return Game.IsEditor;

		// Progression check: The MentorDad perk is available to anyone who hasn't "Graduated" yet.
		// This could be interpreted as a catch-up mechanic for newer players.
		if ( mod == StartingModifiers.MentorDad && !HasGraduated() ) return true;

		// TODO: Check the UnlockedAchievements HashSet or persistent disk stats 
		// to unlock modifiers like 'LadyLuck' or 'Nepokid' based on past play.
		return false;
	}
}
