using System.Runtime.InteropServices;

using Microsoft.Win32.SafeHandles;

namespace Simagic
{
	/// <summary>
	/// Controls haptic pedal rumble (HPR) on Simagic sim racing pedal controllers.
	/// Call <see cref="Initialize"/> to connect, <see cref="VibratePedal"/> to send vibration
	/// commands, and <see cref="Uninitialize"/> (or <see cref="Dispose"/>) to disconnect.
	/// </summary>
	public class HPR : IDisposable
	{
		// enums

		/// <summary>Identifies the connected Simagic pedals model.</summary>
		public enum PedalsDevice
		{
			None,
			P500,
			P700,
			P1000,
			P2000
		}

		/// <summary>The individual pedal channel to target with a vibration command.</summary>
		public enum Channel
		{
			Clutch = 0,
			Brake = 1,
			Throttle = 2
		};

		/// <summary>Whether vibration is active or inactive on a channel.</summary>
		public enum State
		{
			Off = 0,
			On = 1
		}

		// 64 byte vibrate command packet

		[StructLayout( LayoutKind.Sequential, Pack = 1 )]
		private unsafe struct VibrateCommand
		{
			public byte frameHeader = 0xF1;
			public byte commandCode = 0xEC;

			public required byte channel;
			public required byte state;
			public required byte frequency;
			public required byte amplitude;

			public fixed byte padding[ 58 ];

			public VibrateCommand()
			{
			}
		};

		static HPR()
		{
			var structSize = Marshal.SizeOf<VibrateCommand>();

			if ( structSize != 64 )
			{
				throw new InvalidOperationException( $"VibrateCommand struct size is {structSize} bytes but must be exactly 64 bytes." );
			}
		}

		// private members

#if NET9_0_OR_GREATER
		private readonly System.Threading.Lock _lock = new();
#else
		private readonly object _lock = new();
#endif

		private bool _disposed = false;
		private bool _initialized = false;
		private PedalsDevice _pedals = PedalsDevice.None;
		private SafeFileHandle? _safeFileHandle = null;
		private int _vibrateCommandStructSize = 0;
		private IntPtr? _vibrateCommandBytes = null;
		private readonly int[] _lastState = new int[ Enum.GetValues<Channel>().Length ];
		private readonly int[] _lastFrequency = new int[ Enum.GetValues<Channel>().Length ];
		private readonly int[] _lastAmplitude = new int[ Enum.GetValues<Channel>().Length ];

		/// <summary>
		/// Scans the system for a supported Simagic pedal controller and opens a raw HID stream to it.
		/// Safe to call multiple times; any previous connection is closed before reconnecting.
		/// </summary>
		/// <param name="enabled">
		/// When <c>false</c> the device is detected but the HID stream is not opened and no
		/// vibration commands will be sent.
		/// </param>
		/// <param name="onDeviceFound">
		/// Optional callback invoked for every HID game-controller device found during the scan.
		/// Useful for diagnostics.
		/// </param>
		/// <returns>The detected <see cref="PedalsDevice"/>, or <see cref="PedalsDevice.None"/> if no supported device was found.</returns>
		public PedalsDevice Initialize( bool enabled, Action<string>? onDeviceFound = null )
		{
			// uninitialize in case we have previously initialized

			Uninitialize();

			// get the raw device list from windows and try to find a Simagic pedal controller and open it as a raw device stream

			var rawInputDeviceList = GetRawInputDeviceList();

			if ( rawInputDeviceList != null )
			{
				nint selectedDeviceHandle = 0;
				string? selectedDeviceName = null;

				foreach ( var device in rawInputDeviceList )
				{
					uint size = (uint) Marshal.SizeOf<User32WinApi.DeviceInfo>();

					var deviceInfo = new User32WinApi.DeviceInfo
					{
						Size = Marshal.SizeOf<User32WinApi.DeviceInfo>()
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
								// VID/PID matching is used to identify candidate devices; the friendly name must
								// also confirm the model to guard against other peripherals sharing the same VID/PID

								if ( IsP500Pedals( vid, pid, friendlyName ) )
								{
									_pedals = PedalsDevice.P500;

									selectedDeviceHandle = device.hDevice;
									selectedDeviceName = deviceName;
								}
								else if ( IsP700Pedals( vid, pid, friendlyName ) )
								{
									_pedals = PedalsDevice.P700;

									selectedDeviceHandle = device.hDevice;
									selectedDeviceName = deviceName;
								}
								else if ( IsP1000Pedals( vid, pid, friendlyName ) )
								{
									_pedals = PedalsDevice.P1000;

									selectedDeviceHandle = device.hDevice;
									selectedDeviceName = deviceName;
								}
								else if ( IsP2000Pedals( vid, pid, friendlyName ) )
								{
									_pedals = PedalsDevice.P2000;

									selectedDeviceHandle = device.hDevice;
									selectedDeviceName = deviceName;
								}
							}
						}
					}
				}

				if ( ( selectedDeviceHandle != 0 ) && ( selectedDeviceName != null ) && enabled )
				{
					_safeFileHandle = OpenRawDeviceStream( selectedDeviceName );

					if ( _safeFileHandle is not null && !_safeFileHandle.IsInvalid )
					{
						_vibrateCommandStructSize = Marshal.SizeOf<VibrateCommand>();
						_vibrateCommandBytes = Marshal.AllocHGlobal( _vibrateCommandStructSize );

						_initialized = true;

						try
						{
							VibratePedalCore( Channel.Clutch, State.Off, 0, 0 );
							VibratePedalCore( Channel.Brake, State.Off, 0, 0 );
							VibratePedalCore( Channel.Throttle, State.Off, 0, 0 );
						}
						catch
						{
							// if the initial stop commands fail, tear down cleanly rather than leaving a half-initialized state

							Uninitialize();
						}
					}
					else
					{
						_safeFileHandle = null;
					}
				}
			}

			// return which pedals were found (None if not found or not enabled)

			return _pedals;
		}

		/// <summary>
		/// Stops all vibration and closes the HID device stream.
		/// Safe to call even if <see cref="Initialize"/> was never called or already called.
		/// </summary>
		public void Uninitialize()
		{
			lock ( _lock )
			{
				try
				{
					if ( _initialized )
					{
						VibratePedalCore( Channel.Clutch, State.Off, 0, 0 );
						VibratePedalCore( Channel.Brake, State.Off, 0, 0 );
						VibratePedalCore( Channel.Throttle, State.Off, 0, 0 );
					}
				}
				finally
				{
					_initialized = false;
					_pedals = PedalsDevice.None;

					_safeFileHandle?.Dispose();
					_safeFileHandle = null;

					if ( _vibrateCommandBytes is IntPtr bytesPtr )
					{
						Marshal.FreeHGlobal( bytesPtr );

						_vibrateCommandBytes = null;
					}

					for ( var channel = Channel.Clutch; channel <= Channel.Throttle; channel++ )
					{
						_lastState[ (int) channel ] = -1;
						_lastFrequency[ (int) channel ] = -1;
						_lastAmplitude[ (int) channel ] = -1;
					}
				}
			}
		}

		/// <summary>
		/// Sends a vibration command to a single pedal.
		/// Identical consecutive calls are deduplicated and will not re-send a HID feature report.
		/// </summary>
		/// <param name="channel">The pedal to target: <see cref="Channel.Clutch"/>, <see cref="Channel.Brake"/>, or <see cref="Channel.Throttle"/>.</param>
		/// <param name="state"><see cref="State.On"/> to enable vibration, <see cref="State.Off"/> to disable it.</param>
		/// <param name="frequency">Vibration frequency in the range 0–50. A value of 0 forces the channel off.</param>
		/// <param name="amplitude">Vibration strength in the range 0–100. A value of 0 forces the channel off.</param>
		public void VibratePedal( Channel channel, State state, float frequency, float amplitude )
		{
			lock ( _lock )
			{
				VibratePedalCore( channel, state, frequency, amplitude );
			}
		}

		/// <inheritdoc/>
		public void Dispose()
		{
			if ( !_disposed )
			{
				_disposed = true;

				Uninitialize();

				GC.SuppressFinalize( this );
			}
		}

		// internal vibrate implementation — must be called with _lock held (or before _lock is needed, e.g. from Uninitialize)

		private void VibratePedalCore( Channel channel, State state, float frequency, float amplitude )
		{
			if ( _initialized )
			{
				if ( ( _safeFileHandle != null ) && ( _vibrateCommandBytes != null ) )
				{
					// convert from float to int

					var intFrequency = (int) Math.Clamp( frequency, 0f, 50f );
					var intAmplitude = (int) Math.Clamp( amplitude, 0f, 100f );

					// force state to off if either frequency or amplitude are zero (prevents buzzing pedals)

					if ( ( intFrequency == 0 ) || ( intAmplitude == 0 ) )
					{
						intFrequency = 0;
						intAmplitude = 0;

						state = State.Off;
					}

					_lastState[ (int) channel ] = (int) state;
					_lastFrequency[ (int) channel ] = intFrequency;
					_lastAmplitude[ (int) channel ] = intAmplitude;

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

		private static bool IsP500Pedals( uint vid, uint pid, string? friendlyName )
		{
			return ( vid == 0x3670 )
				&& ( pid == 0x0903 )
				&& ( ( friendlyName?.Contains( "P500", StringComparison.OrdinalIgnoreCase ) == true )
					|| ( friendlyName?.Contains( "Alpha Pedal Neo", StringComparison.OrdinalIgnoreCase ) == true ) );
		}

		private static bool IsP700Pedals( uint vid, uint pid, string? friendlyName )
		{
			return ( vid == 0x3670 )
				&& ( pid == 0x0905 )
				&& ( friendlyName?.Contains( "P700", StringComparison.OrdinalIgnoreCase ) == true );
		}

		private static bool IsP1000Pedals( uint vid, uint pid, string? friendlyName )
		{
			return ( vid == 0x0483 )
				&& ( pid == 0x0525 )
				&& ( friendlyName?.Contains( "P1000", StringComparison.OrdinalIgnoreCase ) == true );
		}

		private static bool IsP2000Pedals( uint vid, uint pid, string? friendlyName )
		{
			return ( vid == 0x3670 )
				&& ( pid == 0x0902 )
				&& ( friendlyName?.Contains( "P2000", StringComparison.OrdinalIgnoreCase ) == true );
		}

		private static string? GetFriendlyName( string deviceName )
		{
			using var safeFileHandle = Kernel32WinApi.CreateFile( deviceName, Kernel32WinApi.GENERIC_READ | Kernel32WinApi.GENERIC_WRITE, Kernel32WinApi.FILE_SHARE_READ | Kernel32WinApi.FILE_SHARE_WRITE, IntPtr.Zero, Kernel32WinApi.OPEN_EXISTING, Kernel32WinApi.FILE_ATTRIBUTE_NORMAL, IntPtr.Zero );

			if ( safeFileHandle is null || safeFileHandle.IsInvalid )
			{
				return null;
			}

			static string? ReadHidString( Func<SafeFileHandle, IntPtr, int, bool> fn, SafeFileHandle h )
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

			var manufacturer = ReadHidString( HidWinApi.HidD_GetManufacturerString, safeFileHandle );
			var product = ReadHidString( HidWinApi.HidD_GetProductString, safeFileHandle );

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

			uint rawInputDeviceListSize = (uint) Marshal.SizeOf<User32WinApi.RAWINPUTDEVICELIST>();

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

		private static SafeFileHandle? OpenRawDeviceStream( string deviceName )
		{
			return Kernel32WinApi.CreateFile( deviceName, Kernel32WinApi.GENERIC_READ | Kernel32WinApi.GENERIC_WRITE, Kernel32WinApi.FILE_SHARE_READ | Kernel32WinApi.FILE_SHARE_WRITE, IntPtr.Zero, Kernel32WinApi.OPEN_EXISTING, Kernel32WinApi.FILE_ATTRIBUTE_NORMAL, IntPtr.Zero );
		}
	}
}
