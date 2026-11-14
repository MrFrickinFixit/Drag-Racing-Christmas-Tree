# Packaging and GitHub releases

Build prerequisites: Windows, .NET 10 SDK, and [Inno Setup 6.3+ or 7](https://jrsoftware.org/inno-setup/downloads).

```powershell
./installer/build.ps1 -Version 1.0.4
```

Supply `-IsccPath 'C:\path\ISCC.exe'` if the compiler is not auto-detected. Use a fresh `-OutputRoot` to rebuild an existing version; the script refuses a nonempty output directory to prevent stale files entering a release.

The script runs the application checks, publishes an untrimmed self-contained `win-x64` application, builds a portable ZIP and Inno Setup EXE, and generates SHA-256 checksums. Files appear in `artifacts/v1.0.4/`. Do not publish the `publish/` directory separately; it is the staging payload for the EXE and ZIP. Build outputs and downloaded tools are ignored by Git.

The installer has a stable AppId for upgrades, per-user installation, version metadata, Start-menu and optional desktop shortcuts, running-app detection, and an uninstaller. Racer preferences are not packaged and are preserved on uninstall. No software license is asserted by the installer; choose a repository license separately if desired. Production code signing can be added later when a signing certificate is available.

Push this source and `.github/workflows/release.yml` to the intended GitHub repository. Then either push a version tag or run **Build and release Drag Tree** from the Actions page with a version input. The workflow tests and builds on Windows, uploads build artifacts, and publishes a GitHub release with the installer, portable ZIP, and checksum file. Releases containing a version suffix are marked prerelease. Existing releases are never overwritten.

```powershell
git tag v1.0.4
git push origin v1.0.4
```

The workflow uses the repository's `GITHUB_TOKEN` with `contents: write`; no personal token is required. Manual runs create the release tag at the checked-out commit. Release notes are maintained in `installer/RELEASE_NOTES.md`.

Before publishing, validate the installer on a clean Windows installation: install, launch, upgrade using the same AppId, and uninstall. A silent installation can be tested with `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /LOG=install.log`. Do not use a working source folder as the install target.
