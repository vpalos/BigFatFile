# Editable sends

- Status: Accepted
- Recorded: 2026-11-09

## 1. Context

- A send (also called a transfer) delivers files to people outside the system, through a
  download link.
- In v1, the sender can edit a sent transfer while its link works:
    - the files, with the same tools as a drive, including folders and earlier versions;
    - the title, the message and the recipients;
    - the expiry and the password.
- In v1, the sender can keep the files of a send after its link expires. Kept files count
  as storage until the sender removes them.

## 2. Decision

- The sender can edit every part of a send after it is created:
    - the files: add, remove and replace;
    - the title;
    - the message;
    - the recipients;
    - the expiry;
    - the password.
- The download link stays the same after an edit.
- For each edit, the sender chooses whether the recipients get an email about it.
- An expired send always moves to the sender's Trash. The sender can reactivate it from
  there with a new expiry, until retention removes it.

## 3. Alternatives

- A send that cannot change:
    - every fix needs a new send and a new link.
- Edits to the title, message, expiry and password only:
    - a wrong or a missing file still needs a new send.
- Deleting the files at expiry:
    - nothing is left to reactivate.
- Keeping an expired send for a set number of days of its own:
    - it adds a second rule for how long deleted content is kept, next to the Trash.
- Keeping an expired send until the sender deletes it, as v1 does for a kept send:
    - it keeps using storage until the sender acts.

## 4. Consequences

- The files of a send stay whole objects. An edit adds, removes or replaces whole files,
  so it needs no chunks and no delta.
- A send keeps no versions. Garbage collection deletes the object of a removed or a
  replaced file.
- A file that is added by an edit is scanned, like every other upload.
- Removing a recipient stops the emails to that person. The link still works for them,
  because the link belongs to the send, not to one recipient.
- Details:
    - [Sends](../sends.md);
    - [File transfer](../../architecture/files/transfer.md), section 3.
