# Privacy Policy

Last updated: July 26, 2026

Mireya is an open-source digital-signage system. This policy describes the Mireya display client distributed through the Microsoft Store.

## Data handled by the display client

The display client stores the following data on the Windows device:

- The backend addresses configured by the operator.
- Credentials issued by the operator's Mireya backend. Credentials are protected with Windows Data Protection API (DPAPI).
- A local database containing screen and synchronization state.
- Cached image and video assets supplied by the configured backend.
- WebView2 browser data created while displaying website assets.

The client sends registration, health, synchronization, now-playing, and proof-of-play information to the Mireya backend selected by the operator. That backend is operated by the user or organization deploying Mireya, not by the Mireya open-source project.

## Website assets and third parties

When an operator adds a website asset, the display client loads that address using Microsoft Edge WebView2. The website may collect data under its own privacy policy. Operators are responsible for choosing appropriate website assets and backend configuration.

## Analytics and advertising

The Mireya display client does not include advertising, behavioral analytics, or tracking services operated by the Mireya project.

## Retention and deletion

Local data remains on the device until it is replaced during synchronization, cleared by the application, or removed when the application data is reset or uninstalled. Data stored by the configured Mireya backend is controlled by that backend's operator.

## Support

Questions and privacy requests concerning a particular deployment should be directed to the organization operating that deployment. For issues with the open-source client, use the [Mireya issue tracker](https://github.com/clFaster/Mireya/issues).
