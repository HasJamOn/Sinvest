using System;
using System.Collections.Generic;
using Sandbox;

namespace Sinvest;

/// <summary>
/// Core Lifecycle & Component Management.
/// This partial handles the initialization of the save system, singleton enforcement,
/// and the high-level scene transitions.
/// </summary>
public sealed partial class GameSaveSystem : Component
{
    /// <summary> Global access point for the save system. </summary>
    public static GameSaveSystem Instance { get; private set; }

    /// <summary> The currently selected save file index. Defaults to Slot 1. </summary>
    [Property] public int ActiveSlot { get; set; } = 1;

    /// <summary> 
    /// The live data container for the current player. 
    /// This is populated during the ledger replay in LoadActiveSlot(). 
    /// </summary>
    public CharacterSession CurrentCharacter { get; private set; } = new();

    /// <summary> The scene to load when the player clicks "Start" or "Continue". </summary>
    [Property] public SceneFile WorldScene { get; set; }
    
    /// <summary> Placeholder for Phase 2 cloud-sync integration. </summary>
    [Property, Group("Phase 2")] public bool ShouldUseCloud { get; set; } = false;
    
    /// <summary> 
    /// Event invoked whenever the local data changes (transactions, unlocks, etc.). 
    /// Subscribed to by the UI to trigger refreshes.
    /// </summary>
    public static System.Action OnDataChanged { get; set; }

    /// <summary> Triggers the OnDataChanged event to update any listening UI panels. </summary>
    public void NotifyDataChanged() => OnDataChanged?.Invoke();

    /// <summary> Cache of achievements unlocked in the current session. </summary>
    public HashSet<string> UnlockedAchievements { get; private set; } = new();
    
    /// <summary> Maps a slot integer to a physical filename on the local disk. </summary>
    private string GetPath( int slot ) => $"slot_{slot}.txt";

    protected override void OnAwake()
    {
       // Singleton Enforcement: Ensure only one GameSaveSystem exists at a time.
       if ( Instance.IsValid() && Instance != this )
       {
          GameObject.Destroy(); 
          return;
       }

       Instance = this;

       // Persistence: Prevent the engine from destroying this object when switching scenes.
       // This allows the CurrentCharacter data to remain in RAM during the transition.
       GameObject.Flags |= GameObjectFlags.DontDestroyOnLoad;

       // Auto-load the active slot on startup if a valid slot is assigned.
       if ( ActiveSlot > 0 )
       {
          LoadActiveSlot();
       }
    }

    /// <summary>
    /// Deletes a specific save file from the local FileSystem.
    /// </summary>
    public void DeleteSlot( int slot )
    {
       var path = GetPath( slot );
       if ( FileSystem.Data.FileExists( path ) )
       {
          FileSystem.Data.DeleteFile( path );
          Log.Info( $"Deleted save file: {path}" );
       }
    }
    
    /// <summary>
    /// Commits an achievement unlock to the ledger. 
    /// Uses a standard tag format: ACH | ID | STATUS | TIMESTAMP.
    /// </summary>
    public void LocalUnlockAchievement( string id )
    {
       // HashSet.Add returns false if the item already exists, preventing duplicate entries.
       if ( UnlockedAchievements.Add( id ) )
       {
          var path = GetPath( ActiveSlot );
          var entry = $"ACH|{id}|0|{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}\n";
            
          // Using a simple Read/Write pattern for the achievement list.
          string existingData = FileSystem.Data.ReadAllText( path ) ?? "";
          FileSystem.Data.WriteAllText( path, existingData + entry );
            
          Log.Info( $"[ACHIEVEMENT] Locally Unlocked: {id}" );
       }
    }

    /// <summary>
    /// Handles the hand-off between the Main Menu and the active game world.
    /// </summary>
    public void StartGame()
    {
       if ( WorldScene == null )
       {
          Log.Error( "WorldScene is not assigned in the GameSaveSystem Inspector!" );
          return;
       }
   
       Log.Info( $"[SAVE] Transitioning to {WorldScene.ResourceName}..." );
   
       // Final notification to ensure any persistent HUD elements prepare for the scene change.
       NotifyDataChanged();

       // Native s&box scene loading.
       Scene.Load( WorldScene );
    }
}
