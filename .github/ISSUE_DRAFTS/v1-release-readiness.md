## Overview

Work package: define the v1.0 release baseline for Mireya so the backend Docker image, Windows desktop client, and Android TV client can ship as one coherent first release.

This issue tracks the shared release checklist, versioning decisions, verification scope, and release notes that the platform-specific publishing work depends on.

## Requirements

- Define the first public release version and apply it consistently across backend image tags, desktop package metadata, Android package metadata, docs, and release notes.
- Create a v1.0 go/no-go checklist covering backend startup, admin login, screen registration and approval, asset upload, campaign scheduling, display playback, offline alerting, proof-of-play reporting, and upgrade/reinstall behavior.
- Document the release artifact matrix: Docker image, Microsoft Store package, Google Play Android TV release, and any manually produced fallback artifacts.
- Confirm which registry/store accounts own the release and which secrets or credentials must be configured outside the repository.
- Add or update release documentation so a maintainer can reproduce every v1.0 artifact from a clean checkout.

## Acceptance Criteria

- A v1.0 release checklist exists and references the exact commands or workflows used to build and verify each artifact.
- Version numbers and display names are consistent across the backend, desktop client, Android client, docs, and GitHub release notes.
- Required owner-only setup is documented without committing secrets, certificates, keystores, or private account material.
- The release is not considered ready until the Docker image, Microsoft Store submission, and Google Play submission issues are complete or explicitly waived.

## Additional Notes

- Current repo state has Docker support, a Windows/Linux Avalonia desktop client, and an Android TV client, but store-ready packaging remains roadmap work in `docs/packaging.md`.
- The existing PR workflow builds the API, API client, desktop client, and tests. Android and release artifact workflows should be considered part of this work package or the platform-specific issues.
