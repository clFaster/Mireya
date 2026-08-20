# Android debugging

Use this guide for Android-specific build, deployment, and diagnostic work. Run the
commands from the repository root in PowerShell.

The project targets `net10.0-android`; its application id is
`dev.moritzreis.mireya`. Android emulators reach a host API at
`http://10.0.2.2:5000`. A physical device needs an address it can reach over the
network (or a suitable `adb reverse` configuration).

## Select a device and runtime

List connected devices and inspect the intended target:

```powershell
adb devices -l
$device = '<serial from adb devices>'
adb -s $device get-state
$abi = (adb -s $device shell getprop ro.product.cpu.abi).Trim()
```

Choose the matching .NET runtime identifier:

| Android ABI | Runtime identifier |
| --- | --- |
| `armeabi-v7a` | `android-arm` |
| `arm64-v8a` | `android-arm64` |
| `x86` | `android-x86` |
| `x86_64` | `android-x64` |

Set it for the remaining commands, for example:

```powershell
$androidRid = 'android-x64'
```

Do not assume a device serial, ABI, or APK output path. These vary between machines
and emulators.

## Build and deploy

Restore the Android workload when needed:

```powershell
dotnet workload restore src/Mireya.Client.Android/Mireya.Client.Android.csproj
```

For normal iteration, let MSBuild deploy and launch the Debug build:

```powershell
dotnet build src/Mireya.Client.Android/Mireya.Client.Android.csproj `
    -c Debug -r $androidRid -t:Run
```

For manual installation, lifecycle testing, or performance work, build an APK with
embedded assemblies. Use `Release` for final performance and memory conclusions:

```powershell
dotnet build src/Mireya.Client.Android/Mireya.Client.Android.csproj `
    -c Release `
    -r $androidRid `
    -p:EmbedAssembliesIntoApk=true `
    -p:AndroidFastDeploymentType=None

$apk = Get-ChildItem src/Mireya.Client.Android/bin/Release -Recurse -Filter '*-Signed.apk' |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

adb -s $device install -r $apk.FullName
adb -s $device shell monkey `
    -p dev.moritzreis.mireya `
    -c android.intent.category.LAUNCHER 1
```

Use `adb install -r` to preserve pairing, settings, and cached assets. Do not uninstall
the app or clear its data unless losing that state is an intentional part of the test.

`/p:SkipNSwag=true` is safe for repeated Android-only builds only when the API
contract and generated client are unchanged.

## Capture diagnostics

Clear logcat immediately before a planned reproduction, never after an observed
failure. Capture the complete log before filtering it:

```powershell
adb -s $device logcat -c
# Reproduce the issue.
New-Item -ItemType Directory -Force artifacts/android-debug | Out-Null
adb -s $device logcat -d -v threadtime > artifacts/android-debug/logcat.txt

Get-Content artifacts/android-debug/logcat.txt |
    Select-String -Pattern 'FATAL EXCEPTION|AndroidRuntime|SIGABRT|SIGSEGV|OutOfMemory|lowmemory|has died|ANR'
```

Useful snapshots while the app is running:

```powershell
$pidNow = (adb -s $device shell pidof dev.moritzreis.mireya).Trim()
adb -s $device logcat --pid=$pidNow -v threadtime
adb -s $device exec-out screencap -p > artifacts/android-debug/screen.png
adb -s $device shell dumpsys activity activities > artifacts/android-debug/activity.txt
adb -s $device shell dumpsys meminfo dev.moritzreis.mireya > artifacts/android-debug/meminfo.txt
```

A changing PID means Android restarted or killed the process. For suspected memory
growth, sample `dumpsys meminfo` at fixed intervals during an embedded Release soak;
keep the content loop and device state constant.

Review captured files for backend URLs, pairing data, tokens, and other sensitive
values before sharing them. Do not commit investigation artifacts.

## Diagnose by symptom

| Symptom | Start with |
| --- | --- |
| Cannot connect or register | API logs, `/api/info`, emulator host address, network state |
| Immediate exit | Full logcat, ABI and package configuration |
| Black or frozen screen | Screenshot, activity state, Avalonia logs, UI-thread work |
| Image issue | Shared playback code, decoded dimensions, bitmap disposal, cache |
| Video issue | Media3/ExoPlayer logs, codec support, URI accessibility |
| Website issue | WebView logs, network/TLS behavior, native-control lifecycle |
| Background/foreground restart | PID tracking, activity lifecycle, process-death evidence |
| ANR | Full logcat, activity dump, synchronous main-thread work |
| Memory growth | Stable PID, timed `dumpsys meminfo` samples, Release comparison |

Desktop and Android use different native renderers: desktop uses WebView2 and LibVLC;
Android uses Android WebView and Media3/ExoPlayer. Keep platform-specific fixes in the
Android project unless the verified cause is shared behavior.

## Verify a fix

Repeat the original reproduction on the same device and build type, inspect the full
log, and exercise a nearby asset or lifecycle transition. Run the shared client tests
when shared behavior changed:

```powershell
dotnet test src/Mireya.Client.Core.Tests/Mireya.Client.Core.Tests.csproj -c Release
```

Confirm release-critical behavior on a representative physical ARM64 Android TV
device. Report the device, Android version, ABI, build type, reproduction, evidence,
checks completed, and any verification still outstanding.
