
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

		public const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;
		public const uint FILE_FLAG_OVERLAPPED = 0x40000000;

		[DllImport( "kernel32.dll", EntryPoint = "CreateFileW", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true )]
		internal static extern SafeFileHandle CreateFile( string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile );
	}
}
