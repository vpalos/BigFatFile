# BigFatFile Architecture: Real-time sync

How clients learn that something changed, how the desktop client keeps its local view
in step with the server, and how conflicts are handled. Referenced from `Architecture.md`.

## 1. Notifications

- The change log is the authoritative truth (`Architecture.DataModel.md`, section 3);
- Clients connect to a SignalR hub and receive `ChangesAvailable(tenantId, latestSeq)`;
    - the push channel only says that something changed;
    - the payload carries no data.
- A client reads the log from its cursor on certain events:
    - when it receives that message;
    - after a reconnect;
    - after waking from sleep;
    - on a timer.
- Change storage/detection:
    - for one server process, committed changes go on an in-process bus;
    - in cluster mode each node listens on a PostgreSQL `NOTIFY` channel, so a change
      committed on one node reaches clients connected to another.

## 2. Desktop client loop

1. Sync changes from Origin or Replica:
    - read the change log from the stored cursor until empty;
    - apply entries that touch directories the client has materialised;
    - discard all other entries (not affecting the client);
    - persist the cursor after each page.
2. Listen for available changes:
    - connect to the SignalR hub;
    - on each change notification, repeat step 1 (debounced).
3. Sync local changes:
    - receive local changes from the platform adapter;
    - wait until a changed file is closed and quiet;
    - chunk the file;
    - upload only the missing chunks;
    - complete the upload with `If-Match` over the manifest;
    - mark the file as being "in sync".

## 3. Conflicts

- No merges and no silent overwrites.
- A write rejected because the version moved on becomes a conflict copy:
    - named `<name> (conflict copy, <device>).<ext>`, next to the original;
    - both appear everywhere;
    - the user resolves afterwards from Explorer or the web UI.
- A remote delete of a file with un-synced local edits keeps the local file as a
  conflict copy.
- Edits by the same client are recognised by the actor's client ID and are not conflicts.
- Later feature:
    - "someone else is editing this file" notifications sent over the same SignalR hub.
