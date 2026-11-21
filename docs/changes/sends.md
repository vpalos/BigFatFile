# Sends

What changes from v1 in sends. A send (also called a transfer) delivers files to people
outside the system, through one download link.

## 1. File edits

- In v1, the sender edits the files of a sent transfer with the same tools as a drive:
    - upload, delete, rename, move and copy files;
    - make new folders;
    - restore a deleted file from the Trash;
    - revert a file to an earlier version.
- In v2, an edit adds, removes or replaces whole files.
- A send keeps no versions, so no file can go back to an earlier version.
- Reasons: [Editable sends](./decisions/001-editable-sends.md).

## 2. Update emails

- In v1, an edit sends no email. The sender presses "Notify recipients" to email them
  about the changes.
- In v2, the sender chooses whether the recipients get an email about each edit.

## 3. Expiry

- In v1, the sender can keep the files of a send after its link expires.
    - Keeping is on by default.
    - Kept files count as storage until the sender removes them.
- In v2, an expired send always moves to the sender's Trash.
    - From the Trash, the sender can reactivate the send with a new expiry. The same link
      works again.
    - The send is deleted when retention removes it from the Trash. The tenant's plan sets
      how long that is.
- Reasons: [Editable sends](./decisions/001-editable-sends.md).

## 4. Send again

- "Send again to..." is new in v2.
- On an earlier send, it starts a new send with the same recipients.
- It copies only the recipients.
