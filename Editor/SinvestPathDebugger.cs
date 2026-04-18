using Sandbox;
using Editor;
using System;
using System.Linq;
using System.Collections.Generic;

namespace Sinvest;

public static class SinvestPathDebugger
{
    [Menu( "Editor", "Sinvest/Debug Path Problems" )]
    public static void DebugPaths()
    {
        var fs = Editor.FileSystem.Root;
        
        Log.Info( "--- STARTING SINVEST DEEP PATH DEBUG ---" );
        Log.Info( "Current FS Root Physical Path: " + fs.GetFullPath( "." ) );

        // This is the path we WANT to work
        string targetPath = "Assets/UI/Casino/Cards/art/Cards_large";
        string[] segments = targetPath.Split( '/' );
        string currentPath = "";

        // Test each segment to find the point of failure
        for ( int i = 0; i < segments.Length; i++ )
        {
            if ( i > 0 ) currentPath += "/";
            currentPath += segments[i];

            bool exists = fs.DirectoryExists( currentPath );
            Log.Info( (exists ? "[OK] " : "[FAIL] ") + "Checking: " + currentPath );

            if ( exists )
            {
                // If it exists, let's see what's INSIDE it to help us find the next step
                var subDirs = fs.FindDirectory( currentPath );
                if ( subDirs.Any() )
                {
                    Log.Info( "      Subfolders found: " + string.Join( ", ", subDirs ) );
                }
                else
                {
                    Log.Info( "      (Folder is empty or no subdirectories)" );
                }
            }
            else
            {
                Log.Error( "!!! Path search broke at: " + currentPath );
                
                // Let's try a case-insensitive search at this specific level
                string parent = i == 0 ? "." : currentPath.Substring( 0, currentPath.LastIndexOf( '/' ) );
                var actualDirs = fs.FindDirectory( parent );
                
                Log.Warning( "      Available folders in parent '" + parent + "':" );
                foreach ( var d in actualDirs )
                {
                    Log.Warning( "      > " + d );
                }
                break; 
            }
        }

        Log.Info( "--- DEBUG FINISHED ---" );
    }
}
