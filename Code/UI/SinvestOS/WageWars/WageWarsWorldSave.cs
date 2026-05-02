using Sandbox;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Manages persistence of the WageWars world state (cube ownership and stack heights)
/// in a dedicated save file, completely separate from any CharacterSession / GameSaveSystem data.
///
/// SETUP: Add this component to the same GameObject as WageWarsManager.
///
/// Save file location: {FileSystem.Data}/wagewars_world.json  (host-side only)
///
/// Identity strategy
/// -----------------
/// In-session ownership uses WageWarsPlayer.GameObject.Id (Guid) for fast per-frame
/// comparisons. That Guid is ephemeral — it changes every session. For persistence we
/// use the Steam ID (ulong), which is stable across reconnects. WageWarsCube stores
/// BOTH via [Sync] so all clients always see the current authoritative values.
///
/// Restore flow
/// ------------
/// 1. Host calls LoadWorld() after GenerateWorld().
///    → Sets cube.Value and cube.OwnerSteamId; OwnerId stays Guid.Empty.
/// 2. Each player's WageWarsPlayer.OnStart() broadcasts ClaimRestoredCubes().
///    → Host finds every cube whose OwnerSteamId matches and fills in OwnerId.
/// </summary>
public sealed class WageWarsWorldSave : Component
{
	public static WageWarsWorldSave Instance { get; private set; }

	private const string SavePath = "wagewars_world.json";

	// ── Data contract ─────────────────────────────────────────────────────────

	/// <summary>Serialisable snapshot of a single claimed cube.</summary>
	public sealed class CubeState
	{
		public int GridX    { get; set; }
		public int GridY    { get; set; }
		public ulong SteamId { get; set; }   // Stable owner identifier
		public int Value    { get; set; }
	}

	/// <summary>Root save document.</summary>
	public sealed class WorldSaveData
	{
		public List<CubeState> Cubes  { get; set; } = new();
		public long            SavedAt { get; set; } // Unix timestamp (UTC)
	}

	// ── Lifecycle ─────────────────────────────────────────────────────────────

	protected override void OnAwake()
	{
		Instance = this;
	}

	// ── Public API ────────────────────────────────────────────────────────────

	/// <summary>
	/// Writes all currently-claimed cubes to disk. Host-only; silently skips on clients.
	/// Call after any ProcessTouch that changes cube state.
	/// </summary>
	public void SaveWorld()
	{
		if ( !Networking.IsHost ) return;

		var data = new WorldSaveData
		{
			SavedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
		};

		foreach ( var cube in Scene.GetAllComponents<WageWarsCube>() )
		{
			// Only persist tiles that are actively owned
			if ( cube.Value <= 0 || cube.OwnerSteamId == 0 ) continue;

			data.Cubes.Add( new CubeState
			{
				GridX   = cube.GridPosition.x,
				GridY   = cube.GridPosition.y,
				SteamId = cube.OwnerSteamId,
				Value   = cube.Value
			} );
		}

		var json = JsonSerializer.Serialize( data, new JsonSerializerOptions { WriteIndented = false } );
		FileSystem.Data.WriteAllText( SavePath, json );

		Log.Info( $"[WAGEWARS SAVE] World saved — {data.Cubes.Count} active cubes." );
	}

	/// <summary>Returns true if a save file exists on disk.</summary>
	public bool HasSaveData() => FileSystem.Data.FileExists( SavePath );

	/// <summary>
	/// Reads the save file and restores cube state into the live scene.
	/// Must be called after GenerateWorld() so the cube map is populated.
	/// Host-only; silently skips on clients.
	/// OwnerId (Guid) is left as Guid.Empty — it is filled in by ClaimRestoredCubes
	/// as each player's WageWarsPlayer announces itself.
	/// </summary>
	public void LoadWorld()
	{
		if ( !Networking.IsHost ) return;
		if ( !FileSystem.Data.FileExists( SavePath ) )
		{
			Log.Info( "[WAGEWARS SAVE] No save file found — starting fresh." );
			return;
		}

		try
		{
			var json = FileSystem.Data.ReadAllText( SavePath );
			if ( string.IsNullOrWhiteSpace( json ) ) return;

			var data = JsonSerializer.Deserialize<WorldSaveData>( json );
			if ( data == null ) return;

			int restored = 0;
			foreach ( var state in data.Cubes )
			{
				var gridPos = new Vector2Int( state.GridX, state.GridY );
				var cube    = WageWarsManager.Instance?.GetCubeAt( gridPos );
				if ( cube == null ) continue;

				// Restore synced state — OwnerId will be filled when the player reconnects
				cube.OwnerSteamId = state.SteamId;
				cube.Value        = state.Value;
				cube.OwnerId      = Guid.Empty; // Remapped by ClaimRestoredCubes

				// Rebuild the visual stack
				cube.RestoreVisualLayers();

				restored++;
			}

			Log.Info( $"[WAGEWARS SAVE] World loaded — {restored}/{data.Cubes.Count} cubes restored." );
		}
		catch ( Exception ex )
		{
			Log.Error( $"[WAGEWARS SAVE] Failed to load world save: {ex.Message}" );
		}
	}

	/// <summary>
	/// Remaps saved cubes whose OwnerSteamId matches the reconnecting player back to
	/// their new session Guid. Broadcast by WageWarsPlayer on start so it runs on the host.
	/// </summary>
	[Rpc.Broadcast]
	public void ClaimRestoredCubes( ulong steamId, Guid newPlayerId )
	{
		if ( !Networking.IsHost ) return;
		if ( steamId == 0 ) return;

		int claimed = 0;
		foreach ( var cube in Scene.GetAllComponents<WageWarsCube>() )
		{
			// Only remap cubes that are still waiting for their owner to reconnect
			if ( cube.OwnerSteamId == steamId && cube.OwnerId == Guid.Empty )
			{
				cube.OwnerId = newPlayerId;
				claimed++;
			}
		}

		if ( claimed > 0 )
			Log.Info( $"[WAGEWARS SAVE] Re-linked {claimed} cubes to reconnected player (SteamId={steamId})." );
	}

	/// <summary>Wipes the world save file. Useful for debug or end-of-round resets.</summary>
	public void DeleteSave()
	{
		if ( FileSystem.Data.FileExists( SavePath ) )
		{
			FileSystem.Data.DeleteFile( SavePath );
			Log.Info( "[WAGEWARS SAVE] Save file deleted." );
		}
	}
}
