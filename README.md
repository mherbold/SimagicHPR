# SimagicHPR

A .NET library for controlling haptic pedal rumble (HPR) on **Simagic** sim racing pedals. It communicates with the pedal controller over a raw HID device stream to send vibration commands to the clutch, brake, and throttle pedals.

## Supported Devices

| Model | VID    | PID    |
|-------|--------|--------|
| P500  | 0x3670 | 0x0903 |
| P700  | 0x3670 | 0x0905 |
| P1000 | 0x0483 | 0x0525 |
| P2000 | 0x3670 | 0x0902 |

## Requirements

- Windows (uses `hid.dll`, `user32.dll`, `kernel32.dll`)
- .NET 7, .NET 8, or .NET 9

## Installation

Reference the project or build it and add the resulting assembly to your project.

## Usage

```csharp
using Simagic;

var hpr = new HPR();

// Connect to the pedals controller
// Pass an optional callback to log discovered HID devices
HPR.PedalsDevice device = hpr.Initialize(enabled: true, onDeviceFound: msg => Console.WriteLine(msg));

if (device != HPR.PedalsDevice.None)
{
    Console.WriteLine($"Connected to: {device}");

    // Vibrate the brake pedal: frequency 0–50, amplitude 0–100
    hpr.VibratePedal(HPR.Channel.Brake, HPR.State.On, frequency: 25f, amplitude: 80f);

    // Stop vibration
    hpr.VibratePedal(HPR.Channel.Brake, HPR.State.Off, frequency: 0f, amplitude: 0f);
}

// Disconnect when done — Uninitialize() or Dispose() (supports 'using')
hpr.Uninitialize();

// Alternatively, use a 'using' block for automatic cleanup
using ( var hpr2 = new HPR() )
{
    hpr2.Initialize( enabled: true );
    hpr2.VibratePedal( HPR.Channel.Throttle, HPR.State.On, frequency: 10f, amplitude: 50f );
} // Dispose() calls Uninitialize() automatically
```

## API

### `HPR.Initialize(bool enabled, Action<string>? onDeviceFound = null)`

Scans the system for a supported Simagic pedal controller and opens a raw HID stream to it.

- `enabled` — when `false`, the device is detected but the HID stream is not opened (no vibration commands will be sent).
- `onDeviceFound` — optional callback invoked for every HID game-controller device found during the scan, useful for diagnostics.

Returns the detected `PedalsDevice` enum value (`None` if no supported device was found).

### `HPR.Uninitialize()`

Stops all vibration and closes the HID device stream. Safe to call even if `Initialize` was never called.

### `HPR.Dispose()`

Implements `IDisposable`. Calls `Uninitialize()` automatically, enabling the `using` pattern for deterministic cleanup.

### `HPR.VibratePedal(Channel channel, State state, float frequency, float amplitude)`

Sends a vibration command to a single pedal.

| Parameter   | Type      | Range  | Description                         |
|-------------|-----------|--------|-------------------------------------|
| `channel`   | `Channel` | —      | `Clutch`, `Brake`, or `Throttle`    |
| `state`     | `State`   | —      | `On` or `Off`                       |
| `frequency` | `float`   | 0 – 50 | Vibration frequency                 |
| `amplitude` | `float`   | 0 – 100| Vibration strength                  |

> **Note:** If either `frequency` or `amplitude` is zero, both are forced to zero and `state` is set to `Off` automatically to prevent pedal buzzing.
>
> **Performance:** Consecutive calls with identical values are deduplicated — no HID feature report is sent unless at least one value has changed. This makes it safe to call `VibratePedal` every frame from a game loop.

### Enums

```csharp
enum PedalsDevice { None, P500, P700, P1000, P2000 }
enum Channel      { Clutch = 0, Brake = 1, Throttle = 2 }
enum State        { Off = 0, On = 1 }
```

## License

See repository for license details.
