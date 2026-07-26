## Overview

Work package: publish a production-ready Mireya backend Docker image for the first public release.

The repository already contains a `Dockerfile` and `docker-compose.yml`; this issue turns that local deployment path into a reproducible release artifact with registry publishing, tagging, verification, and documentation.

## Requirements

- Add a GitHub Actions workflow that builds the backend Docker image from the existing `Dockerfile`.
- Publish the image to the selected registry, preferably `ghcr.io/clfaster/mireya` or a documented alternative chosen by the maintainer.
- Generate stable tags for release versions, commit SHAs, and `latest` only for the current stable release.
- Consider multi-platform publishing for `linux/amd64` and `linux/arm64`, or explicitly document why v1 only supports one platform.
- Verify the published image can run with PostgreSQL using the existing Compose configuration or an equivalent clean smoke test.
- Document pull/run commands, required environment variables, volumes, health checks, and initial admin password behavior.

## Acceptance Criteria

- A maintainer can publish the Docker image from a tagged release or manual release workflow without local machine state.
- The published image starts successfully with PostgreSQL and reports healthy readiness through `/health`.
- Uploaded media persists through the documented `/app/uploads` volume.
- The image includes OCI labels for source repository, revision, version, and license.
- Release documentation shows how to pull the image, start it, configure `DefaultAdminUser__Password`, and confirm the admin UI is reachable.

## Additional Notes

- Existing files to build from: `Dockerfile`, `docker-compose.yml`, `src/Mireya.Api/Mireya.Api.csproj`.
- GitHub's Docker publishing guide documents publishing to GHCR with `docker/login-action`, `docker/metadata-action`, and `docker/build-push-action`: https://docs.github.com/en/actions/tutorials/publish-packages/publish-docker-images
- Docker's GitHub Actions docs cover multi-platform builds with `platforms: linux/amd64,linux/arm64`: https://docs.docker.com/build/ci/github-actions/multi-platform/
