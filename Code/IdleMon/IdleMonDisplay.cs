using Sandbox;
using Sandbox.UI;
using Sandbox.UI.Construct;
using System;

namespace Sinvest;

public class IdleMonDisplay : ScenePanel
{
    private SceneWorld _privateWorld;
    private SceneObject _monObject;
    private SceneLight _light;
    
    private Label _debugStatus;
    private Label _debugPath;

    public IdleMonDisplay()
    {
        _privateWorld = new SceneWorld();
        World = _privateWorld;

        // Camera Setup
        Camera.Position = new Vector3( -120, 0, 30 );
        Camera.Rotation = Rotation.LookAt( Vector3.Zero - Camera.Position );
        Camera.FieldOfView = 30;
        Camera.ZNear = 1;
        Camera.ZFar = 5000;

        // 1. FIX: Style.BackgroundColor expects a non-nullable Color
        // We use a fallback (??) so if the parse fails, it defaults to Black
        Style.BackgroundColor = Color.Parse( "#1a1a1a" ) ?? Color.Black;

        // 2. FIX: SceneLight requires a non-nullable Color
        var lightColor = Color.Parse( "#ffffff" ) ?? Color.White;
        _light = new SceneLight( _privateWorld, new Vector3( -50, 50, 100 ), 500, lightColor * 1.5f );

        // Setup Debug Labels
        _debugStatus = Add.Label( "Initializing...", "debug-text" );
        _debugPath = Add.Label( "", "debug-text" );
        
        // 3. FIX: Style.FontColor and PositionMode Enum
        _debugStatus.Style.FontColor = Color.Parse( "yellow" ) ?? Color.Yellow;
        _debugStatus.Style.FontSize = 12;
        _debugStatus.Style.Position = PositionMode.Absolute; // Use the Enum, not a string
        _debugStatus.Style.Left = 5;
        _debugStatus.Style.Top = 5;

        _debugPath.Style.FontColor = Color.Parse( "cyan" ) ?? Color.Cyan;
        _debugPath.Style.FontSize = 10;
        _debugPath.Style.Position = PositionMode.Absolute; // Use the Enum, not a string
        _debugPath.Style.Left = 5;
        _debugPath.Style.Top = 20;
    }

    public void SetModel( string modelPath )
    {
        if ( string.IsNullOrEmpty( modelPath ) ) return;
        if ( _monObject.IsValid() && _monObject.Model?.Name == modelPath ) return;

        _monObject?.Delete();

        var model = Model.Load( modelPath );
        if ( model == null || model.IsError ) 
        {
            _debugStatus.Text = "Model: ERROR";
            _debugPath.Text = modelPath;
            return;
        }

        _monObject = new SceneObject( _privateWorld, model, Transform.Zero );
        _debugStatus.Text = "Model: OK";
        _debugPath.Text = modelPath;
    }

    public override void Tick()
    {
        base.Tick();

        if ( _monObject.IsValid() )
        {
            _monObject.Transform = _monObject.Transform.WithRotation( 
                _monObject.Transform.Rotation * Rotation.FromYaw( Time.Delta * 40f ) 
            );
        }
    }

    public override void OnDeleted()
    {
        base.OnDeleted();
        _privateWorld?.Delete();
        _monObject?.Delete();
        _light?.Delete();
    }
}
