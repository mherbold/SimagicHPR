
using System.Runtime.InteropServices;

using Microsoft.Win32.SafeHandles;

namespace Simagic
{
	class Kernel32WinApi
	{
		public const uint GENERIC_READ = 0x80000000;
		public const uint GENERIC_WRITE = 0x40000000;

		public const int FILE_SHARE_READ = 1;
		public const int FILE_SHARE_WRITE = 2;

		public const int OPEN_EXISTING = 3;

		[DllImport( "kernel32.dll", EntryPoint = "CreateFileA", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true )]
		public static extern SafeFileHandle CreateFile( string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr SecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile );
	}
}
