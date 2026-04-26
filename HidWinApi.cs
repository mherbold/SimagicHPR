
using System.Runtime.InteropServices;

using Microsoft.Win32.SafeHandles;

namespace Simagic
{
	internal static partial class HidWinApi
	{
		[LibraryImport( "hid.dll", SetLastError = true )]
		[return: MarshalAs( UnmanagedType.Bool )]
		internal static partial bool HidD_SetFeature( SafeFileHandle HidDeviceObject, IntPtr lpReportBuffer, int ReportBufferLength );

		[LibraryImport( "hid.dll", SetLastError = true )]
		[return: MarshalAs( UnmanagedType.Bool )]
		internal static partial bool HidD_GetProductString( SafeFileHandle hDevice, IntPtr buffer, int bufferLength );

		[LibraryImport( "hid.dll", SetLastError = true )]
		[return: MarshalAs( UnmanagedType.Bool )]
		internal static partial bool HidD_GetManufacturerString( SafeFileHandle hDevice, IntPtr buffer, int bufferLength );
	}
}
