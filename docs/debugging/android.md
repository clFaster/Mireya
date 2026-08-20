# Android and Android TV debugging

This runbook is the repeatable starting point for diagnosing the Mireya Android
client. It covers normal build, deployment, connectivity, logging, crash, lifecycle,
rendering, and performance investigations. Record issue-specific experiments and
measurements separately rather than turning this guide into a postmortem.

For a worked example of reproducing, isolating, and verifying a native-memory issue,
including a five-minute Release soak, see
[Android memory debugging](android-memory.md).

Run commands from the repository root unless a section says otherwise. The examples
use PowerShell.

## Known values

| Item | Value |
| --- | --- |
| Project | `src/Mireya.Client.Android/Mireya.Client.Android.csproj` |
| Target framework | `net10.0-android` |
| Application id | `dev.moritzreis.mireya` |
| Local host API | `http://localhost:5000` |
| API as seen by the Android emulator | `http://10.0.2.2:5000` |

The emulator's `localhost` is the emulator itself. A physical device cannot normally
use `10.0.2.2`; use an API address reachable from that device, or configure an
appropriate `adb reverse` workflow when supported.

## 1. Capture the starting state

Before making changes, record the repository state and available devices:

```powershell
git status --short
adb devices -l
```

Choose the intended serial explicitly when more than one device is connected:

```powershell
$device = 'emulator-5554'
adb -s $device get-state
adb -s $device shell getprop ro.build.version.release
adb -s $device shell getprop ro.product.cpu.abi
adb -s $device shell wm size
adb -s $device shell wm density
```

Do not copy the example serial into automation. Set `$device` from the actual
`adb devices -l` result.

Map Android's ABI to the .NET runtime identifier:

| Device ABI | .NET runtime identifier |
| --- | --- |
| `armeabi-v7a` | `android-arm` |
| `arm64-v8a` | `android-arm64` |
| `x86` | `android-x86` |
| `x86_64` | `android-x64` |

Record the chosen value, for example:

```powershell
$androidRid = 'android-x64'
```

## 2. Start and verify the backend

Start the development API over HTTP in a separate terminal:

```powershell
dotnet run --project src/Mireya.Api/Mireya.Api.csproj --launch-profile http
```

Verify it from the host:

```powershell
Invoke-RestMethod http://localhost:5000/api/info
```

On an emulator, configure Mireya with `http://10.0.2.2:5000`. If registration or
synchronization fails, first distinguish backend reachability from client behavior.
Check the API terminal and verify that the emulator has network access before changing
application code.

## 3. Restore prerequisites

The Android SDK and .NET Android workload must be installed:

```powershell
dotnet tool restore
dotnet workload restore src/Mireya.Client.Android/Mireya.Client.Android.csproj
```

Only install or update workloads and dependencies when the task requires it. Such
changes can affect every later build and may require network access.

## 4. Choose the right build mode

### Iteration build

MSBuild can build, deploy, and launch a Debug build on the selected device:

```powershell
dotnet build src/Mireya.Client.Android/Mireya.Client.Android.csproj `
    -c Debug `
    -r $androidRid `
    -t:Run
```

This is convenient for breakpoints and fast iteration. Fast Deployment can change
runtime and native-memory behavior, so do not use it alone for a final performance or
memory conclusion.

### Standalone Debug APK

Use an embedded APK when manual installation or a more representative package is
needed:

```powershell
$env:AVALONIA_TELEMETRY_OPTOUT = '1'
dotnet build src/Mireya.Client.Android/Mireya.Client.Android.csproj `
    -c Debug `
    -r $androidRid `
    -p:EmbedAssembliesIntoApk=true `
    -p:AndroidFastDeploymentType=None
```

### Acceptance build

Use an embedded Release APK to confirm lifecycle, performance, and memory fixes:

```powershell
$env:AVALONIA_TELEMETRY_OPTOUT = '1'
dotnet build src/Mireya.Client.Android/Mireya.Client.Android.csproj `
    -c Release `
    -r $androidRid `
    -p:EmbedAssembliesIntoApk=true `
    -p:AndroidFastDeploymentType=None
```

`Mireya.ApiClient` generates its NSwag client during normal builds. For repeated
Android-only builds, `/p:SkipNSwag=true` is acceptable only when the API contract and
generated client are not changing and the generated file is already current.

Locate the APK instead of assuming an output path:

```powershell
$apk = Get-ChildItem src/Mireya.Client.Android/bin -Recurse -Filter '*-Signed.apk' |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

$apk.FullName
```

Confirm that `$apk` belongs to the intended configuration and runtime identifier
before installing it.

## 5. Install and launch without destroying state

Install over the existing package to retain backend selection, pairing, and cached
assets:

```powershell
adb -s $device install -r $apk.FullName
adb -s $device shell monkey `
    -p dev.moritzreis.mireya `
    -c android.intent.category.LAUNCHER `
    1
```

Avoid `adb uninstall` and `adb shell pm clear` unless losing application state is an
intentional part of the test. If a clean-state reproduction is necessary, first note
what will be lost and obtain permission when the state matters.

Confirm that the process started:

```powershell
$pidNow = (adb -s $device shell pidof dev.moritzreis.mireya).Trim()
$pidNow
```

An empty PID means the app did not start or already exited. A changing PID during a
test means Android restarted or killed the process.

## 6. Capture a clean reproduction

Clear old logs immediately before a planned reproduction, not after a crash:

```powershell
adb -s $device logcat -c
adb -s $device shell am force-stop dev.moritzreis.mireya
adb -s $device shell monkey `
    -p dev.moritzreis.mireya `
    -c android.intent.category.LAUNCHER `
    1
```

Reproduce the smallest reliable sequence and record:

1. The exact screen and user action.
2. Whether the API was reachable and what the API logged.
3. Whether image, video, or website content was active.
4. The elapsed time before failure.
5. Whether the PID changed.
6. Whether the issue occurs in Debug, embedded Debug, and embedded Release.

Change one relevant variable at a time during A/B testing.

## 7. Collect diagnostics

Capture the full buffered log before filtering it:

```powershell
New-Item -ItemType Directory -Force artifacts/android-debug | Out-Null
adb -s $device logcat -d -v threadtime > artifacts/android-debug/logcat.txt
```

Then inspect common failure signals:

```powershell
Get-Content artifacts/android-debug/logcat.txt |
    Select-String -Pattern 'FATAL EXCEPTION|AndroidRuntime|dotnet|mono|SIGABRT|SIGSEGV|OutOfMemory|lowmemory|has died|Force removing|ANR'
```

When the process is alive, a PID-scoped live log reduces noise:

```powershell
$pidNow = (adb -s $device shell pidof dev.moritzreis.mireya).Trim()
adb -s $device logcat --pid=$pidNow -v threadtime
```

Capture a screenshot and useful Android state:

```powershell
adb -s $device exec-out screencap -p > artifacts/android-debug/screen.png
adb -s $device shell dumpsys activity activities > artifacts/android-debug/activity.txt
adb -s $device shell dumpsys meminfo dev.moritzreis.mireya > artifacts/android-debug/meminfo.txt
adb -s $device shell dumpsys package dev.moritzreis.mireya > artifacts/android-debug/package.txt
```

Treat these artifacts as local evidence. Review them for backend URLs, pairing data,
tokens, or other sensitive values before sharing or committing them.

## 8. Route the investigation

| Symptom | Start with |
| --- | --- |
| Cannot connect or register | API host logs, `/api/info`, `10.0.2.2`, network state, cleartext HTTP configuration |
| App exits immediately | Full logcat, `AndroidRuntime`, managed exception, ABI and package configuration |
| Black or frozen screen | Screenshot, activity state, Avalonia logs, content type, UI-thread blocking |
| Image issue | Shared playback/view model first; decoded dimensions, bitmap lifetime, and cache behavior |
| Video issue | `AndroidVideoAssetDisplay`, Media3/ExoPlayer logs, codec and URI accessibility |
| Website issue | `AndroidWebsiteAssetDisplay`, WebView logs, network/TLS behavior, native-control lifecycle |
| App restarts in background/foreground | PID tracking, activity lifecycle, Android process-death evidence |
| ANR | Main-thread stalls, activity dump, full logcat, expensive synchronous I/O |
| Memory growth | `dumpsys meminfo`, stable PID, timed sampling, embedded Release comparison; follow the [memory runbook](android-memory.md) |

Desktop and Android do not use identical native renderers. Desktop uses WebView2 and
LibVLC; Android uses Android WebView and Media3/ExoPlayer. A platform-specific media
failure should not automatically be fixed in shared code.

## 9. Verify the fix

Verification should mirror the original failure and include a nearby regression case:

1. Repeat the exact reproduction on the same device and configuration.
2. Confirm the PID remains stable unless a restart is expected.
3. Search the resulting full log for exceptions, ANRs, native crashes, OOM, and
   process-death messages.
4. Exercise another asset or lifecycle transition to detect regressions.
5. Run shared-client tests when shared behavior changed:

```powershell
dotnet test src/Mireya.Client.Core.Tests/Mireya.Client.Core.Tests.csproj -c Release
```

6. Confirm performance and memory findings with an embedded Release APK.
7. Repeat release-critical behavior on a representative physical ARM64 Android TV
   device before production rollout.

In the final report, include the device, Android version, ABI, build configuration,
runtime identifier, reproduction steps, evidence, code change, commands run, and any
verification that remains outstanding.
