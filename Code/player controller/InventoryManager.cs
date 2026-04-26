using Sandbox;
using Sandbox.Services;

namespace Sinvest;

public sealed class InventoryManager : Component
{
	// 1. ADD THIS PROPERTY
	public static InventoryManager Instance { get; private set; }

	[Property] public bool HasPhone { get; private set; }

	// 2. ADD THIS METHOD TO ASSIGN THE INSTANCE
	protected override void OnAwake()
	{
		Instance = this;
	}

	protected override void OnStart()
	{
		if ( IsProxy ) return;

		LoadInventoryFromCloud();
	}

	private void LoadInventoryFromCloud()
	{
		// Ensure your SinvestSession helper is accessible here
		string phoneKey = SinvestSession.GetSlotKey( "has_phone" );

		var stat = Stats.LocalPlayer.Get( phoneKey );
    
		if ( stat.Value > 0 )
		{
			SetPhoneEnabled( true );
		}
	}

	public void UnlockPhone()
	{
		if ( HasPhone ) return;

		SetPhoneEnabled( true );
		Stats.SetValue( SinvestSession.GetSlotKey( "has_phone" ), 1 );

		if ( !Achievements.All.Any( x => x.Name == "disconnected" && x.IsUnlocked ) )
		{
			Achievements.Unlock( "disconnected" );
			Log.Info( "Meta-Progression: Phone modifier unlocked for all future characters!" );
		}

		Stats.Flush();
	}

	private void SetPhoneEnabled( bool state )
	{
		HasPhone = state;
		Log.Info( $"Inventory: Phone status set to {state}" );
	}
}
