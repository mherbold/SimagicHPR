
using System.Runtime.InteropServices;

namespace Simagic
{
	public static class User32WinApi
	{
		[StructLayout( LayoutKind.Explicit )]
		public struct DeviceInfo
		{
			[FieldOffset( 0 )]
			public int Size;
			[FieldOffset( 4 )]
			public int Type;

			[FieldOffset( 8 )]
			public DeviceInfoMouse MouseInfo;
			[FieldOffset( 8 )]
			public DeviceInfoKeyboard KeyboardInfo;
			[FieldOffset( 8 )]
			public DeviceInfoHID HIDInfo;
		}

		public struct DeviceInfoMouse
		{
			public uint ID;
			public uint NumberOfButtons;
			public uint SampleRate;
		}

		public struct DeviceInfoKeyboard
		{
			public uint Type;
			public uint SubType;
			public uint KeyboardMode;
			public uint NumberOfFunctionKeys;
			public uint NumberOfIndicators;
			public uint NumberOfKeysTotal;
		}

		public struct DeviceInfoHID
		{
			public uint VendorID;
			public uint ProductID;
			public uint VersionNumber;
			public ushort UsagePage;
			public ushort Usage;
		}

		public enum RawInputDeviceType : uint
		{
			MOUSE = 0,
			KEYBOARD = 1,
			HID = 2
		}

		[StructLayout( LayoutKind.Sequential )]
		public struct RAWINPUTDEVICELIST
		{
			public IntPtr hDevice;
			public RawInputDeviceType Type;
		}

		public const uint RIDI_PREPARSEDDATA = 0x20000005;
		public const uint RIDI_DEVICENAME = 0x20000007;
		public const uint RIDI_DEVICEINFO = 0x2000000b;

		[DllImport( "user32.dll", EntryPoint = "GetRawInputDeviceInfoA" )]
		public static extern uint GetRawInputDeviceInfo( IntPtr deviceHandle, uint command, ref DeviceInfo data, ref uint dataSize );

		[DllImport( "user32.dll", EntryPoint = "GetRawInputDeviceInfoA" )]
		public static extern uint GetRawInputDeviceInfo( IntPtr deviceHandle, uint command, IntPtr data, ref uint dataSize );

		[DllImport( "user32.dll", SetLastError = true, CharSet = CharSet.Auto )]
		public static extern uint GetRawInputDeviceList( [In, Out] RAWINPUTDEVICELIST[]? RawInputDeviceList, ref uint NumDevices, uint Size );
	}
}
