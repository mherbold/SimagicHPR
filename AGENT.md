# AGENT.md — SimagicHPR

Guidelines for AI coding agents working on this repository.

## Project Overview

SimagicHPR is a **Windows-only .NET class library** (`Simagic` namespace) that sends haptic vibration commands to Simagic sim racing pedal controllers (P500, P700, P1000, P2000) via raw HID feature reports.

## Repository Layout

```
SimagicHPR.csproj   — SDK-style project; targets net7.0-windows, net8.0-windows, net9.0-windows
HPR.cs              — Public API: Initialize / Uninitialize / VibratePedal
HidWinApi.cs        — P/Invoke wrappers for hid.dll (HidD_SetFeature, product/manufacturer strings)
User32WinApi.cs     — P/Invoke wrappers for user32.dll (raw input device enumeration)
Kernel32WinApi.cs   — P/Invoke wrappers for kernel32.dll (CreateFile for opening HID streams)
```

## Architecture

- **Device discovery** — `GetRawInputDeviceList` enumerates all raw input HID devices. A candidate must satisfy **both** a VID/PID match **and** a friendly-name substring match (e.g. `"P2000"`) to be accepted, guarding against other peripherals that share the same VID/PID. The device name resolved during the scan is reused when opening the stream, avoiding a redundant kernel round-trip.
- **Device communication** — the controller is opened with `CreateFile` and vibration commands are delivered via `HidD_SetFeature`.
- **Command packet** — `VibrateCommand` is a 64-byte `unsafe` sequential struct (`Pack = 1`) with a fixed header (`0xF1 0xEC`), channel, state, frequency, amplitude, and a `fixed byte padding[58]` buffer. A static constructor asserts `Marshal.SizeOf<VibrateCommand>() == 64` at startup and throws `InvalidOperationException` if the size is ever wrong.
- **Deduplication** — `VibratePedal` tracks the last sent state/frequency/amplitude per channel and skips the HID write when nothing has changed, preventing USB bus flooding from high-frequency game loops.
- **Thread safety** — a `private readonly` lock object serialises all calls to `VibratePedal` and `Uninitialize`, preventing use-after-free on the native handle. On .NET 9+ the lock uses `System.Threading.Lock` (lower overhead); on .NET 7/8 it falls back to a plain `object`, selected via `#if NET9_0_OR_GREATER`.
- **Lifetime** — `HPR` implements `IDisposable`; `Dispose()` calls `Uninitialize()` and `GC.SuppressFinalize(this)`, enabling the `using` pattern.
- **Interop** — all Win32 calls use `LibraryImportAttribute` (source-generated, AOT/trim-safe) instead of `DllImportAttribute`. The project enables `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` which is required by the generated stubs.
- **No external NuGet dependencies** — all HID/Win32 interop is implemented directly via source-generated P/Invoke.

## Coding Conventions

- Tabs for indentation.
- Allman brace style (opening brace on its own line).
- Nullable reference types are enabled (`<Nullable>enable</Nullable>` via project property).
- `required` keyword used on mandatory struct fields.
- Private members prefixed with `_`.
- Keep P/Invoke declarations in the dedicated `*WinApi.cs` files; do not add Win32 calls to `HPR.cs`.
- All `*WinApi` classes are `internal static partial` — `internal` to keep Win32 types off the public API surface, `static partial` because `LibraryImportAttribute` requires it.
- Use `LibraryImportAttribute` (not `DllImportAttribute`) for all new P/Invoke declarations. Always specify `EntryPoint` explicitly for `W`-suffixed Unicode functions. Use `StringMarshalling = StringMarshalling.Utf16` in place of `CharSet = CharSet.Unicode`. Use `[return: MarshalAs(UnmanagedType.Bool)]` for `bool`-returning imports.
- Pass `SafeFileHandle` directly to P/Invoke rather than extracting a raw `IntPtr` via `DangerousGetHandle`.

## Key Constraints

- **Windows only** — the library uses `hid.dll`, `user32.dll`, and `kernel32.dll`. Do not add cross-platform code paths.
- **No GUI, no app entry point** — this is a pure library; do not add `Main` or UI code.
- **Packet format is fixed** — the 64-byte `VibrateCommand` struct layout must not be changed without verifying compatibility with the physical hardware. The static constructor enforces this at runtime.
- **Supported VID/PID pairs** — only the four device identifiers listed in `HPR.cs` are known-good. Adding a new device requires confirmation of its VID/PID from hardware documentation or testing.
- **`AllowUnsafeBlocks` is required** — the `LibraryImport` source generator emits `unsafe` stubs. Do not remove `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` from the project file.
- **`[In, Out]` attributes are not supported** by the `LibraryImport` source generator (SYSLIB1051). Do not add them to array parameters on `LibraryImport` declarations.

## Common Tasks

### Add support for a new pedals model
1. Add a new value to `HPR.PedalsDevice`.
2. Add a matching `else if` block in `HPR.Initialize` with the correct VID/PID **and** a `friendlyName?.Contains("<ModelName>", StringComparison.OrdinalIgnoreCase) == true` name check.
3. Confirm the exact friendly name string by running `Initialize` with the `onDeviceFound` callback and inspecting the output.

### Change the vibration command packet
1. Update `VibrateCommand` in `HPR.cs`. The padding is a `fixed byte padding[58]` buffer inside an `unsafe struct` — adjust the size if the payload fields change.
2. Verify `Marshal.SizeOf<VibrateCommand>()` still equals 64 — the static constructor will throw at runtime if it does not.

### Add a new Win32 API call
- Place `LibraryImport` declarations in the appropriate `*WinApi.cs` file (`HidWinApi`, `User32WinApi`, or `Kernel32WinApi`).
- The class must remain `internal static partial`.
- Specify `EntryPoint` explicitly for `W`-suffixed functions; use `StringMarshalling = StringMarshalling.Utf16` for string parameters; add `[return: MarshalAs(UnmanagedType.Bool)]` for `bool` returns.
- Do not use `[In, Out]` on array parameters — they are unsupported by the source generator.

## Build

```powershell
dotnet build SimagicHPR.csproj
```

Builds for all three target frameworks. No test project exists; validate changes manually with physical hardware or by inspecting P/Invoke call results.
