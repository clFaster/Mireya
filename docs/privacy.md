# Privacy Policy

Last updated: August 2, 2026

> **In short:** Mireya does not contain advertising, project-operated analytics, tracking SDKs, or crash-reporting services. The display client does not connect to a central Mireya cloud. It connects only to a Mireya backend selected by you or your organization. That backend, any websites shown as signage content, Microsoft Store, and the infrastructure used to serve this documentation may process data independently as described below.

This privacy notice is intended to provide the information required by [Article 13 of the EU General Data Protection Regulation (GDPR)](https://eur-lex.europa.eu/eli/reg/2016/679/oj/eng#art_13). It covers the Mireya display client distributed through Microsoft Store, this public documentation website, and privacy-related contact with the Mireya project.

## 1. Controller and contact

For this documentation website and privacy-related communication with the Mireya project, the controller is:

**Moritz Reis**<br>
Vienna, Austria<br>
Email: [legal@moritzreis.dev](mailto:legal@moritzreis.dev)

No data protection officer has been appointed.

Mireya is self-hosted software. If you use a Mireya backend operated by an employer, customer, school, venue, managed-service provider, or another organization, that organization determines why and how deployment data is processed and is normally the controller for that processing. Please contact that operator for its privacy notice or to exercise rights relating to that deployment.

## 2. What the display client processes

The Windows display client processes the following data to connect to the selected backend, synchronize signage content, and provide reliable playback:

| Category | Examples | Where it goes |
| --- | --- | --- |
| Backend settings | Backend address, display name, backend identifiers, connection timestamps, and client settings | Stored locally on the display device |
| Authentication data | A client-generated username and password, access tokens, and refresh tokens | Stored locally; credentials are protected for the current Windows user with Windows DPAPI and are sent only to the selected backend when authenticating |
| Signage configuration | Screen, campaign, schedule, asset, and synchronization state | Stored in a local SQLite database and synchronized with the selected backend |
| Cached content | Images, videos, website addresses, thumbnails, and related asset metadata supplied by the operator | Stored locally so content can continue playing offline |
| Operational messages | Registration details, screen identifiers, connection and last-seen state, currently playing asset name and identifier, proof-of-play time, download progress, and synchronization errors | Sent to the selected backend |
| Website playback data | WebView2 cache, cookies, site storage, and other browser data created by a website used as signage content | Stored in Mireya's local WebView2 profile; network requests are sent directly to that website and its service providers |

The application writes diagnostic messages to the device's local console/debug output. Mireya does not upload those messages to the project developer. The client does not request precise location, contacts, camera, microphone, or advertising identifiers.

The project developer does **not** receive the data listed in this section merely because you install or use the client.

## 3. No Mireya analytics or advertising

The Mireya display client contains no project-operated:

- behavioral analytics or telemetry service;
- advertising or cross-app tracking SDK;
- user profiling or automated decision-making; or
- remote crash-reporting service.

The client does not create a Mireya project account and does not send usage data to Moritz Reis. Operating-system and Microsoft Store services may process their own diagnostics independently of Mireya; see section 6.

## 4. The backend selected by the operator

A Mireya backend is required for the display client to work. The project does not provide a hosted Mireya backend. The backend address is selected by the person or organization deploying the display.

The self-hosted backend can store and process:

- administrator accounts, email addresses, roles, authentication cookies, and security tokens;
- display names, identifiers, descriptions, locations entered by an administrator, screen resolution, approval status, online state, and last-seen times;
- content, campaigns, schedules, screen assignments, and uploaded media;
- asset download state, progress, and error messages;
- now-playing and proof-of-play records containing display, asset, and time information; and
- an audit log containing the administrator identity, action, affected item, time, and a short summary.

The backend host, reverse proxy, firewall, or hosting provider may also process technically necessary connection data such as IP address, request time, requested path, user agent, and error information. Mireya's server produces application logs and traces. If the operator configures an OpenTelemetry export endpoint, telemetry is sent to the collector selected by that operator. Optional offline-alert webhooks send screen availability notifications to the webhook service selected by the operator.

Depending on how screens are named, located, or assigned, some of this information may be personal data. The backend operator is responsible for choosing a lawful basis, limiting access, configuring processors and international transfers, setting retention periods, securing the deployment, and answering data-subject requests. Current Mireya server records remain until the operator deletes them or applies its own retention procedure; Mireya does not impose an automatic retention period.

## 5. This documentation website

This website does not provide accounts, forms, advertising, or project-operated visitor analytics. Its code does not set first-party analytics cookies or create visitor profiles.

Technical requests are nevertheless processed by the following infrastructure providers:

The browser supplies this technical request data automatically. It is not a statutory or contractual requirement, but the website and CDN cannot return the requested page without processing a network address and related request information.

### GitHub Pages hosting

The documentation is hosted on GitHub Pages, a service of GitHub. GitHub states that a visitor's IP address is logged and stored for security purposes whenever a GitHub Pages site is visited. GitHub may also receive ordinary request information such as date and time, requested page, referring page, browser, operating system, and device information.

This processing is necessary to deliver and secure the website. The legal basis on the controller's side is the legitimate interest in making the documentation reliably and securely available, Article 6(1)(f) GDPR. See [GitHub Pages data collection](https://docs.github.com/en/pages/getting-started-with-github-pages/what-is-github-pages#data-collection) and the [GitHub Privacy Statement](https://docs.github.com/en/site-policy/privacy-policies/github-general-privacy-statement).

### jsDelivr content delivery network

The documentation loads Docsify, Mermaid, and related static files from the jsDelivr content delivery network operated by Volentio JSD Limited. To return those files, jsDelivr and its infrastructure providers receive the visitor's IP address and request information such as browser type, requested CDN URL, referring domain, date, time, and diagnostic data. jsDelivr states that this information is used for analytics and security and that it does not track individual users.

The purpose is the fast, reliable, and secure delivery of the documentation interface. The legal basis on the controller's side is the legitimate interest in providing the documentation efficiently and securely, Article 6(1)(f) GDPR. See the [jsDelivr privacy policy](https://www.jsdelivr.com/terms/privacy-policy) and [jsDelivr sub-processor information](https://www.jsdelivr.com/terms/sub-processors).

### Recipients, transfers, and retention

Recipients of website request data are GitHub and jsDelivr together with the infrastructure providers identified in their privacy notices. These providers may process data in the United States, the United Kingdom, the European Economic Area, and other locations in which their delivery networks operate. Their notices describe the applicable transfer safeguards, including adequacy frameworks or standard contractual clauses where relevant.

The controller does not receive GitHub Pages or CDN request logs and cannot use them to identify visitors. GitHub and jsDelivr determine the precise retention of the technical data they process. Under their published policies, retention is based on what is necessary to provide and secure their services and to meet legal obligations.

External links are not loaded merely because they appear in the documentation. If you follow a link, the destination website processes the resulting request under its own privacy policy.

## 6. Microsoft Store

Microsoft independently operates Microsoft Store, including app discovery, download, licensing, updates, reviews, payment, and Store-level diagnostics. Mireya does not receive your Microsoft account credentials, payment data, or Store browsing history. Microsoft's processing is governed by the [Microsoft Privacy Statement](https://www.microsoft.com/privacy/privacystatement).

## 7. Website assets shown by an operator

An operator can add a website address as signage content. On Windows, Mireya displays that address using Microsoft Edge WebView2. Loading a website discloses at least the display's network IP address and ordinary browser request information to that website and its providers. The website may use cookies, local storage, analytics, embedded content, or other tracking according to its own policy.

Mireya does not select those websites, receive their data, or control their retention. The deployment operator is responsible for choosing appropriate website assets, obtaining any required consent, and configuring the display and network consistently with applicable law.

## 8. Contact and support data

If you email the address above, the sender address, name (if supplied), message, attachments, and related communication metadata are processed to answer the request. Providing this information is voluntary, but without a usable contact method the request may not be answerable.

The legal basis is Article 6(1)(b) GDPR where the request concerns a contract or steps requested before entering one. Otherwise it is Article 6(1)(f) GDPR: the legitimate interest in answering enquiries, handling privacy requests, maintaining the project, and resolving security or legal issues. The email provider is a recipient where necessary to deliver the message. Correspondence is retained only as long as needed to resolve the request and meet any applicable legal obligations.

Support is also available through the public [Mireya issue tracker](https://github.com/clFaster/Mireya/issues). Information posted there is normally public and is additionally processed by GitHub under its own privacy statement. Do not include credentials, private deployment data, or unnecessary personal data in an issue.

## 9. Local data retention and deletion

Local client data remains on the display until it is replaced during synchronization, a backend or cached item is removed, the application data is cleared, or the application is uninstalled, subject to the operating system's storage behavior. WebView2 website data remains in Mireya's local WebView2 profile until that profile or the application's local data is cleared.

For data held by a selected Mireya backend, contact the backend operator. For data processed by Microsoft Store, GitHub, jsDelivr, or a displayed website, consult and contact the relevant provider.

## 10. Your GDPR rights

Where the GDPR applies, you may have the right to:

- request access to and a copy of your personal data;
- request correction or completion of inaccurate data;
- request erasure of your data;
- request restriction of processing;
- receive data you provided in a portable format where the legal requirements are met;
- object to processing based on legitimate interests; and
- withdraw consent at any time where processing is based on consent, without affecting earlier lawful processing.

To exercise rights concerning this documentation website or direct communication with the Mireya project, email [legal@moritzreis.dev](mailto:legal@moritzreis.dev). A response will normally be provided within one month. Because the controller does not receive hosting or CDN request logs, it may not be possible to identify a particular website visit without additional information.

For deployment data, contact the operator of the Mireya backend. That operator, rather than the open-source project, controls those records.

## 11. Changes to this notice

This notice will be updated when Mireya's data flows, hosting providers, or legal requirements materially change. The current version and its update date are always published on this page.
