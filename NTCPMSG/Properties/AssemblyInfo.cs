using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
#if Dot40
[assembly: AssemblyTitle("NTCPMSG .net 4.0")]
#else
[assembly: AssemblyTitle("NTCPMSG .net 2.0")]
#endif
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("NTCPMSG")]
[assembly: AssemblyCopyright("Copyright © eaglet 2012")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a type in this assembly from 
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("4645e464-75c9-4d82-a87e-01c7093847c9")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version 
//      Build Number
//      Revision
//
// You can specify all the values or you can default the Build and Revision Numbers 
// by using the '*' as shown below:
// [assembly: AssemblyVersion("1.0.*")]
[assembly: AssemblyVersion("1.2.4.0")]
[assembly: AssemblyFileVersion("1.2.4.0")]

/**********************************************************************************************
 * 1.1.9.0
 * Allocate an identify cableid for each single connection cable.
 * Server can asend to specified cableid directly.
 * 1.2.0.0
 * Improve the performance for cable id feature.
 * 1.2.1.0
 * Fix a problem of references for VS2010 that will cause compile error. 
 * 1.2.3.0
 * Add a Connected Event for singleConnectionCable
 * 1.2.4.0
 * Add CableId to DisconnectEventArgs

************************************************************************************************/