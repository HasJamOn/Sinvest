using Sandbox; 
using Sinvest;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Manages cinematic sequences by freezing player control, switching cameras, 
/// and cycling through dialogue entries.
/// </summary>
[Title( "Cutscene Manager" )]
[Category( "Sinvest" )]
public sealed class CutsceneManager : Component
{
    /// <summary>
    /// Represents a single line of dialogue with a specific speaker.
    /// Used in 'MultipleSpeakers' mode.
    /// </summary>
    public struct DialogueEntry
    {
       public string SpeakerName { get; set; }
       [TextArea] public string Message { get; set; }
    }

    [Property] public CameraComponent CinematicCamera { get; set; }
    [Property] public GameObject DialogueUI { get; set; }
    
    /// <summary> If true, this cutscene can only be played once per session. </summary>
    [Property] public bool OnlyTriggerOnce { get; set; } = true;

    [Header( "Dialogue Settings" )]
    [Property] public bool MultipleSpeakers { get; set; } = false;

    // SINGLE SPEAKER MODE: Useful for inner monologues or one-sided conversations.
    [Property, ShowIf( nameof( MultipleSpeakers ), false )] 
    public string DefaultSpeaker { get; set; } = "Character";

    [Property, ShowIf( nameof( MultipleSpeakers ), false )]
    public List<string> SimpleConversation { get; set; }

    // MULTI SPEAKER MODE: Used for scripted interactions between NPCs or the player.
    [Property, ShowIf( nameof( MultipleSpeakers ), true )]
    public List<DialogueEntry> ComplexConversation { get; set; }

    private GameObject _playerObject;
    private bool _inCutscene;
    private bool _hasTriggered;
    private int _currentEntryIndex = 0;

    /// <summary>
    /// Exposed to the ActionGraph to trigger via world triggers or events.
    /// Handles the heavy lifting of pausing the game-world player state.
    /// </summary>
    [ActionGraphNode( "start_cutscene" )]
    public void StartCutscene()
    {
        if ( _inCutscene ) return;
        if ( OnlyTriggerOnce && _hasTriggered ) return;

        // Find the local non-proxy player to freeze.
        var localPlayer = Scene.GetAllComponents<PlayerController>()
                        .FirstOrDefault( x => !x.IsProxy );

        if ( !localPlayer.IsValid() ) return;

        _playerObject = localPlayer.GameObject;

        var controller = _playerObject.Components.Get<PlayerController>();
        var camera = _playerObject.Components.GetInChildren<CameraComponent>();
        var rb = _playerObject.Components.GetInAncestorsOrSelf<Rigidbody>();
        var renderer = _playerObject.Components.GetInChildren<SkinnedModelRenderer>();

        // 1. PHYSICAL FREEZE: "Park" the physics body to prevent sliding/falling during the scene.
        if ( rb.IsValid() )
        {
            rb.Velocity = Vector3.Zero;
            rb.AngularVelocity = Vector3.Zero;
            rb.MotionEnabled = false; 
        }

        // 2. ANIMATION FREEZE: Reset the Citizen animator parameters so the character stays in an idle pose.
        if ( renderer.IsValid() )
        {
            renderer.Set( "move_x", 0f );
            renderer.Set( "move_y", 0f );
            renderer.Set( "move_z", 0f );
            renderer.Set( "move_groundspeed", 0f );
            renderer.Set( "wishspeed", 0f );
            renderer.Set( "forward", 0f );
            renderer.Set( "sideways", 0f );
            renderer.Set( "move_speed", 0f );
            renderer.Set( "b_grounded", true );
            renderer.Set( "b_noclip", false );
            renderer.Set( "b_swimming", false );
            renderer.Set( "jump", false );
        }

        // 3. COMPONENT SWAP: Disable player input and pivot to the Cinematic Camera.
        if ( controller.IsValid() ) controller.Enabled = false;
        if ( camera.IsValid() ) camera.Enabled = false;

        if ( CinematicCamera.IsValid() )
        {
            CinematicCamera.Enabled = true;
            CinematicCamera.Priority = 100;
        }

        // 4. UI INITIALIZATION: Enable the dialogue overlay.
        if ( DialogueUI.IsValid() ) DialogueUI.Enabled = true;

        _currentEntryIndex = 0;
        _inCutscene = true;
        _hasTriggered = true;

        DisplayCurrentDialogue();
    }

    /// <summary>
    /// Fetches the IDialogueUI interface and pushes the current string data to it.
    /// </summary>
    private void DisplayCurrentDialogue()
    {
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

       // Progression via the 'USE' key (standard E bind).
       if ( Input.Pressed( "use" ) )
       {
          _currentEntryIndex++;

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

    /// <summary>
    /// Restores player movement and camera controls, ensuring velocities are reset 
    /// to prevent jerky movement on return.
    /// </summary>
    public void EndCutscene()
    {
       if ( _playerObject.IsValid() )
       {
          var controller = _playerObject.Components.Get<PlayerController>( true );
          var camera = _playerObject.Components.GetInChildren<CameraComponent>( true );
          var rb = _playerObject.Components.GetInAncestorsOrSelf<Rigidbody>();

          if ( controller.IsValid() ) controller.Enabled = true;

          // RESTORE PHYSICS: Wake the body back up and ensure it doesn't snap to a previous velocity.
          if ( rb.IsValid() )
          {
             rb.MotionEnabled = true; 
             rb.Velocity = Vector3.Zero;
             rb.AngularVelocity = Vector3.Zero;
          }

          if ( camera.IsValid() )
          {
             camera.Enabled = true;
             camera.Priority = 101; // Override cinematic camera priority
          }

          // Reset the CharacterController (if used) to ensure a clean handoff to movement.
          var cc = _playerObject.Components.Get<CharacterController>( true );
          if ( cc.IsValid() ) cc.Velocity = Vector3.Zero;
       }

       if ( CinematicCamera.IsValid() ) CinematicCamera.Enabled = false;
       if ( DialogueUI.IsValid() ) DialogueUI.Enabled = false;

       // Clear buffered inputs so the player doesn't jump/interact instantly upon exit.
       Input.ClearActions();
       _inCutscene = false;
    }
}
