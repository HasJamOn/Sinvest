namespace Sinvest; 

/// <summary>
/// Defines the contract for any UI component capable of displaying dialogue.
/// This abstraction allows the CutsceneManager to push data without needing 
/// to know the internal implementation of the UI (e.g., Panorama or World Labels).
/// </summary>
public interface IDialogueUI
{
	/// <summary>
	/// Updates the UI display with the current speaker's name and their dialogue text.
	/// </summary>
	/// <param name="name">The name of the character currently speaking.</param>
	/// <param name="text">The message or dialogue line to be displayed.</param>
	void UpdateDialogue( string name, string text );
}
