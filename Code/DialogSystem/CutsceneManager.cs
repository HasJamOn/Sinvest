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

	    // Find the local player
	    var localPlayer = Scene.GetAllComponents<PlayerController>()
	                    .FirstOrDefault( x => !x.IsProxy );

	    if ( !localPlayer.IsValid() ) return;

	    _playerObject = localPlayer.GameObject;

	    // Get all necessary components for the "Freeze"
	    var controller = _playerObject.Components.Get<PlayerController>();
	    var camera = _playerObject.Components.GetInChildren<CameraComponent>();
	    var rb = _playerObject.Components.GetInAncestorsOrSelf<Rigidbody>();
	    var renderer = _playerObject.Components.GetInChildren<SkinnedModelRenderer>();

	    // 1. PHYSICAL FREEZE
	    // We stop the Rigidbody entirely so it doesn't slide with existing momentum
	    if ( rb.IsValid() )
	    {
	        rb.Velocity = Vector3.Zero;
	        rb.AngularVelocity = Vector3.Zero;
	        rb.MotionEnabled = false; // "Park" the physics body
	    }

	    // 2. ANIMATION FREEZE
	    // We manually zero out the parameters that the Citizen animator uses
	    if ( renderer.IsValid() )
	    {
	        // Standard movement floats
	        renderer.Set( "move_x", 0f );
	        renderer.Set( "move_y", 0f );
	        renderer.Set( "move_z", 0f );
	        renderer.Set( "move_groundspeed", 0f );
	        renderer.Set( "wishspeed", 0f );
	        renderer.Set( "forward", 0f );
	        renderer.Set( "sideways", 0f );
	        renderer.Set( "move_speed", 0f );

	        // State booleans
	        renderer.Set( "b_grounded", true );
	        renderer.Set( "b_noclip", false );
	        renderer.Set( "b_swimming", false );
	        
	        // Reset any active triggers
	        renderer.Set( "jump", false );
	    }

	    // 3. COMPONENT SWAP
	    // Disable player control but enable our cinematic view
	    if ( controller.IsValid() ) controller.Enabled = false;
	    if ( camera.IsValid() ) camera.Enabled = false;

	    if ( CinematicCamera.IsValid() )
	    {
	        CinematicCamera.Enabled = true;
	        CinematicCamera.Priority = 100;
	    }

	    // 4. UI INITIALIZATION
	    if ( DialogueUI.IsValid() ) DialogueUI.Enabled = true;

	    _currentEntryIndex = 0;
	    _inCutscene = true;
	    _hasTriggered = true;

	    DisplayCurrentDialogue();
	}

	private void DisplayCurrentDialogue()
	{
		// Look for the interface instead of the class
		var ui = DialogueUI.Components.Get<IDialogueUI>( FindMode.EverythingInSelfAndChildren );

		if ( ui == null )
		{
			Log.Warning( "CutsceneManager: DialogueUI does not have an IDialogueUI component!" );
			return;
		}

		if ( MultipleSpeakers )
		{
			if ( ComplexConversation == null || _currentEntryIndex >= ComplexConversation.Count ) return;
			var entry = ComplexConversation[_currentEntryIndex];
        
			ui.UpdateDialogue( entry.SpeakerName, entry.Message );
		}
		else
		{
			if ( SimpleConversation == null || _currentEntryIndex >= SimpleConversation.Count ) return;
        
			ui.UpdateDialogue( DefaultSpeaker, SimpleConversation[_currentEntryIndex] );
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
			var rb = _playerObject.Components.GetInAncestorsOrSelf<Rigidbody>();

			// 1. Re-enable the controller first
			if ( controller.IsValid() ) controller.Enabled = true;

			// 2. RESTORE PHYSICS
			if ( rb.IsValid() )
			{
				rb.MotionEnabled = true; 
            
				// Force the Rigidbody to wake up and sync with its current position
				rb.Velocity = Vector3.Zero;
				rb.AngularVelocity = Vector3.Zero;
			}

			// 3. Restore Camera
			if ( camera.IsValid() )
			{
				camera.Enabled = true;
				camera.Priority = 101;
			}

			// 4. Reset CharacterController velocity if one exists 
			// (PlayerController often uses this internally)
			var cc = _playerObject.Components.Get<CharacterController>( true );
			if ( cc.IsValid() ) cc.Velocity = Vector3.Zero;
		}

		// Clean up Cutscene elements
		if ( CinematicCamera.IsValid() ) CinematicCamera.Enabled = false;
		if ( DialogueUI.IsValid() ) DialogueUI.Enabled = false;

		Input.ClearActions();
		_inCutscene = false;
	}
}
