# Android memory debugging

Last updated: 2026-08-20

This document records how a specific Android native-memory crash was reproduced,
isolated, fixed, and verified on `emulator-5554`. For the general Android investigation
workflow, see [Android and Android TV debugging](android.md).

Run all commands below from the repository root in PowerShell.

The package name is:

```text
dev.moritzreis.mireya
```

## 1. Confirm the emulator and application state

List connected Android devices:

```powershell
adb devices
```

Check the physical and overridden display size:

```powershell
adb shell wm size
adb shell wm density
```

The final test used the emulator's physical **3840×2160** display. Any temporary size
override can be removed with:

```powershell
adb shell wm size reset
```

The 1080p A/B comparison used:

```powershell
adb shell wm size 1920x1080
```

The override was reset before the final 4K Release soak.

Check whether Mireya is running and record its PID:

```powershell
adb shell pidof dev.moritzreis.mireya
```

A changing PID means the application restarted or was killed. The same PID throughout
a soak test is therefore part of the pass criteria.

## 2. Build the Android client

### Debug build used during isolation

Debug/Fast Deployment builds were useful for adding instrumentation and comparing one
change at a time:

```powershell
$env:AVALONIA_TELEMETRY_OPTOUT = '1'
dotnet build src/Mireya.Client.Android/Mireya.Client.Android.csproj `
    -c Debug `
    -r android-x86 `
    --no-restore `
    --disable-build-servers `
    -m:1 `
    /p:UseSharedCompilation=false `
    /p:SkipNSwag=true
```

Debug builds were not used for the final memory verdict. Even after the app fixes,
Fast Deployment showed residual native growth that did not occur in the packaged
Release build.

### Embedded Release build used for acceptance testing

The exact final build command was:

```powershell
$env:AVALONIA_TELEMETRY_OPTOUT = '1'
dotnet build src/Mireya.Client.Android/Mireya.Client.Android.csproj `
    -c Release `
    -r android-x86 `
    --no-restore `
    --disable-build-servers `
    -m:1 `
    /p:UseSharedCompilation=false `
    /p:SkipNSwag=true `
    /p:EmbedAssembliesIntoApk=true
```

The generated APK is:

```text
src\Mireya.Client.Android\bin\Release\net10.0-android\android-x86\dev.moritzreis.mireya-Signed.apk
```

`EmbedAssembliesIntoApk=true` is important for this check: it produces a self-contained
APK and removes the Fast Deployment override/runtime from the measurement.

## 3. Preserve Debug state before installing Release

The paired client database and downloaded campaign assets were retained so Debug and
Release exercised the same content. Before replacing the Debug build, its active Fast
Deployment override directory was renamed from inside the debuggable package:

```powershell
adb shell "run-as dev.moritzreis.mireya mv files/.__override__ files/.__override__.debug-before-release"
```

This is a recoverable rename rather than deletion. It also prevents stale Debug
assemblies from being loaded during the Release test. `run-as` normally works only
while a debuggable build is installed.

## 4. Install and launch the APK

Install over the existing package so pairing and cached content remain available:

```powershell
adb install -r "src\Mireya.Client.Android\bin\Release\net10.0-android\android-x86\dev.moritzreis.mireya-Signed.apk"
```

Launch the main activity without depending on its fully qualified activity name:

```powershell
adb shell monkey `
    -p dev.moritzreis.mireya `
    -c android.intent.category.LAUNCHER `
    1
```

Confirm the new process:

```powershell
adb shell pidof dev.moritzreis.mireya
```

## 5. Inspect a single memory snapshot

Capture the complete Android memory report:

```powershell
adb shell dumpsys meminfo dev.moritzreis.mireya
```

For this issue, the most useful rows were `Native Heap` and `TOTAL`:

```powershell
adb shell dumpsys meminfo dev.moritzreis.mireya |
    Select-String -Pattern '^\s+Native Heap|^\s+TOTAL\s'
```

The relevant columns are:

- `Native Heap` → `Alloc`: native allocation owned by Skia and other native code.
- `Native Heap` → PSS/RSS-related columns: how much is currently resident/accounted.
- `TOTAL` → PSS: overall process footprint.

During the failing run, native allocation increased by roughly **25–30 MiB/s** and
eventually approached 2 GiB. Managed/Java memory did not show the same trend.

## 6. Sample memory over time

The following is the PowerShell sampling pattern used for the final smoke test. It
prints elapsed time, PID, the entire native-heap row, and the total row:

```powershell
$watch = [Diagnostics.Stopwatch]::StartNew()

1..10 | ForEach-Object {
    $pidNow = (adb shell pidof dev.moritzreis.mireya).Trim()
    $mem = adb shell dumpsys meminfo dev.moritzreis.mireya
    $native = $mem |
        Where-Object { $_ -match '^\s*Native Heap\s' } |
        Select-Object -First 1
    $total = $mem |
        Where-Object { $_ -match '^\s*TOTAL\s+\d' } |
        Select-Object -First 1

    '{0,5:N1}s PID={1} | {2} | {3}' -f `
        $watch.Elapsed.TotalSeconds,
        $pidNow,
        $native.Trim(),
        $total.Trim()

    if ($_ -lt 10) {
        Start-Sleep -Seconds 5
    }
}
```

For a five-minute soak, change `1..10` to `1..61`. To retain the trace, pipe the loop's
output through `Tee-Object`:

```powershell
& {
    # Place the sampling loop here.
} | Tee-Object -FilePath .\mireya-memory-trace.txt
```

Pass criteria:

1. The PID remains unchanged.
2. Native allocation reaches a plateau instead of rising linearly.
3. Total PSS remains within a narrow steady-state range.
4. No low-memory kill or fatal exception appears in logcat.

### Final measured results

Five-minute embedded Release soak at 3840×2160:

| Time | Native allocation | Total PSS |
|---:|---:|---:|
| 0.0 s | 132,248 kB | 287,054 kB |
| 304.7 s | 132,001 kB | 286,341 kB |

The same PID remained alive, and both measurements ended slightly lower.

After rebuilding with the final source, a second 46-second smoke test stabilized at
approximately **132.14 MB native allocation** from 10.5 seconds onward on PID `8967`.

## 7. Inspect logcat

Search the full buffered log for the original failure signature:

```powershell
adb logcat -d -v time |
    Select-String -Pattern "lowmemorykiller|Kill 'dev.moritzreis.mireya'|has died|WINDOW DIED|FATAL EXCEPTION|OutOfMemory|SIGKILL"
```

When the app is still running, restrict the output to its current PID:

```powershell
$pidNow = (adb shell pidof dev.moritzreis.mireya).Trim()

adb logcat -d --pid=$pidNow |
    Select-String -Pattern 'FATAL EXCEPTION|OutOfMemory|lowmemory|has died|Force removing|SIGKILL'
```

The final check returned no fatal, OOM, low-memory-kill, or process-death matches.

For a clean future capture, clear old buffered messages immediately before launching:

```powershell
adb logcat -c
```

Do not clear the log while investigating an already completed crash because that would
destroy the evidence.

## 8. Inspect the paired Debug client's content

While the Debug package was installed, `run-as` was used to inspect the app-private
database and downloaded asset cache:

```powershell
adb shell "run-as dev.moritzreis.mireya sh -c 'ls -lR files/Mireya'"
```

When local SQLite inspection was needed, the client database could be copied without
root access while the debuggable package was installed:

```powershell
adb exec-out run-as dev.moritzreis.mireya `
    cat files/Mireya/mireya_client.db > .\mireya-client-debug.db
```

The copied database can contain pairing and environment configuration. Keep it out of
source control and remove it when the investigation is complete.

The cached JPEG dimensions were then compared with their decoded cost. A JPEG's file
size is not its runtime size; an approximate BGRA bitmap costs `width × height × 4`.

For locally copied images, PowerShell can calculate this with:

```powershell
Add-Type -AssemblyName System.Drawing

Get-ChildItem -Filter *.jpg | ForEach-Object {
    $image = [System.Drawing.Image]::FromFile($_.FullName)
    try {
        '{0}: {1}x{2} = {3:N1} MiB decoded' -f `
            $_.Name,
            $image.Width,
            $image.Height,
            ($image.Width * $image.Height * 4 / 1MB)
    }
    finally {
        $image.Dispose()
    }
}
```

This exposed the 6720×4480 image: about **115 MiB decoded** despite being only a few
megabytes on disk.

## 9. Isolate the cause with A/B builds

Only one relevant behavior was changed between each build, followed by the same launch
and memory sampling procedure.

| Experiment | Observation |
|---|---|
| Remove the image fade only | No material change to the Debug growth rate. |
| Change emulator from 4K to 1080p | Reduced rendering cost but did not remove the trend. |
| Bound image decode and cache decoded images | Confirmed smaller images and cache hits; large bitmap churn was removed. |
| Remove the hidden infinite `IdentifyFlash` animation | Debug growth dropped from about 25–30 MiB/s to roughly 1–3 MiB/s. |
| Attach website/video native controls only when needed | Reduced unnecessary image-campaign native surfaces; not the primary trigger. |
| Prefer Vulkan or software rendering | Did not eliminate the remaining Debug-only trend; Vulkan fell back on the emulator. |
| Install a self-contained embedded Release APK | Native memory flattened completely during the five-minute soak. |

This sequence was important: it separated the app-side continuous rendering problem
from the residual Debug/Fast Deployment behavior.

## 10. Run the regression tests

The final Release test command was:

```powershell
$env:AVALONIA_TELEMETRY_OPTOUT = '1'
dotnet test src/Mireya.Client.Core.Tests/Mireya.Client.Core.Tests.csproj `
    -c Release `
    --no-restore `
    --disable-build-servers `
    -m:1 `
    /p:UseSharedCompilation=false `
    /p:SkipNSwag=true
```

Result:

```text
Passed: 9
Failed: 0
Skipped: 0
```

The suite covers bounded decoding, decoded-bitmap reuse, image replacement, content
transitions, cleanup, and disposal lifetime behavior.

## 11. Final repeatable verification checklist

1. Build an embedded Release APK for the emulator ABI.
2. Install with `adb install -r` to retain the paired campaign.
3. Start the app and record its PID.
4. Confirm playback of the real image campaign.
5. Sample `dumpsys meminfo` every five seconds for at least five minutes.
6. Verify that native allocation and total PSS plateau.
7. Verify that the PID does not change.
8. Search logcat for fatal, OOM, LMK, and process-death messages.
9. Run the Release unit tests.
10. Repeat on a physical ARM64 Android TV before production rollout.
