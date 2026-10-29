# BigFatFile Architecture: Deployment

How the Origin Server, the Replica Server, the desktop client and the CLI are packaged, installed
and updated on each platform. Referenced from `Architecture.md`.

## 1. Rules for every artifact

- One release tag builds every artifact below: images, installers, packages, CLI
  binaries and the package-manager manifests. All carry the same version number, and
  each client reports its version to the Origin Server so the admin UI can show outdated
  clients.
- Every .NET artifact is self-contained: no runtime to install on the target machine.
- Origin Server and Replica Server containers use BBR congestion control
  (`Architecture.Transfer.md`, section 5):
    - the compose files set `sysctls: net.ipv4.tcp_congestion_control: bbr` on the
      Origin Server and Replica Server services; the setting is per container network, so the host
      keeps its own;
    - the host must have the `tcp_bbr` kernel module loaded; the docs give the one
      command, and the Origin Server logs a warning at startup when it runs on another
      algorithm.

## 2. Server

- Primary: Docker Compose.
- Images:
    - `bigfatfile` (public): the open core; built by the public pipeline;
    - `bigfatfile-licensed` (private pipeline): `FROM` the public image, plus the licensed
      module assemblies in `/app/modules/`;
    - published on GHCR with tags `1.4.2`, `1.4`, `1`, `edge` and a commit tag; compose
      files pin the major;
    - built for linux/amd64 and linux/arm64;
    - carry an SBOM; signed with cosign through GitHub's OIDC; OCI labels point at the
      source and licence.
- Reference deployment, one `docker-compose.yml` with a `.env.example`:
    - the Origin Server plus a pinned PostgreSQL; a `local` default warehouse on a named volume;
    - profiles add optional targets (`garage`, `minio`, extra volumes for more local
      targets) and a `proxy` profile (Caddy with automatic TLS);
    - each target a profile starts is referenced from the warehouse file inside the BFF
      container by service name or mounted path;
    - override files cover the cluster and development cases;
    - migration, backup and restore are commands of the Origin Server image.
- Other hosts:
    - one-click templates for Unraid Community Applications, TrueNAS SCALE, CasaOS,
      Portainer and Coolify, all wrapping the same compose file;
    - bare-metal Linux without Docker: a self-contained tarball plus a systemd unit, with
      PostgreSQL installed by the operator; documented, not recommended;
    - Windows Server is not a target for the Origin Server.
- Upgrade:
    - `docker compose pull && docker compose up -d`; migrations run at startup;
    - a minor release never needs manual steps; breaking changes wait for a major and are
      documented in `UPGRADING.md`;
    - the PostgreSQL major version is pinned, with the upgrade path documented.
- Cluster mode (`BFF_CLUSTER=true`):
    - requires every warehouse to be shared storage;
    - turns on the `NOTIFY`-based change notifier;
    - everything else is identical to single-node; a two-node compose file is part of
      the integration tests.
- Backup:
    - `bff backup` produces one archive (database dump plus local warehouses);
      `bff restore` consumes it;
    - S3 warehouses are backed up by the object store's own means;
    - the same path migrates v1 data.
- The server validates its configuration at startup and names the variable that is
  wrong.
- The cloud and paying dedicated instances run the licensed image with a licence key;
  self-hosters run the public image. A dedicated client instance is the reference
  deployment on its own VPS.

## 3. Replica Server

- Primary: Docker Compose, one container, one volume for the mirror.
- Pairing:
    - an administrator creates the Replica Server in the web UI and receives a one-time pairing
      code;
    - `bff-replica pair <code>` (or the same value in the container's environment)
      fetches the Origin Server address, the tenant and the key pair; nothing else to configure.
- Hosts:
    - NAS: the NAS's container UI with the compose file, or the same one-click templates
      as the Origin Server;
    - Linux box or mini PC: Docker, or `.deb` and `.rpm` packages with a systemd service;
    - Windows machine, for offices with no NAS: a self-contained Windows service
      installed by an MSI, also available through `winget`;
    - macOS: a launchd service in a `.pkg`; low priority.

## 4. NAS hosts

- Origin Server and Replica Server both run on NAS units that have Docker, which is most units sold to
  small businesses:
    - Synology (DSM 7.2 and later, Container Manager, "Project" takes a Compose file);
    - QNAP (Container Station, x86 and newer ARM);
    - Asustor, TerraMaster, UGREEN (Docker apps);
    - Unraid (built in; Community Applications templates);
    - TrueNAS SCALE 24.10 and later (Docker-based Apps; custom app from Compose);
    - OpenMediaVault (omv-extras with the Compose plugin).
- Not supported: units without Docker (WD My Cloud, entry ARM models from Synology and
  QNAP). Those offices use the Windows-service Replica Server.
- What the images do for NAS hosts:
    - `linux/arm64` builds;
    - modest memory: the compose file sets PostgreSQL to `shared_buffers=128MB` and a
      small `max_connections`; the Replica Server stays well under 500 MB;
    - `PUID` and `PGID` environment variables (or a `user:` override) so bind mounts owned
      by the NAS user work without permission errors;
    - data on plain directories of the NAS pool: the `local` warehouse and the Replica Server's
      mirror are bind mounts of a share;
    - the Replica Server's SQLite file must be on the NAS's own filesystem (Btrfs or ext4), never
      on an SMB or NFS mount;
    - health check and restart policy, so the NAS UI shows the state and restarts after a
      power cut;
    - no privileged mode, no host networking; one published port each;
    - the NAS's built-in reverse proxy with Let's Encrypt sits in front of the Origin Server's
      port.

## 5. Desktop

| Platform | Primary                                                                                                         | Also                                                        | Updates                                                                    |
| -------- | --------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------- | -------------------------------------------------------------------------- |
| Windows  | MSIX, signed, installed from the website through App Installer (`.appinstaller`) or `winget install BigFatFile` | Microsoft Store; Intune and Group Policy for managed fleets | App Installer checks the `.appinstaller` URL and updates in the background |
| macOS    | `.app` in a DMG, Developer ID signed and notarized, File Provider extension and the .NET agent inside           | `brew install --cask bigfatfile`; Mac App Store later       | Sparkle                                                                    |
| Linux    | `.deb` and `.rpm` from one nfpm definition; installs `fuse3` and a systemd user service                         | AppImage or tarball; an apt and a dnf repository            | package manager                                                            |

- MSIX is required on Windows: sync root registration and the Explorer context menu need
  package identity, and it uninstalls cleanly.
- Flatpak and Snap are not used for the Linux client: their sandboxes do not allow FUSE
  mounts.
- The desktop package includes the CLI.

## 6. CLI

| Platform | Install                                                                                                   |
| -------- | --------------------------------------------------------------------------------------------------------- |
| Windows  | `winget install BigFatFile.Cli`, `scoop install bff`, or the desktop package                              |
| macOS    | `brew install bigfatfile/tap/bff`                                                                         |
| Linux    | the apt and dnf repositories, or a one-line install script that downloads the binary from GitHub Releases |
| Any      | `dotnet tool install -g bff` for people who have .NET; a single binary from GitHub Releases               |

- Built with Native AOT where its dependencies allow it; otherwise self-contained and
  trimmed.
- `bff self-update` covers installs that did not come through a package manager.
- The server image also contains it.

## 7. Signing

- Windows: one code-signing certificate (an OV certificate from a public CA, or Azure
  Trusted Signing if GCAS meets its company-history requirement) for MSIX, MSI and the
  CLI. Unsigned installers are blocked by SmartScreen.
- macOS: one Apple Developer account for Developer ID signing and notarization.
- Linux: one GPG key for the apt and dnf repositories.
- Docker images: cosign through GitHub's OIDC.
