# Sends

A send (also called a transfer) delivers files to people outside the system, through one
download link.

## 1. Editing

- After a send is created, the sender can edit:
    - its files: add, remove and replace;
    - its title;
    - its message;
    - its recipients;
    - its expiry;
    - its password.
- The download link stays the same after an edit.
- For each edit, the sender chooses whether the recipients get an email about it.
- Removing a recipient only takes them off the email list. The link still works for them.
- Reasons: [Editable sends](./decisions/001-editable-sends.md).

## 2. Expiry

- At expiry, the link stops working and the send moves to the sender's Trash.
- Before expiry, the sender can extend the send by changing its expiry.
- From the Trash, the sender can reactivate the send with a new expiry. The same link
  works again.
- The send is deleted when retention removes it from the Trash. The tenant's plan sets
  how long that is.

## 3. Send again

- "Send again to..." on an earlier send starts a new send with the same recipients.
- It copies only the recipients.
