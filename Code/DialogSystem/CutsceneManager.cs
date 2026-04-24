using Sandbox;
using System.Collections.Generic;
using System.Linq;

[Title( "Cutscene Manager" )]
[Category( "Sinvest" )]
public sealed class CutsceneManager : Component
{
	public struct DialogueEntry
	{
		public string SpeakerName { get; set; }
		[TextArea] public string Message { get; set; }
	}

	[Property] public CameraComponent CinematicCamera { get; set; }
	[Property] public GameObject DialogueUI { get; set; }
	[Property] public bool OnlyTriggerOnce { get; set; } = true;

	[Header( "Dialogue Settings" )]
	[Property] public bool MultipleSpeakers { get; set; } = false;

	// SINGLE SPEAKER MODE
	[Property, ShowIf( nameof( MultipleSpeakers ), false )] 
	public string DefaultSpeaker { get; set; } = "Character";

	[Property, ShowIf( nameof( MultipleSpeakers ), false )]
	public List<string> SimpleConversation { get; set; }

	// MULTI SPEAKER MODE
	[Property, ShowIf( nameof( MultipleSpeakers ), true )]
	public List<DialogueEntry> ComplexConversation { get; set; }

	private GameObject _playerObject;
	private bool _inCutscene;
	private bool _hasTriggered;
	private int _currentEntryIndex = 0;

	[ActionGraphNode( "start_cutscene" )]
	public void StartCutscene()
	{
		if ( _inCutscene ) return;
		if ( OnlyTriggerOnce && _hasTriggered ) return;

		var localPlayer = Scene.GetAllComponents<PlayerController>()
							 .FirstOrDefault( x => !x.IsProxy );

		if ( !localPlayer.IsValid() ) return;

		_playerObject = localPlayer.GameObject;

		var controller = _playerObject.Components.Get<PlayerController>();
		var camera = _playerObject.Components.GetInChildren<CameraComponent>();

		if ( controller.IsValid() ) controller.Enabled = false;
		if ( camera.IsValid() ) camera.Enabled = false;

		if ( CinematicCamera.IsValid() )
		{
			CinematicCamera.Enabled = true;
			CinematicCamera.Priority = 100;
		}

		if ( DialogueUI.IsValid() ) DialogueUI.Enabled = true;

		_currentEntryIndex = 0;
		_inCutscene = true;
		_hasTriggered = true;

		DisplayCurrentDialogue();
	}

	private void DisplayCurrentDialogue()
	{
		var worldUI = DialogueUI.Components.Get<WorldDialogue>();
		if ( !worldUI.IsValid() ) return;

		if ( MultipleSpeakers )
		{
			if ( ComplexConversation == null || _currentEntryIndex >= ComplexConversation.Count ) return;
			var entry = ComplexConversation[_currentEntryIndex];
			worldUI.UpdateDialogue( entry.SpeakerName, entry.Message );
		}
		else
		{
			if ( SimpleConversation == null || _currentEntryIndex >= SimpleConversation.Count ) return;
			worldUI.UpdateDialogue( DefaultSpeaker, SimpleConversation[_currentEntryIndex] );
		}
	}

	protected override void OnUpdate()
	{
		if ( !_inCutscene ) return;

		if ( Input.Pressed( "use" ) )
		{
			_currentEntryIndex++;

			// Check the correct list based on the toggle
			int totalCount = MultipleSpeakers ? (ComplexConversation?.Count ?? 0) : (SimpleConversation?.Count ?? 0);

			if ( _currentEntryIndex < totalCount )
			{
				DisplayCurrentDialogue();
			}
			else
			{
				EndCutscene();
			}
		}
	}

	public void EndCutscene()
	{
		if ( _playerObject.IsValid() )
		{
			var controller = _playerObject.Components.Get<PlayerController>( true );
			var camera = _playerObject.Components.GetInChildren<CameraComponent>( true );

			if ( controller.IsValid() ) controller.Enabled = true;
			if ( camera.IsValid() )
			{
				camera.Enabled = true;
				camera.Priority = 101;
			}

			var cc = _playerObject.Components.Get<CharacterController>( true );
			if ( cc.IsValid() ) cc.Velocity = Vector3.Zero;
		}

		if ( CinematicCamera.IsValid() ) CinematicCamera.Enabled = false;
		if ( DialogueUI.IsValid() ) DialogueUI.Enabled = false;

		Input.ClearActions();
		_inCutscene = false;
	}
}
