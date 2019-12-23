﻿using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;

namespace Cudafy
{
    /// <summary>This utility class resolves path to nVidia's nvcc.exe and Microsoft's cl.exe.</summary>
    internal static class NvccExe
    {
        /// <summary>Get GPU Computing Toolkit 7.0 installation path.</summary>
        /// <remarks>Throws an exception if it's not installed.</remarks>
        static string getToolkitBaseDir()
        {
            //Find Computing Toolkit in the default path
            string prFil = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"NVIDIA GPU Computing Toolkit\CdUDA");

            if (Directory.Exists(prFil))
            {
                string[] ctDirs = Directory.GetDirectories(prFil);
                if(ctDirs.Length > 0)
                    for (int i = ctDirs.Length; i > 0; i--)
                        return Path.Combine(prFil, ctDirs[i - 1]);
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("nVidia GPU Toolkit error: Could not find 'NVIDIA GPU Computing Toolkit' in default path. Modify the Cudafy/Compilers/NvccExe.cs file to change the path.");
                Console.ResetColor();
            }

            throw new CudafyCompileException("nVidia GPU Toolkit error: Computing Toolkit was not found");
        }


        static readonly string toolkitBaseDir = getToolkitBaseDir();

        const string csNVCC = "nvcc.exe";

        /// <summary>Path to the nVidia's toolkit bin folder where nvcc.exe is located.</summary>
        public static string getCompilerPath()
        {
            return Path.Combine( toolkitBaseDir, "bin", csNVCC );
        }

        /// <summary>Path to the nVidia's toolkit's include folder.</summary>
        public static string getIncludePath()
        {
            return Path.Combine( toolkitBaseDir, @"include" );
        }

        /// <summary>Path to the Microsoft's visual studio folder where cl.exe is localed.</summary>
        public static string getClExeDirectory()
        {
            //Search using vswhere.exe
            Process getVS = new Process
            {
                StartInfo = {
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        FileName = "vswhere.exe",
                        Arguments = " -latest -property installationPath"
                }
            };
            getVS.Start();
            string vsPath = Path.GetFullPath(Path.Combine(getVS.StandardOutput.ReadLine(), @"VC\Tools\MSVC"));
            getVS.WaitForExit();

            string[] vsDirs = Directory.GetDirectories(vsPath);

            string coVer = @"bin\Hostx64\x86";
            if (Environment.Is64BitProcess)
                coVer = @"bin\Hostx64\x64";


            if (vsDirs.Length > 0)
                for (int i = vsDirs.Length; i > 0; i--)
                    if (File.Exists(Path.Combine(vsDirs[i - 1], coVer + @"\cl.exe")))
                        return Path.Combine(vsDirs[i - 1], coVer);

            //Traditional method of searching by the registry
            string[] versionsToTry = new string[] { "12.0", "11.0" };
            RegistryKey localKey;
            if( Environment.Is64BitProcess )
                localKey = RegistryKey.OpenBaseKey( RegistryHive.LocalMachine, RegistryView.Registry32 );
            else
                localKey = Registry.LocalMachine;

            RegistryKey vStudio = localKey.OpenSubKey( @"SOFTWARE\Wow6432Node\Microsoft\VisualStudio" );
            if( null == vStudio )
                throw new CudafyCompileException( "nVidia GPU Toolkit error: visual studio was not found" );

            foreach( string ver in versionsToTry )
            {
                RegistryKey key = vStudio.OpenSubKey( ver );
                if( null == key )
                    continue;
                string InstallDir = key.GetValue( "InstallDir" ) as string;
                if( null == InstallDir )
                    continue;
                // C:\Program Files (x86)\Microsoft Visual Studio 12.0\Common7\IDE\

                InstallDir.TrimEnd( '\\', '/' );
                string clDir = Path.GetFullPath( Path.Combine( InstallDir, @"..\..\VC\bin" ) );

                if( Environment.Is64BitProcess )
                {
                    // In 64-bits processes we use a 64-bits compiler. If you'd like to always use the 32-bits one, remove this.
                    clDir = Path.Combine( clDir, "amd64" );
                }
                if( !Directory.Exists( clDir ) )
                    continue;

                string clPath = Path.Combine( clDir, "cl.exe" );
                if( File.Exists( clPath ) )
                    return clDir;
            }

            throw new CudafyCompileException( "nVidia GPU Toolkit error: cl.exe was not found" );
        }
    }
}