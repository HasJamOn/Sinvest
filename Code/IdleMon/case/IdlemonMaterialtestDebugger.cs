using Sandbox;
using System.Linq;

public sealed class IdlemonDebugger : Component
{
	[Property, Group( "References" )] public IdlemonCase Case { get; set; }
    
	[Property, Group( "Settings" )] public int ManualId { get; set; } = 400; // Portal

	protected override void OnStart()
	{
		if ( Case == null )
		{
			Case = Components.Get<IdlemonCase>();
		}
	}

	[Button( "Cycle Next (Bootstrap)", "refresh" )]
	public void CycleNext()
	{
		if ( Case == null ) { Log.Warning("No IdlemonCase found!"); return; }
		Case.CycleIdlemon();
	}

	[Button( "Generate Manual ID", "fingerprint" )]
	public void GenerateManual()
	{
		if ( Case == null ) return;
		// We use _ = to fire and forget the Task
		_ = Case.ApplyStatsFromId( ManualId );
	}

	[Button( "Generate Random ID (From Pool)", "casino" )]
	public void GenerateRandom()
	{
		if ( Case == null ) return;

		// Check if the pool actually has IDs to choose from
		if ( Case.IdPool != null && Case.IdPool.Count > 0 )
		{
			// Pick a random ID that we KNOW exists on Steam
			ManualId = Game.Random.FromList( Case.IdPool );
			_ = Case.ApplyStatsFromId( ManualId );
		}
		else
		{
			Log.Warning( "Cannot generate random: IdlemonCase Pool is empty!" );
		}
	}
}
