using Sandbox;
using System.Linq;

namespace Sinvest;

public sealed class PlayerInventoryManager : Component
{
	public static PlayerInventoryManager Instance { get; private set; }

	private bool _hasPhone;
	
	[Property] public bool HasPhone 
	{ 
		get 
		{
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

		// Reconstruct state from the local session data loaded by GameSaveSystem
		RestoreFromSession();
	}

	private void RestoreFromSession()
	{
		var session = GameSaveSystem.Instance?.CurrentCharacter;
		if ( session != null && session.UnlockedItems.Contains( "item_phone" ) )
		{
			SetPhoneEnabled( true );
		}
	}

	public void UnlockPhone()
	{
		// If already unlocked in RAM, skip to avoid duplicate ledger entries
		if ( _hasPhone ) return;

		SetPhoneEnabled( true );
   
		// Commit to the local plaintext ledger only
		GameSaveSystem.Instance?.CommitItemTransaction( "item_phone" );
	}

	private void SetPhoneEnabled( bool state )
	{
		_hasPhone = state;
		Log.Info( $"Inventory: Phone status set to {state}" );
	}
}
