# Linux packaging

- Status: Accepted
- Recorded: 2026-11-08

## 1. Context

- The Linux desktop client mounts the drive with FUSE ([File materialisation on the desktop](./0002-materialised-desktop-drive.md)).

## 2. Decision

- The Linux client ships as `.deb` and `.rpm` packages built from one nfpm definition,
  with an AppImage or tarball as a second option.

## 3. Alternatives

- Flatpak and Snap:
    - their sandboxes do not allow FUSE mounts.

## 4. Consequences

- The packages install `fuse3` and a systemd user service.
- Updates come from the package manager, through an apt and a dnf repository.
- Details: [Deployment](../architecture/operations/deployment.md), section 5.
