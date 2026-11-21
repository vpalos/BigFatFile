# Authentication

What changes from v1 in how people sign in to BigFatFile.

## 1. Account switching

- In v1, a browser has one account signed in. To use another account, the user signs out
  and signs in again.
- In v2, one browser can keep several accounts signed in at once.
- "Switch to another account" in the account menu:
    - lists the accounts that are signed in;
    - picking one makes it the active account, with no password asked;
    - "Add another account" opens the sign-in screen, and keeps the accounts that are
      already signed in.
- An account stays signed in until it signs out, or until its session ends.
