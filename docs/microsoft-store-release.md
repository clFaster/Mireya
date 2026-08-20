# Microsoft Store Release

The main **Release Mireya** workflow can build an x64/ARM64 MSIX bundle and submit it
to Partner Center. Store publishing uses the `stable` GitHub environment.

## One-time Partner Center setup

Reserve **Mireya** in Partner Center. Its Product identity is committed to `src/Mireya.Client.Desktop.Package/Package.appxmanifest`, so every build uses the same Store identity.

In **Settings → Environments → stable**, add the reviewers who must approve a Store
release. An environment reference alone does not create an approval gate.

Create a Microsoft Entra application, associate it with Partner Center, and grant it the **Manager** role. Add these GitHub repository secrets:

| Repository secret | Value |
| --- | --- |
| `AZURE_AD_TENANT_ID` | Microsoft Entra tenant ID |
| `PARTNER_CENTER_SELLER_ID` | Partner Center seller ID |
| `AZURE_AD_APP_REGISTRATION_CLIENT_ID` | App registration client ID |
| `AZURE_AD_APP_REGISTRATION_CLIENT_SECRET` | App registration client secret |
| `STORE_APP_ID` | Partner Center product ID |

The Store CLI currently supports automated updates for free products. The product must already exist in Partner Center, and the listing must be completed there before the first committed submission can pass certification.

## Publish a release

Run **Release Mireya** from `main` and enable **Release the Microsoft Store package**.
The generated bundle is retained as a workflow artifact and submitted to the Store.
The workflow itself is the source of truth for package architectures, version mapping,
validation, and artifact paths.

## Local package build

Install .NET 10 and Visual Studio or Visual Studio Build Tools with the Windows SDK and **MSIX Packaging Tools**. Open the standalone packaging project directly in Visual Studio when using its manifest designer or Store-association wizard; it is intentionally not part of the cross-platform `.slnx`. The manifest already contains the Partner Center identity; set its four-part package version before producing a local Store package.

Run the command-line build from a Developer PowerShell for Visual Studio:

```powershell
msbuild src/Mireya.Client.Desktop.Package/Mireya.Client.Desktop.Package.wapproj `
  /restore `
  /p:Configuration=Release `
  /p:Platform=x64 `
  /p:RuntimeIdentifier=win-x64 `
  /p:AppxBundle=Never `
  /p:AppxPackageSigningEnabled=false
```

The unsigned package is written below
`src/Mireya.Client.Desktop.Package/AppPackages`. Microsoft signs the production
package after certification; a private code-signing certificate is not required for
Store-only distribution.

For a manual readiness run, sign the package with a test certificate whose subject exactly matches the manifest publisher, trust that certificate on the test machine, and run:

```powershell
$msix = Get-ChildItem src/Mireya.Client.Desktop.Package/AppPackages -Filter *.msix -File -Recurse |
  Select-Object -First 1 -ExpandProperty FullName
& "${env:ProgramFiles(x86)}\Windows Kits\10\App Certification Kit\appcert.exe" reset
& "${env:ProgramFiles(x86)}\Windows Kits\10\App Certification Kit\appcert.exe" test `
  -appxpackagepath $msix `
  -reportoutputpath ./artifacts/microsoft-store/WackReport.xml
```

Before the first public submission, install the certified package on a clean Windows 10/11 device and verify backend selection, registration and approval, asset synchronization, image/video/website playback, and remote commands.

## Store listing copy

**Category:** Business

**Short description:** Connect a Windows display to a self-hosted Mireya digital-signage server.

**Full description:**

> Mireya Digital Signage turns a Windows device into a managed display for a self-hosted Mireya server.
>
> Connect the client to your server, approve the screen in the Mireya administration interface, and deliver scheduled image, video, and website campaigns. The client caches media for reliable playback, reconnects automatically, reports proof of play, and responds to remote playback commands.
>
> A Mireya server is required. Mireya is open-source software and does not provide a hosted signage service or bundled content.

**Feature bullets:**

- Display scheduled image, video, and website campaigns.
- Pair and approve screens from the Mireya administration interface.
- Cache media locally and reconnect automatically.
- Report playback and synchronization status.
- Receive remote identify, reload, restart, next, and previous commands.

**Privacy policy:** `https://mireya.moritzreis.dev/#/privacy`

**Support:** `https://github.com/clFaster/Mireya/issues`

**Website:** `https://mireya.moritzreis.dev`

**Release notes template:** `Mireya {version} improves the Windows display client and keeps it aligned with the matching Mireya server release. See the GitHub release notes for details.`

Partner Center still requires screenshots captured from a clean packaged-app test. Include at least the backend selection, pairing/approval, active image campaign, active video campaign, and active website campaign states, without real credentials or private customer content.
