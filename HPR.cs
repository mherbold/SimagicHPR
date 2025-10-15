﻿using System.Runtime.InteropServices;

using Microsoft.Win32.SafeHandles;

namespace Simagic
{
	public class HPR
	{
		// enums

		public enum PedalsDevice
		{
			None,
			P500,
			P700,
			P1000,
			P2000
		}

		public enum Channel
		{
			Clutch = 0,
			Brake = 1,
			Throttle = 2
		};

		public enum State
		{
			Off = 0,
			On = 1
		}

		// 64 byte vibrate command packet

		[StructLayout( LayoutKind.Sequential, Pack = 1 )]
		private struct VibrateCommand
		{
			public byte frameHeader = 0xF1;
			public byte commandCode = 0xEC;

			public required byte channel;
			public required byte state;
			public required byte frequency;
			public required byte amplitude;

			public byte padding00 = 0;
			public byte padding01 = 0;
			public byte padding02 = 0;
			public byte padding03 = 0;
			public byte padding04 = 0;
			public byte padding05 = 0;
			public byte padding06 = 0;
			public byte padding07 = 0;
			public byte padding08 = 0;
			public byte padding09 = 0;

			public byte padding10 = 0;
			public byte padding11 = 0;
			public byte padding12 = 0;
			public byte padding13 = 0;
			public byte padding14 = 0;
			public byte padding15 = 0;
			public byte padding16 = 0;
			public byte padding17 = 0;
			public byte padding18 = 0;
			public byte padding19 = 0;

			public byte padding20 = 0;
			public byte padding21 = 0;
			public byte padding22 = 0;
			public byte padding23 = 0;
			public byte padding24 = 0;
			public byte padding25 = 0;
			public byte padding26 = 0;
			public byte padding27 = 0;
			public byte padding28 = 0;
			public byte padding29 = 0;

			public byte padding30 = 0;
			public byte padding31 = 0;
			public byte padding32 = 0;
			public byte padding33 = 0;
			public byte padding34 = 0;
			public byte padding35 = 0;
			public byte padding36 = 0;
			public byte padding37 = 0;
			public byte padding38 = 0;
			public byte padding39 = 0;

			public byte padding40 = 0;
			public byte padding41 = 0;
			public byte padding42 = 0;
			public byte padding43 = 0;
			public byte padding44 = 0;
			public byte padding45 = 0;
			public byte padding46 = 0;
			public byte padding47 = 0;
			public byte padding48 = 0;
			public byte padding49 = 0;

			public byte padding50 = 0;
			public byte padding51 = 0;
			public byte padding52 = 0;
			public byte padding53 = 0;
			public byte padding54 = 0;
			public byte padding55 = 0;
			public byte padding56 = 0;
			public byte padding57 = 0;

			public VibrateCommand()
			{
			}
		};

		// private members

		private bool _initialized = false;
		private PedalsDevice _pedals = PedalsDevice.None;
		private SafeFileHandle? _safeFileHandle = null;
		private int _vibrateCommandStructSize = 0;
		private IntPtr? _vibrateCommandBytes = null;

		// call Initialize() to connect to a Simagic pedals controller

		public PedalsDevice Initialize( bool enabled, Action<string>? onDeviceFound = null )
		{
			// uninitialize in case we have previously initialized

			Uninitialize();

			// get the raw device list from windows and try to find either the P1000 or the P2000 pedal controller and open it as a raw device stream

			var rawInputDeviceList = GetRawInputDeviceList();

			if ( rawInputDeviceList != null )
			{
				nint selectedDeviceHandle = 0;

				foreach ( var device in rawInputDeviceList )
				{
					uint size = (uint) Marshal.SizeOf( typeof( User32WinApi.DeviceInfo ) );

					var deviceInfo = new User32WinApi.DeviceInfo
					{
						Size = Marshal.SizeOf( typeof( User32WinApi.DeviceInfo ) )
					};

					if ( User32WinApi.GetRawInputDeviceInfo( device.hDevice, User32WinApi.RIDI_DEVICEINFO, ref deviceInfo, ref size ) > 0 )
					{
						var isHid = deviceInfo.Type == (int) User32WinApi.RawInputDeviceType.HID;

						if ( isHid )
						{
							var vid = deviceInfo.HIDInfo.VendorID;
							var pid = deviceInfo.HIDInfo.ProductID;

							var deviceName = GetDeviceName( device.hDevice ) ?? string.Empty;
							var friendlyName = !string.IsNullOrEmpty( deviceName ) ? GetFriendlyName( deviceName ) : null;
							var shownName = !string.IsNullOrWhiteSpace( friendlyName ) ? friendlyName : deviceName;

							var isGameCtrl = ( deviceInfo.HIDInfo.UsagePage == 0x01 ) && ( deviceInfo.HIDInfo.Usage == 0x04 || deviceInfo.HIDInfo.Usage == 0x05 );

							onDeviceFound?.Invoke( $"VID=0x{vid:X4}, PID=0x{pid:X4}, Name={shownName}, IsGameCtrl={isGameCtrl}" );

							if ( isGameCtrl )
							{
								if ( ( deviceInfo.HIDInfo.VendorID == 0x3670 ) && ( deviceInfo.HIDInfo.ProductID == 0x0903 ) )
								{
									_pedals = PedalsDevice.P500;

									selectedDeviceHandle = device.hDevice;
								}
								else if ( ( deviceInfo.HIDInfo.VendorID == 0x3670 ) && ( deviceInfo.HIDInfo.ProductID == 0x0905 ) )
								{
									_pedals = PedalsDevice.P700;

									selectedDeviceHandle = device.hDevice;
								}
								else if ( ( deviceInfo.HIDInfo.VendorID == 0x0483 ) && ( deviceInfo.HIDInfo.ProductID == 0x0525 ) )
								{
									_pedals = PedalsDevice.P1000;

									selectedDeviceHandle = device.hDevice;
								}
								else if ( ( deviceInfo.HIDInfo.VendorID == 0x3670 ) && ( deviceInfo.HIDInfo.ProductID == 0x0902 ) )
								{
									_pedals = PedalsDevice.P2000;

									selectedDeviceHandle = device.hDevice;
								}
							}
						}
					}
				}

				if ( ( selectedDeviceHandle != 0 ) && enabled )
				{
					_safeFileHandle = OpenRawDeviceStream( selectedDeviceHandle );

					if ( _safeFileHandle is not null && !_safeFileHandle.IsInvalid )
					{
						_vibrateCommandStructSize = Marshal.SizeOf( typeof( VibrateCommand ) );
						_vibrateCommandBytes = Marshal.AllocHGlobal( _vibrateCommandStructSize );

						_initialized = true;

						VibratePedal( Channel.Clutch, State.Off, 0, 0 );
						VibratePedal( Channel.Brake, State.Off, 0, 0 );
						VibratePedal( Channel.Throttle, State.Off, 0, 0 );
					}
					else
					{
						_safeFileHandle = null;
					}
				}
			}

			// return true if we found the pedals

			return _pedals;
		}

		// call this Uninitialize() to close down this system

		public void Uninitialize()
		{
			VibratePedal( Channel.Clutch, State.Off, 0, 0 );
			VibratePedal( Channel.Brake, State.Off, 0, 0 );
			VibratePedal( Channel.Throttle, State.Off, 0, 0 );

			_initialized = false;

			if ( _safeFileHandle != null )
			{
				_safeFileHandle.Dispose();

				_safeFileHandle = null;
			}

			if ( _vibrateCommandBytes != null )
			{
				Marshal.FreeHGlobal( (IntPtr) _vibrateCommandBytes );

				_vibrateCommandBytes = null;
			}
		}

		// call VibratePedal() to vibrate one of the three pedals, frequency = 0-50, amplitude = 0-100

		public void VibratePedal( Channel channel, State state, float frequency, float amplitude )
		{
			if ( _initialized )
			{
				if ( ( _safeFileHandle != null ) && ( _vibrateCommandBytes != null ) )
				{
					var intFrequency = (int) Math.Clamp( frequency, 0f, 50f );
					var intAmplitude = (int) Math.Clamp( amplitude, 0f, 100f );

					var vibrateCommand = new VibrateCommand
					{
						channel = (byte) channel,
						state = (byte) state,
						frequency = (byte) intFrequency,
						amplitude = (byte) intAmplitude
					};

					Marshal.StructureToPtr( vibrateCommand, (IntPtr) _vibrateCommandBytes, false );

					HidWinApi.HidD_SetFeature( _safeFileHandle, (IntPtr) _vibrateCommandBytes, _vibrateCommandStructSize );
				}
			}
		}

		// private functions for internal use

		private static string? GetFriendlyName( string deviceName )
		{
			using var safeFileHandle = Kernel32WinApi.CreateFile( deviceName, Kernel32WinApi.GENERIC_READ | Kernel32WinApi.GENERIC_WRITE, Kernel32WinApi.FILE_SHARE_READ | Kernel32WinApi.FILE_SHARE_WRITE, IntPtr.Zero, Kernel32WinApi.OPEN_EXISTING, Kernel32WinApi.FILE_ATTRIBUTE_NORMAL, IntPtr.Zero );

			if ( safeFileHandle is null || safeFileHandle.IsInvalid )
			{
				return null;
			}

			static string? ReadHidString( Func<IntPtr, IntPtr, int, bool> fn, IntPtr h )
			{
				const int byteLen = 512;

				var ptr = Marshal.AllocHGlobal( byteLen );

				try
				{
					if ( fn( h, ptr, byteLen ) )
					{
						return Marshal.PtrToStringUni( ptr );
					}
				}
				finally
				{
					Marshal.FreeHGlobal( ptr );
				}

				return null;
			}

			var h = safeFileHandle.DangerousGetHandle();
			var manufacturer = ReadHidString( HidWinApi.HidD_GetManufacturerString, h );
			var product = ReadHidString( HidWinApi.HidD_GetProductString, h );

			if ( !string.IsNullOrWhiteSpace( manufacturer ) && !string.IsNullOrWhiteSpace( product ) )
			{
				return $"{manufacturer} {product}".Trim();
			}

			return product ?? manufacturer;
		}

		private static string? GetDeviceName( nint hDevice )
		{
			uint charCount = 0;

			var rc = User32WinApi.GetRawInputDeviceInfo( hDevice, User32WinApi.RIDI_DEVICENAME, IntPtr.Zero, ref charCount );

			if ( rc != 0 || charCount == 0 )
			{
				return null;
			}

			var byteCount = checked((int) charCount * sizeof( char ));
			var buffer = Marshal.AllocHGlobal( byteCount );

			try
			{
				var rc2 = User32WinApi.GetRawInputDeviceInfo( hDevice, User32WinApi.RIDI_DEVICENAME, buffer, ref charCount );

				if ( rc2 <= 0 ) return null;

				return Marshal.PtrToStringUni( buffer );
			}
			finally
			{
				Marshal.FreeHGlobal( buffer );
			}
		}

		private static User32WinApi.RAWINPUTDEVICELIST[]? GetRawInputDeviceList()
		{
			uint deviceCount = 0;

			uint rawInputDeviceListSize = (uint) Marshal.SizeOf( typeof( User32WinApi.RAWINPUTDEVICELIST ) );

			uint result = User32WinApi.GetRawInputDeviceList( null, ref deviceCount, rawInputDeviceListSize );

			if ( ( result != 0 ) || ( deviceCount == 0 ) )
			{
				return null;
			}

			var rawInputDeviceList = new User32WinApi.RAWINPUTDEVICELIST[ deviceCount ];

			result = User32WinApi.GetRawInputDeviceList( rawInputDeviceList, ref deviceCount, rawInputDeviceListSize );

			if ( result == 0 )
			{
				return null;
			}

			return rawInputDeviceList;
		}

		private static SafeFileHandle? OpenRawDeviceStream( IntPtr hDevice )
		{
			var deviceName = GetDeviceName( hDevice );

			if ( string.IsNullOrEmpty( deviceName ) )
			{
				return null;
			}

			return Kernel32WinApi.CreateFile( deviceName, Kernel32WinApi.GENERIC_READ | Kernel32WinApi.GENERIC_WRITE, Kernel32WinApi.FILE_SHARE_READ | Kernel32WinApi.FILE_SHARE_WRITE, IntPtr.Zero, Kernel32WinApi.OPEN_EXISTING, Kernel32WinApi.FILE_ATTRIBUTE_NORMAL, IntPtr.Zero );
		}
	}
}
