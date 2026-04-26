using Sandbox;
using Sandbox.Services;
using System.Linq;

namespace Sinvest;

public sealed class InventoryManager : Component
{
	public static InventoryManager Instance { get; private set; }

	// We keep the property but allow the debug manager to influence the getter
	private bool _hasPhone;
	[Property] public bool HasPhone 
	{ 
		get 
		{
			// If debug is forcing it, always return true
			var dbg = Scene.GetAllComponents<SinvestDebugManager>().FirstOrDefault();
			if ( dbg != null && dbg.ForceHasPhone ) return true;
            
			return _hasPhone;
		}
		private set => _hasPhone = value;
	}

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
		string phoneKey = SinvestSession.GetSlotKey( "has_phone" );
		var stat = Stats.LocalPlayer.Get( phoneKey );
    
		if ( stat.Value > 0 )
		{
			SetPhoneEnabled( true );
		}
	}

	public void UnlockPhone()
	{
		// Only skip if the ACTUAL backing variable is true
		if ( _hasPhone ) return;

		SetPhoneEnabled( true );
       
		// Save to cloud/local stats
		Stats.SetValue( SinvestSession.GetSlotKey( "has_phone" ), 1 );

		if ( !Achievements.All.Any( x => x.Name == "disconnected" && x.IsUnlocked ) )
		{
			Achievements.Unlock( "disconnected" );
			Log.Info( "Meta-Progression: Phone modifier unlocked!" );
		}

		Stats.Flush();
	}

	private void SetPhoneEnabled( bool state )
	{
		_hasPhone = state;
		Log.Info( $"Inventory: Phone status set to {HasPhone} (Backing: {_hasPhone})" );
	}
}
