using Sandbox;
using System;
using System.Linq;
using System.Collections.Generic;

[Group( "Sinvest" )]
public sealed class StampLandDebugger : Component
{
    [Property, Group("Settings")] public float DiagnosticTraceRadius { get; set; } = 22f;

    [Button( "Generate Comprehensive Global Report" )]
    public void RunGlobalStuckDiagnostic()
    {
        // 1. GATHER ALL POTENTIAL PLAYER OBJECTS
        // We look for your logic script, the engine script, and the 'player' tag.
        var playerComponents = Scene.GetAllComponents<StampLandPlayer>().Select( x => x.GameObject );
        var engineControllers = Scene.GetAllComponents<PlayerController>().Select( x => x.GameObject );
        var taggedPlayers = Scene.GetAllObjects( true ).Where( x => x.Tags.Has( "player" ) );

        // Combine, unique-ify, and filter out nulls
        var allPlayerObjects = playerComponents
            .Concat( engineControllers )
            .Concat( taggedPlayers )
            .Where( x => x.IsValid() )
            .Distinct()
            .ToList();

        if ( !allPlayerObjects.Any() )
        {
            Log.Error( "--- [ DIAGNOSTIC FAILED ] ---" );
            Log.Warning( "CRITICAL: No players detected in the scene hierarchy." );
            Log.Info( "REASON: Your prefab might not have the 'player' tag or the scripts are missing." );
            return;
        }

        Log.Info( $"=== [ GLOBAL STUCK REPORT - {allPlayerObjects.Count} PLAYERS ] ===" );

        foreach ( var playerObj in allPlayerObjects )
        {
            AnalyzePlayer( playerObj );
        }

        Log.Info( "=== [ END OF REPORT ] ===" );
    }

    private void AnalyzePlayer( GameObject go )
    {
        var rb = go.Components.Get<Rigidbody>( FindMode.EverythingInSelfAndChildren );
        var pos = go.WorldPosition;
        string netStatus = go.IsProxy ? "PROXY (Remote)" : "LOCAL (Owner)";

        Log.Info( $"--- Target: {go.Name} [{netStatus}] ---" );
        Log.Info( $"ID: {go.Id} | Pos: {pos} | Vel: {rb?.Velocity ?? Vector3.Zero}" );

        // COMPONENT AUDIT: This tells us why detection might be failing
        var comps = go.Components.GetAll<Component>( FindMode.EverythingInSelfAndChildren )
                        .Select( c => c.GetType().Name );
        Log.Info( $"Components on Object: {string.Join( ", ", comps )}" );

        // GRID ANALYSIS
        var nearestCube = Scene.GetAllComponents<StampLandCube>()
            .OrderBy( c => Vector3.DistanceBetween( pos.WithZ( 0 ), c.WorldPosition.WithZ( 0 ) ) )
            .FirstOrDefault();

        if ( nearestCube.IsValid() )
        {
            float cubeTopZ = nearestCube.WorldPosition.z + (nearestCube.Value * 50f);
            float deltaZ = pos.z - cubeTopZ;

            Log.Info( $"Nearest Cube: {nearestCube.GridPosition} (Value: {nearestCube.Value}) | Top Z: {cubeTopZ}" );
            
            if ( deltaZ < -2f ) 
                Log.Error( $"[STUCK] Player is {Math.Abs(deltaZ):F2} units DEEP in the cube!" );
            else 
                Log.Info( $"[PHYSICS] Clearance: {deltaZ:F2} units." );
        }

        RunSpatialAudit( go );
    }

    private void RunSpatialAudit( GameObject go )
    {
        // Overlap Check
        var overlaps = Scene.Trace.Sphere( DiagnosticTraceRadius, go.WorldPosition, go.WorldPosition )
            .WithoutTags( "player" ).RunAll();

        if ( overlaps != null && overlaps.Any() )
        {
            Log.Warning( $"[OVERLAP] Inside {overlaps.Count()} objects:" );
            foreach ( var hit in overlaps ) 
                Log.Warning( $"  > {hit.GameObject.Name} (Tags: {string.Join( ',', hit.GameObject.Tags )})" );
        }

        // Downward Floor Check
        var floor = Scene.Trace.Ray( go.WorldPosition + Vector3.Up * 5, go.WorldPosition + Vector3.Down * 100 )
            .WithoutTags( "player" ).Run();

        if ( !floor.Hit ) 
            Log.Error( "[FLOOR] NO COLLISION BELOW PLAYER! Are they falling through the world?" );
        else 
            Log.Info( $"[FLOOR] Standing on: {floor.GameObject.Name}" );
    }
}
