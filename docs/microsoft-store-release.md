# Microsoft Store Release

Stable Mireya tags are packaged as a self-contained x64 MSIX, checked with the Windows App Certification Kit, and submitted to Partner Center by `.github/workflows/publish-microsoft-store.yml`. Pre-release tags continue through the other release workflows but are intentionally not submitted to the public Store.

## One-time Partner Center setup

Reserve **Mireya Digital Signage** in Partner Center and copy the values from **Product identity** into these GitHub repository variables:

| Repository variable | Partner Center value |
| --- | --- |
| `MICROSOFT_STORE_PACKAGE_IDENTITY_NAME` | Package/Identity/Name |
| `MICROSOFT_STORE_DISPLAY_NAME` | Package/Properties/DisplayName |
| `MICROSOFT_STORE_PUBLISHER` | Package/Identity/Publisher |
| `MICROSOFT_STORE_PUBLISHER_DISPLAY_NAME` | Publisher display name |

Create a Microsoft Entra application, associate it with Partner Center, and grant it the **Manager** role. Add these GitHub repository secrets:

| Repository secret | Value |
| --- | --- |
| `AZURE_AD_TENANT_ID` | Microsoft Entra tenant ID |
| `PARTNER_CENTER_SELLER_ID` | Partner Center seller ID |
| `AZURE_AD_APP_REGISTRATION_CLIENT_ID` | App registration client ID |
| `AZURE_AD_APP_REGISTRATION_CLIENT_SECRET` | App registration client secret |
| `STORE_APP_ID` | Partner Center product ID |

The Store CLI currently supports automated updates for free products. The product must already exist in Partner Center, and the listing must be completed there before the first committed submission can pass certification.

## Release behavior

A stable tag such as `v0.2.0` starts the Docker and Microsoft Store workflows independently. The Store workflow:

1. Injects the Partner Center identity and release version into the package manifest.
2. Builds `Mireya.Client.Desktop.Package.wapproj`, which publishes the desktop client as a self-contained `win-x64` application and creates the MSIX.
3. Uses the committed package manifest and Store artwork from the packaging project.
4. Signs it with an ephemeral development certificate for the certification run.
5. Runs the Windows App Certification Kit and uploads its XML report and the MSIX as workflow artifacts.
6. Submits and commits the package through the Microsoft Store Developer CLI.

Microsoft Store package versions have three usable numeric components and require a non-zero first component. Mireya therefore maps `major.minor.patch` to `(major + 1).minor.patch.0`; for example, Mireya `0.2.0` becomes Store package `1.2.0.0`. This preserves version ordering through the pre-1.0 period.

Because `v0.1.0` predates this workflow, publish it by manually running **Publish Microsoft Store** with version `0.1.0` and `submit_to_store` enabled.

Run the workflow manually with `submit_to_store` disabled to build, sign, and certify a package without changing Partner Center.

## Local package build

Install .NET 10 and Visual Studio or Visual Studio Build Tools with the Windows SDK and **MSIX Packaging Tools**. Open the standalone packaging project directly in Visual Studio when using its manifest designer or Store-association wizard; it is intentionally not part of the cross-platform `.slnx`. Before producing a local Store package, replace the development identity in `src/Mireya.Client.Desktop.Package/Package.appxmanifest` with the values from Partner Center and set its four-part package version.

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

The unsigned package is written below `src/Mireya.Client.Desktop.Package/AppPackages`. The release workflow redirects it to `artifacts/microsoft-store/package` and supplies the real Partner Center identity automatically. Microsoft signs the production package after certification; a private code-signing certificate is not required for Store-only distribution.

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
