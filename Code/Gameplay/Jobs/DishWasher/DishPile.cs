using Sandbox;

namespace Sinvest;

public sealed class DishPile : Component
{
    public static DishPile Instance { get; private set; }

    [Property] public GameObject DishPrefab { get; set; }
    [Property] public GameObject PlateRootPrefab { get; set; }
    [Property] public GameObject WorkspaceAnchor { get; set; }

    [Property] public int DishCount { get; set; } = 5;
    [Property] public float SpreadRadius { get; set; } = 15f;
    [Property] public float StackSpacing { get; set; } = 1.5f;
    [Property, Range(0.1f, 5.0f)] public float PlateScale { get; set; } = 1.0f;

    private int _totalSpawned = 0;

    protected override void OnAwake()
    {
        Instance = this;
    }

    protected override void OnStart()
    {
       if ( !DishPrefab.IsValid() || !PlateRootPrefab.IsValid() || !WorkspaceAnchor.IsValid() ) 
       { 
           Log.Warning( "DishPile: Missing prefab or anchor references." ); 
           return; 
       }

       for ( int i = 0; i < DishCount; i++ )
       {
           SpawnSingleDish();
       }
    }

    public void SpawnSingleDish()
    {
        if ( !DishPrefab.IsValid() || !PlateRootPrefab.IsValid() ) return;

        int index = _totalSpawned++;

        // --- Spawn the pile sprite ---
        var dish = DishPrefab.Clone();
        dish.Parent = GameObject;
        dish.Name = $"Dish_{index}";

        // Wrap the angle calculation so it continues circulating the pile
        float angle = (index % DishCount / (float)DishCount) * 360f;
        float radius = SpreadRadius * Game.Random.Float( 0.6f, 1.0f );
        var offset = Rotation.FromYaw( angle ).Forward * radius;
        
        // Slightly random Z offset so they don't cleanly stack to the ceiling over time
        float zOffset = (index % DishCount) * StackSpacing + Game.Random.Float(-0.2f, 0.2f);
        offset = offset.WithZ( zOffset );

        dish.LocalPosition = offset;
        dish.LocalRotation = Rotation.FromYaw( Game.Random.Float( 0f, 360f ) );

        // --- Spawn this dish's own Plate_Root at the workspace position ---
        var plateRoot = PlateRootPrefab.Clone();
        plateRoot.Parent = GameObject;
        plateRoot.Name = $"PlateRoot_{index}";
        plateRoot.LocalScale = new Vector3( PlateScale ); 
        plateRoot.WorldPosition = WorkspaceAnchor.WorldPosition;
        plateRoot.WorldRotation = WorkspaceAnchor.WorldRotation;
        plateRoot.Enabled = false; 

        // --- Wire them together ---
        var transition = dish.Components.Get<DishTransition>();
        if ( transition.IsValid() )
        {
           transition.ModelVisual = plateRoot;
        }
    }
}
