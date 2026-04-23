using Sandbox;
using Sandbox.UI;
using Sandbox.Movement;
using System.Linq;

public sealed class SitChairDebugger : Component
{
	protected override void OnUpdate()
	{
		// 1. Find the local PlayerController
		var controller = Scene.GetAllComponents<PlayerController>()
			.FirstOrDefault( x => !x.IsProxy );

		if ( controller == null ) 
		{
			Gizmo.Draw.ScreenText( "Searching for PlayerController...", new Vector2( 100, 100 ) );
			return; 
		}

		// 2. Use the controller's GameObject
		var playerGo = controller.GameObject;

		// 3. Gather data using GameObject properties
		var parentObj = playerGo.Parent;
		var parentName = parentObj != null ? parentObj.Name : "No Parent (Standing)";
        
		// Check for Interface 
		var hasSitInterface = playerGo.Components.Get<ISitTarget>( FindMode.EverythingInAncestors ) != null;
        
		// Check MoveMode
		var currentMode = controller.Mode;
		bool isSitMode = currentMode is SitMoveMode;
		string modeName = currentMode != null ? currentMode.GetType().Name : "None";

		// 4. Draw to screen (Simplified args to avoid "color" error)
		string debugText = "--- SIT DEBUGGER ---\n" +
		                   $"Player GO Name: {playerGo.Name}\n" +
		                   $"Parent Name: {parentName}\n" +
		                   $"Is Parented: {parentObj != null}\n" +
		                   $"Current Mode: {modeName}\n" +
		                   $"Is Mode SitMoveMode: {isSitMode}\n" +
		                   $"Interface Found: {hasSitInterface}";

		Gizmo.Draw.ScreenText( debugText, new Vector2( 100, 100 ) );
	}
}
