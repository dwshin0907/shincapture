# Code signing policy

## Current status

As of 2026-09-18, ShinCapture's SignPath organization is a free trial and contains
only `ShinCapture Test Cert`. The existing policy named `Release Signing` has the
purpose **Test signing**. No publicly trusted production certificate is currently
configured, and no release should be described as signed by SignPath Foundation.

The planned provider is [SignPath.io](https://signpath.io/), with a certificate
from [SignPath Foundation](https://signpath.org/) through its free open-source
program. This requires Foundation approval. After approval and successful
production signing, the project will use the acknowledgement:

> Free code signing provided by SignPath.io, certificate by SignPath Foundation.

## Team and approval

- Author, committer, and reviewer: [dwshin0907](https://github.com/dwshin0907).
- Intended production signing approver: [dwshin0907](https://github.com/dwshin0907).
- GitHub Actions may submit artifacts built from this repository. Production
  signing must use the Foundation-approved policy and its approval process.
- The maintainer must enable multi-factor authentication for the GitHub and
  SignPath accounts before production signing, as required by the program.

## What is signed

Only ShinCapture's own application and installer are submitted. Builds run on
GitHub-hosted Windows runners. The application is signed first, then included
in the Inno Setup installer, and the installer is signed separately.

Artifact configurations constrain the filenames, product name, and product
version. Upstream libraries are not individually re-signed as ShinCapture code.
The Inno Setup-generated uninstaller is not separately signed by this pipeline.

## Release checks

Every public release must pass the test suite and Windows Authenticode validation
for both `ShinCapture.exe` and the installer. Both must have embedded signatures
and timestamps. Unsigned files, tampered files, and untrusted test certificates
are rejected. CI must not add a test certificate to the Windows trust store or
fall back to an unsigned release if signing fails.

Published versions and their existing assets must not be replaced. A signed
release uses a new version number and includes SHA-256 checksums.

Code signing identifies the publisher and protects file integrity. It does not
guarantee immediate Microsoft SmartScreen reputation or the absence of warnings.

## Privacy

See the [privacy policy](PRIVACY.md) for local capture storage, automatic GitHub
update checks, and user-initiated OpenAI translation requests.

## References

- [SignPath Foundation conditions](https://signpath.org/terms)
- [SignPath test and release certificates](https://docs.signpath.io/managing-certificates)
- [Microsoft SmartScreen reputation](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation)
