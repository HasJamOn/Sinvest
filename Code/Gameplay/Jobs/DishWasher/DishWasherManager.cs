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

    [Property] public float MinRespawnDelay { get; set; } = 2.0f;
    [Property] public float MaxRespawnDelay { get; set; } = 5.0f;

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
       double payout = CalculatePayout( cleanPct );

       // 2. Process Ledger Transaction
       if ( payout > 0 )
       {
	       EconomyManager.Instance?.AddLaborIncome( payout, $"Dishwashing ({cleanPct:0}%)" );
    
	       // Pass along whether the completion reached perfect scoring thresholds
	       bool perfectScore = cleanPct >= 96f;
	       FloatingCashDisplay.Instance?.DisplayPayout( payout, perfectScore );
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

    private async Task QueuePileReplenish( float secondsDelay )
    {
        await Task.Delay( (int)(secondsDelay * 1000) );

        if ( DishPile.Instance.IsValid() )
        {
            DishPile.Instance.SpawnSingleDish();
        }
    }

    private double CalculatePayout( float percentage )
    {
        // 96-100% : Considered perfect, double money ($2.00)
        if ( percentage >= 96f ) return 2.00;
        
        // 80-96% : Rewards 0.8 to 1.0 (Direct decimal mapping works perfectly here)
        if ( percentage >= 80f ) return percentage / 100f;
        
        // 50-80% : Rewards less than 0.50 (Scaling the percentage down by half ensures it stays < 0.50)
        if ( percentage >= 50f ) return (percentage / 100f) * 0.5f;

        // < 50% : No reward
        return 0.0;
    }
}
