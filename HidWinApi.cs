
using System.Runtime.InteropServices;

using Microsoft.Win32.SafeHandles;

namespace Simagic
{
	class HidWinApi
	{
		[DllImport( "hid.dll", SetLastError = true )]
		internal static extern Boolean HidD_SetFeature( SafeFileHandle HidDeviceObject, IntPtr lpReportBuffer, Int32 ReportBufferLength );

		[DllImport( "hid.dll", SetLastError = true, ExactSpelling = true )]
		internal static extern bool HidD_GetProductString( IntPtr hDevice, IntPtr buffer, int bufferLength );

		[DllImport( "hid.dll", SetLastError = true, ExactSpelling = true )]
		internal static extern bool HidD_GetManufacturerString( IntPtr hDevice, IntPtr buffer, int bufferLength );
	}
}
