using Sandbox;

namespace Sinvest;

public sealed class DishwasherManager : Component
{
	public static DishwasherManager Instance { get; private set; }

	/// <summary>The DirtManager of whichever dish is currently in the workspace. Read by the HUD.</summary>
	public DishwasherDirtManager ActiveDirtManager { get; private set; }

	/// <summary>The Plate_Root of whichever dish is currently in the workspace. Read by PlateRotator.</summary>
	public GameObject ActivePlateRoot => _currentActiveDish?.ModelVisual;

	private DishTransition _currentActiveDish;

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
		if ( !dish.IsValid() || _currentActiveDish == dish ) return;

		if ( _currentActiveDish.IsValid() )
			_currentActiveDish.SetMode( false );

		_currentActiveDish = dish;
		_currentActiveDish.SetMode( true );

		ActiveDirtManager = dish.DirtManager;
	}
}
