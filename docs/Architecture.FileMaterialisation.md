# BigFatFile Architecture: Materialisation

How the desktop client shows a remote drive as a local folder on each operating system,
how content is cached, and how changes travel in both directions. Referenced from
`Architecture.md`.

## 1. Requirements

- The whole remote drive is visible immediately after sign-in, however large (a 20 TB
  company drive appears in seconds).
- Once a file's content is present, every application works with it at the speed of a
  local disk, including applications that write in place.
- Local disk use is bounded: recently used and pinned content is kept, the rest is let go.
- Nothing kernel-side of our own on Windows or macOS: no driver, no elevated install, no
  signing attestation.
- The engine is written once; each operating system gets an adapter.

## 2. Two models: projection and materialisation

- Projection:
    - our process is the filesystem for the folder; nothing exists on disk;
    - every operation an application performs (open, read, write, lock, map into memory,
      rename) is routed to our process, which must answer with full filesystem semantics;
    - examples: FUSE on Linux; WinFsp and Dokany on Windows (need a driver); ProjFS on
      Windows (built in, designed for developer tooling, no sync-status integration).
- Materialisation:
    - the folder is a real folder on the native filesystem;
    - our process creates real entries lazily; entries are hollow until a file is opened;
    - the operating system's own filter notices the first read of a hollow file and asks
      our process for the bytes;
    - after that the file is an ordinary file: reads, writes, locks and memory maps are
      handled by the native filesystem with no involvement from us;
    - examples: Cloud Files API on Windows (used by OneDrive), File Provider on macOS.
- Decision: materialisation.
    - In-place writes work because a hydrated file is simply a local file: databases,
      Outlook data files, design tools, the Office save sequence (temporary file, lock,
      rename, extra data stream).
    - A projection must reimplement all of that and pays a round trip through our process
      per operation; projections that handle it well do so with a write-back cache, which
      is materialisation rebuilt by hand.
    - If our process stops, hydrated files keep working and hollow ones fail with a clear
      error; a projected drive hangs or disappears.
    - Projection suits read-mostly, never-edited data (a virtual view of a repository, an
      archive). That is not this product.
- Linux has no materialisation API, so the Linux adapter is a FUSE projection that
  emulates the model with a local cache (the Linux adapter in section 6).

## 3. One engine, several adapters

- The sync engine (`BigFatFile.Sync`) owns what is the same on every platform:
    - local state in SQLite: node ID to local identity, last known version and manifest,
      pin state, hydration state, outbox of local changes, change-feed cursor, access
      history;
    - down-sync: applying the change feed to what is materialised locally;
    - content fetching: byte range to manifest chunks; fetch missing chunks from the
      Replica or Origin (or presigned object storage); verify hashes; serve;
    - up-sync: wait until a changed file is closed and quiet; chunk it; compute the delta
      against the last known manifest; upload missing chunks; complete with `If-Match`;
    - conflict copies, ignore rules, bandwidth limits, pause and resume;
    - delete safeguard: if a large share of a folder disappears locally at once, stop and
      ask instead of deleting remotely.
- Each platform implements one adapter:

```csharp
interface ILocalStoreAdapter
{
    AdapterCapabilities Capabilities { get; }

    // Engine -> platform: apply remote state
    Task PopulateDirectoryAsync(NodeId dir, IReadOnlyList<NodeEntry> children);
    Task ApplyRemoteChangeAsync(RemoteChange change);     // create, update, rename, move, delete
    Task InvalidateContentAsync(NodeId node, long newVersion);
    Task EvictContentAsync(NodeId node);
    Task SetPinnedAsync(NodeId node, bool pinned);
    Task MarkInSyncAsync(NodeId node, long version);
    Task WriteConflictCopyAsync(NodeId node, Stream localContent, string conflictName);

    // Platform -> engine: demands and local changes
    event Func<NodeId, Task> DirectoryRequested;
    event Func<ContentRequest, Task> ContentRequested;    // node, byte range, destination, progress, cancellation
    event Func<LocalChange, Task> LocalChanged;           // Created, Modified (with content path), Renamed, Moved, Deleted
    event Func<NodeId, bool, Task> PinChanged;
}

[Flags]
enum AdapterCapabilities
{
    None = 0,
    HandlesConflicts = 1,      // the platform creates conflict copies itself
    HandlesOfflineQueue = 2,   // the platform keeps and retries pending uploads
    HandlesEviction = 4,       // the platform decides what to evict
    HandlesDebounce = 8        // the platform delivers a finished file, not raw writes
}
```

- Capability flags:
    - Windows and Linux report `None`; the engine does everything;
    - macOS reports all four, because File Provider does those jobs itself; the engine
      skips those stages.
- Contract tests run the same scenarios against every adapter:
    - a remote create appears; opening a file hydrates the requested range; an edit
      uploads a delta; a remote rename lands; edits on both sides produce a conflict copy;
      the engine restarts and finishes pending work.
- An in-memory adapter serves engine unit tests and is the base of the Replica's adapter.

## 4. Metadata: lazy everywhere

- The client never downloads the whole tree:
    - directory listings are fetched the first time a directory is opened and cached;
    - the root of a drive of any size appears in under a second after sign-in.
- Per platform:
    - Windows: the sync root uses on-demand population; the first enumeration of a
      directory triggers a callback; the engine fetches that listing and creates
      placeholders for the children only; the engine records which directories are
      populated;
    - macOS: File Provider enumerates per directory on demand;
    - Linux: the FUSE `readdir` fetches on first access and caches.
- Change feed filtering:
    - the feed arrives for the whole tenant;
    - the client applies only entries that touch directories it has populated or cached
      and discards the rest, because an unpopulated directory is fetched fresh when
      opened.
- Pruning (optional): on Windows a directory's child entries can be removed and
  on-demand population re-enabled; on Linux the listing cache has a size cap.

## 5. Content: bounded cache, least recently used

- Cache budget: a fixed share of free space by default, user adjustable.
- Eviction:
    - least recently used first, by the engine's own access records (filesystem access
      times are unreliable);
    - pinned content and files with pending uploads are never evicted.
- Reading:
    - a read of range R on file F maps to the chunks covering R by a binary search over
      the cumulative chunk sizes in F's manifest;
    - present chunks are served from disk; missing ones are fetched, verified, stored,
      served;
    - sequential reads trigger read-ahead of the next chunks.
- Writing:
    - writes land in the local file (Windows, macOS) or a write-back copy (Linux);
    - when the file is closed and quiet, the engine chunks it and uploads the delta.
- Where the cache is:
    - Windows: the hydrated bytes inside the placeholders (the Cloud Files API stores
      content in the file itself; partially hydrated files are sparse); eviction is
      dehydration; a small separate chunk cache holds chunks fetched for delta
      computation and peer sharing;
    - macOS: the system owns the local copies and evicts under storage pressure; the
      extension can evict explicitly; the chunk cache lives in the agent;
    - Linux: a chunk cache under `~/.cache/bigfatfile` plus a write-back directory for
      dirty files.
- Warm-up on first sign-in (no local history yet):
    1. pin policies per group, where the tenant has the `ops.devices` licensed module
       (folders that must always be offline for that group): pinned and prefetched at
       low priority;
    2. the user's own pins from another device, if any;
    3. the user's recent files from the Origin's access history, up to a budget (for
       example the last 200 files or 2 GB);
    4. nothing else.
- After that: siblings of files just opened, the next file in a sequence for media,
  frequently visited folders.
- Prefetch runs only when idle, on unmetered connections, within the budget, and yields
  to on-demand fetches.

## 6. Platform adapters

### Windows: Cloud Files API

- Support:
    - Windows 10 version 1709 and later; the client targets 1903 and later to use the
      whole API without feature checks;
    - Windows Server 2019 and later carry the same filter;
    - the sync root must be on NTFS.
- What is on disk:
    - a real folder (`%USERPROFILE%\BigFatFile`) registered as a sync root, which puts it
      in Explorer's navigation pane with our icon and status column;
    - each entry is a placeholder: a real file or directory with normal name, size,
      timestamps and attributes, whose data is absent (sparse) and which carries a
      reparse point tagged for the Windows Cloud Files filter;
    - the reparse point stores an identity blob; we store the node ID there, so the
      mapping survives renames and moves;
    - content states: dehydrated, partially hydrated, fully hydrated;
    - attributes: Pinned, Unpinned, and an in-sync bit that drives the Explorer icon;
    - everything persists while our process is not running.
- Our process:
    - a normal user-mode application connected to the sync root with a table of
      callbacks;
    - packaged as MSIX, which gives the identity that sync root registration and Explorer
      context-menu commands expect.
- Down-sync:
    - new remote entries: create placeholders with node ID, size, timestamps and version;
      nothing is downloaded; directories get on-demand population;
    - remote content change:
        - dehydrated placeholder: update size and version;
        - hydrated and unpinned: update and dehydrate in one call; the next open fetches
          fresh bytes;
        - pinned: dehydrate and re-hydrate in the background;
        - open by an application: the update fails with a sharing violation; mark
          not-in-sync and retry later;
    - rename, move, delete: ordinary Win32 operations on the placeholder; Windows raises
      the same notifications as for user actions, so the engine keeps a short-lived set
      of its own operations and ignores the matching notifications;
    - content on demand:
        - a read of a dehydrated range makes the filter call `FETCH_DATA` with offset and
          length;
        - the engine acknowledges at once, streams chunks in the background, returns bytes
          with `TRANSFER_DATA` in 4 KB-aligned pieces, and reports progress every few
          hundred milliseconds so Explorer shows its dialog;
        - partial hydration is enabled;
        - the callback names the calling process; hydration is declined for indexers and
          scanners that ignore the recall attribute;
        - pinned files are hydrated proactively; "Free up space" dehydrates.
- Up-sync:
    - renames and deletes: the filter calls `NOTIFY_RENAME` and `NOTIFY_DELETE` before the
      operation; the engine records the change in the outbox and lets it proceed;
      moving a file out of the sync root is a remote delete; moving one in is a new file;
    - content writes: no callback; a write to a hydrated placeholder is a plain NTFS
      write; two signals cover it:
        - Windows flips a modified placeholder to not-in-sync; enumerating that state finds
          modified files and is the startup catch-up;
        - the engine reads the NTFS USN change journal from the last recorded position,
          which catches changes made while the engine was stopped; if the journal has
          wrapped, a full scan compares size, timestamps and in-sync state against the
          local database;
    - files the user creates are ordinary files; after upload they are converted to
      placeholders and given an identity;
    - every successful upload ends with setting the in-sync state.
- Known difficulties:
    - hydration storms from antivirus, indexer, thumbnails and backup tools;
    - sharing violations on remote updates;
    - alignment rules in `TRANSFER_DATA`;
    - echo suppression of our own operations;
    - recovery of placeholder state after a crash;
    - keeping callbacks fast.
- References: Microsoft's CloudMirror sample (C++) for the callback flow; Nextcloud's
  Windows virtual-files plugin as a complete open-source implementation.

### macOS: File Provider

- The system owns the local copy under `~/Library/CloudStorage/`:
    - shows it in Finder; keeps its own item database, pending-upload queue and sync
      anchor; creates conflict copies; evicts under storage pressure;
    - our code is an extension that answers the system's calls; there is nothing to
      watch.
- Structure:
    - the extension must be Swift and runs sandboxed; a host application is required;
    - the Swift adapter is thin and talks to the .NET engine, running as a login-item
      agent, over the loopback API: fetch node X at version Y to this path; upload this
      path as node X with base version V; give me changes since anchor A;
    - the host application shows the settings UI in a `WKWebView` pointed at the agent;
    - fallback if the sandbox blocks the loopback route: a Swift port of Contracts and
      the transfer part of Client, checked against the shared test vectors.
- Down-sync:
    - the system calls `enumerateChanges(from: anchor)`; the anchor is our change-feed
      cursor, so this is a pass-through to the change log;
    - when the engine hears from the real-time hub it signals the enumerator and the
      system re-enumerates;
    - content: `fetchContents` or `fetchPartialContents`; the adapter downloads to a
      temporary URL and returns it; "Remove Download" evicts.
- Up-sync:
    - the system calls `createItem`, `modifyItem(baseVersion:)` and
      `deleteItem(baseVersion:)` after the user's write is complete and quiet, with the
      version the user edited from; that maps directly onto `If-Match`;
    - on a rejected base version the adapter returns the current remote item and the
      system keeps the user's version as a conflict copy.
- Distribution: Developer ID signing plus notarization; the App Store is not required.

### Linux: FUSE with a write-back cache

- A FUSE filesystem (libfuse3, bound directly from .NET with unmanaged function
  pointers) that emulates the materialised model:
    - metadata on demand, content from the chunk cache, writes into a local dirty copy,
      upload on close;
    - FUSE is a normal part of Linux desktops (`sshfs`, `rclone mount`, GNOME virtual
      filesystems); the user installs the `fuse3` package and nothing else;
    - mount point `~/BigFatFile`, run as a user service.
- Operations (path-based high-level API): `getattr`, `readdir`, `open`, `read`,
  `write`, `create`, `mkdir`, `unlink`, `rmdir`, `rename`, `truncate`, `flush`,
  `release`, `fsync`, `utimens`, `statfs`; no-ops for ownership and permissions (single
  user).
- Behaviour:
    - `readdir` and `getattr` come from the metadata cache, fetching a listing on first
      access; kernel attribute and entry caches are kept short so the change feed can
      invalidate our cache without low-level notifications; inode numbers come from a
      stable node-ID-to-inode map in SQLite;
    - `read` maps to chunks as in section 5; missing chunks are fetched and cached;
    - `write` goes to a dirty copy in the write-back directory; reads of a dirty file are
      served from that copy; `release` after a quiet period triggers chunking and upload;
      a remote change to a dirty file is a conflict; dirty files survive a crash and are
      uploaded on the next start;
    - `rename`, `unlink`, `mkdir`, `create` go to the outbox immediately and are applied
      to the cache;
    - extended attributes expose sync status per file for `bff status`; there are no file
      manager badges on Linux.
- Reference: rclone's VFS in full cache mode.

### Replica adapter

- No filesystem at all; reports `None` for capabilities.
- Applies the whole change feed without filtering.
- Metadata in its own SQLite database; chunks in a `local` warehouse on the host's
  disks.
- Never evicts: it is a full mirror and the on-premises backup.
- A verification job compares every manifest against the chunks present and re-fetches
  anything missing.

## 7. Side by side

|                                 | Windows                           | macOS                              | Linux                                       |
| ------------------------------- | --------------------------------- | ---------------------------------- | ------------------------------------------- |
| Model                           | materialised (Cloud Files API)    | materialised (File Provider)       | projection emulating materialisation (FUSE) |
| Who owns the local copy         | we do, on NTFS, with placeholders | the system                         | we do, in a cache directory                 |
| On-demand content               | yes, partial hydration            | yes, dataless files                | yes, chunk cache                            |
| Renames and deletes detected by | filter callbacks                  | system calls us                    | FUSE operations                             |
| Content writes detected by      | not-in-sync state and USN journal | system calls us, already debounced | `release` after a quiet period              |
| Catch-up after being stopped    | USN journal, else full scan       | the system's queue                 | dirty directory and outbox                  |
| Conflict copies made by         | engine                            | system                             | engine                                      |
| Status shown to the user        | Explorer icons and column         | Finder icons                       | `bff status`, extended attributes           |
| Language                        | C# engine and adapter             | Swift adapter over the C# engine   | C# engine and adapter                       |
