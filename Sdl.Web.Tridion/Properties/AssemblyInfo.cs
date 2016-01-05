﻿using System.Runtime.CompilerServices;
using System.Reflection;
using System.Runtime.InteropServices;

#if TRIDION_71
    [assembly: AssemblyTitle("SDL DXA Provider for SDL Tridion 2013 SP1")]
#else
    [assembly: AssemblyTitle("SDL DXA Provider for SDL Web 8 (CDaaS)")]
#endif

#if DEBUG
    [assembly: AssemblyConfiguration("Debug")]
#else
    [assembly: AssemblyConfiguration("Release")]
#endif

[assembly: AssemblyCompany("SDL Group")]
[assembly: AssemblyProduct("SDL Digital Experience Accelerator")]
[assembly: AssemblyCopyright("Copyright © 2014-2015 SDL Group")]

[assembly: ComVisible(false)]

[assembly: InternalsVisibleTo("Sdl.Web.Tridion.Tests")]

// NOTE: Version Info is generated by the build in VersionInfo.cs
