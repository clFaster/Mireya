## Overview

Work package: package and publish the Mireya desktop display client to the Microsoft Store for the first public release.

The current desktop client builds as an Avalonia `net10.0` Windows executable. This issue covers converting the Windows desktop head into a Store-submittable package, validating runtime dependencies, preparing store metadata, and completing the first Partner Center submission.

## Requirements

- Decide and implement the Store packaging approach for the Avalonia desktop client, most likely MSIX packaging for `src/Mireya.Client.Desktop`.
- Add Windows package identity, display name, publisher metadata, versioning, app icon assets, and required manifest capabilities.
- Validate runtime dependencies for Store delivery, including WebView2 and LibVLC behavior inside the packaged app.
- Produce a repeatable Release build/package command or CI workflow for the Store artifact.
- Run Microsoft Store readiness checks, including Windows App Certification Kit validation where applicable.
- Prepare Store listing content: short description, full description, screenshots, category, privacy/support URLs, age rating inputs, and release notes.
- Submit the package through Partner Center and document any certification feedback.

## Acceptance Criteria

- A signed or Store-ready MSIX/MSIX bundle is produced from a clean checkout.
- The packaged app launches on a clean Windows 10/11 test machine, accepts a backend URL, registers as a screen, receives approval, syncs assets, and plays image, video, and website assets.
- The app passes local certification/readiness checks or every warning is documented with a release decision.
- Store listing assets and metadata are complete enough for first submission.
- The Microsoft Store submission reaches certification, and any rejection items are captured as follow-up issues.

## Additional Notes

- Existing project: `src/Mireya.Client.Desktop/Mireya.Client.Desktop.csproj`.
- Current packaging docs say Windows MSIX/Microsoft Store packaging has not been added yet.
- Microsoft recommends MSIX packaging for Store submission and notes that Store submission runs through Partner Center: https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/publish-first-app
- Microsoft MSIX package requirements include package format and versioning rules: https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/app-package-requirements
