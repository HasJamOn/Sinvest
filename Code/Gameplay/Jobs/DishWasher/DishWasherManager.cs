using Sandbox;
using System.Threading.Tasks;

namespace Sinvest;

public sealed class DishwasherManager : Component
{
    public static DishwasherManager Instance { get; private set; }

    /// <summary>The DirtManager of whichever dish is currently in the workspace. Read by the HUD.</summary>
    public DishwasherDirtManager ActiveDirtManager { get; private set; }

    /// <summary>The Plate_Root of whichever dish is currently in the workspace. Read by PlateRotator.</summary>
    public GameObject ActivePlateRoot => _currentActiveDish?.ModelVisual;

    [Property, Header( "Timing" )] public float MinRespawnDelay { get; set; } = 2.0f;
    [Property] public float MaxRespawnDelay { get; set; } = 5.0f;

    [Property, Header( "Audio Tiers" )] public SoundEvent PerfectSound { get; set; }
    [Property] public SoundEvent StandardPayoutSound { get; set; }
    [Property] public SoundEvent PoorJobSound { get; set; }

    [Property, Group( "Dirt Configuration Range" )] public int MinDirtCount { get; set; } = 25;
    [Property, Group( "Dirt Configuration Range" )] public int MaxDirtCount { get; set; } = 50;
    [Property, Group( "Dirt Configuration Range" )] public float GlobalDecalSize { get; set; } = 0.5f;

    /// <summary>
    /// Controls the mathematical curve of dirt distribution.
    /// 1 = Linear, 2 = Quadratic (low dirt common), 3+ = Aggressive curve (high dirt rare).
    /// </summary>
    [Property, Group( "Dirt Configuration Range" ), Range( 0.1f, 5.0f )] public float DirtSkewPower { get; set; } = 2.0f;

    private DishTransition _currentActiveDish;
    private bool _isSubmitting;

    protected override void OnAwake()
    {
       Instance = this;
    }

    protected override void OnDestroy()
    {
       if ( Instance == this ) Instance = null;
    }

    public void RequestFocus( DishTransition dish )
    {
        if ( !dish.IsValid() || _currentActiveDish == dish || _isSubmitting ) return;

        if ( _currentActiveDish.IsValid() )
           _currentActiveDish.SetMode( false );

        _currentActiveDish = dish;
        _currentActiveDish.SetMode( true );

        ActiveDirtManager = dish.DirtManager;

        if ( ActiveDirtManager.IsValid() && ActiveDirtManager.TotalScrubsNeeded == 0 )
        {
           ActiveDirtManager.SpawnDirtRobust();
        }
    }

    public void SubmitActiveDish()
    {
       if ( _isSubmitting || !_currentActiveDish.IsValid() || !ActiveDirtManager.IsValid() ) return;
       
       _isSubmitting = true;

       float cleanPct = ActiveDirtManager.CleanPercentage;
       decimal payout = CalculatePayout( cleanPct );

       PlayTierSound( cleanPct );

       // IF THEY EARNED MONEY: Process Ledger & Green Floating Text
       if ( payout > 0m )
       {
           EconomyManager.Instance?.AddLaborIncome( payout, $"Dishwashing ({cleanPct:0}%)" );
           
           bool perfectScore = cleanPct >= 96f;
           FloatingCashDisplay.Instance?.DisplayPayout( payout, perfectScore );
       }
       // IF THEY FAILED (< 50% Clean): Trigger the Red Warning UI
       else
       {
           FloatingCashDisplay.Instance?.DisplayWarning( "Too Dirty!" );
       }

       var dishObject = _currentActiveDish.GameObject;
       var plateModel = _currentActiveDish.ModelVisual;

       _currentActiveDish = null;
       ActiveDirtManager = null;

       dishObject.Destroy();
       if ( plateModel.IsValid() ) plateModel.Destroy();

       float delay = Game.Random.Float( MinRespawnDelay, MaxRespawnDelay );
       _ = QueuePileReplenish( delay );

       _isSubmitting = false;
    }

    private void PlayTierSound( float percentage )
    {
        if ( percentage >= 96f )
        {
            if ( PerfectSound != null ) Sound.Play( PerfectSound );
            return;
        }

        if ( percentage >= 50f )
        {
            if ( StandardPayoutSound != null ) Sound.Play( StandardPayoutSound );
            return;
        }

        if ( PoorJobSound != null ) Sound.Play( PoorJobSound );
    }

    private async Task QueuePileReplenish( float secondsDelay )
    {
        await Task.Delay( (int)(secondsDelay * 1000) );

        if ( DishPile.Instance.IsValid() )
        {
            DishPile.Instance.SpawnSingleDish();
        }
    }

    private decimal CalculatePayout( float percentage )
    {
        if ( percentage >= 96f ) return 2.00m; 
        if ( percentage >= 80f ) return (decimal)percentage / 100m;
        if ( percentage >= 50f ) return ((decimal)percentage / 100m) * 0.5m;

        return 0.0m;
    }
}
