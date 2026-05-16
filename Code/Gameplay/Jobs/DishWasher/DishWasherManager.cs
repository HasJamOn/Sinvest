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
    }

    public void SubmitActiveDish()
    {
       if ( _isSubmitting || !_currentActiveDish.IsValid() || !ActiveDirtManager.IsValid() ) return;
       
       _isSubmitting = true;

       // 1. Calculate Reward
       float cleanPct = ActiveDirtManager.CleanPercentage;
       
       // FIX 1: Change local variable type to decimal to match CalculatePayout
       decimal payout = CalculatePayout( cleanPct );

       // 2. Play Dynamic Sound & Process Ledger Transaction
       PlayTierSound( cleanPct );

       if ( payout > 0m )
       {
           // This now maps perfectly to EconomyManager.Instance.AddLaborIncome( decimal, string )
           EconomyManager.Instance?.AddLaborIncome( payout, $"Dishwashing ({cleanPct:0}%)" );
           
           bool perfectScore = cleanPct >= 96f;
           
           // FIX 2: Explicitly cast to double since FloatingCashDisplay's interface expects a double
           FloatingCashDisplay.Instance?.DisplayPayout( (double)payout, perfectScore );
       }

       // 3. Cleanup Scene Objects
       var dishObject = _currentActiveDish.GameObject;
       var plateModel = _currentActiveDish.ModelVisual;

       _currentActiveDish = null;
       ActiveDirtManager = null;

       dishObject.Destroy();
       if ( plateModel.IsValid() ) plateModel.Destroy();

       // 4. Trigger localized background replenishment task loop
       float delay = Game.Random.Float( MinRespawnDelay, MaxRespawnDelay );
       _ = QueuePileReplenish( delay );

       // 5. Instantly release the submission state so player can click another dish
       _isSubmitting = false;
    }

    private void PlayTierSound( float percentage )
    {
        // PLAY 2D/GLOBAL: By stripping out Transform.Position context,
        // these reward alerts play at equal power regardless of game coordinate offsets.
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
        // 96-100% : Considered perfect, double money ($2.00)
        if ( percentage >= 96f ) return 2.00m; // Note the 'm' suffix for literal decimals
    
        // 80-96% : Rewards 0.8 to 1.0
        if ( percentage >= 80f ) return (decimal)percentage / 100m;
    
        // 50-80% : Rewards less than 0.50
        if ( percentage >= 50f ) return ((decimal)percentage / 100m) * 0.5m;

        return 0.0m;
    }
}
