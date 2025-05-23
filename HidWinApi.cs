
using System.Runtime.InteropServices;

using Microsoft.Win32.SafeHandles;

namespace Simagic
{
	class HidWinApi
	{
		[DllImport( "hid.dll", SetLastError = true )]
		internal static extern Boolean HidD_SetFeature( SafeFileHandle HidDeviceObject, IntPtr lpReportBuffer, Int32 ReportBufferLength );
	}
}
