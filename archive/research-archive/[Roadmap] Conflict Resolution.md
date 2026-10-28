# BigFatFile: Conflict Resolution

Version: 2025-07-27
Author: Valeriu Paloș <vp@gcas.ro>
Status: Planning / Roadmap

> [!IMPORTANT]
> This paper was generated with the help of AI based on my own technical research.
> It is at the very least work-in-progress and designed to set the "vision" of where
> the BFF architecture intends to evolve towards.

## 1. Guiding Principles

This document outlines the technical strategy for handling file conflicts that arise from concurrent or offline edits. Our approach is guided by two core principles:

1. **Data Safety:** Never lose user data. In any conflict scenario, the system must default to preserving all changes.
2. **User Control:** Whenever possible, empower the user with the context and tools to make the final decision on how to resolve a conflict.

## 2. The Two-Layer Resolution Approach

Our strategy is a two-layer process designed to handle conflicts gracefully, managed by the BFF agent utility.

### Layer 1: User-Driven Resolution (The Preferred Path)

When the BFF agent detects a conflict (e.g., both the local and remote versions of a file have changed since the last sync), it will first attempt to engage the user.

- **Action:** The agent's UI will present a clear notification to the user, indicating that a conflict has occurred.
- **User Options:** The user will be presented with a simple, explicit choice:
  1. **Keep My Version:** Overwrite the remote version with the local changes.
  2. **Keep Remote Version:** Discard local changes and download the remote version.
  3. **Keep Both:** Preserve the remote version and save the local changes as a new, explicitly named "conflicted copy" (e.g., report (Conflicted Copy).docx).

### Layer 2: Automatic Preservation (The Safe Fallback)

If user intervention is not possible (e.g., the user is offline, or the UI is not active), the agent will default to the safest automatic action.

- **Action:** The agent will automatically execute the "Keep Both" strategy. It will download the remote version and rename the user's local version to a "conflicted copy."
- **Result:** This ensures no data is ever lost. The user will see both files in their drive and can manually merge the changes at their convenience.

## 3. Future Roadmap: Conflict Prevention

Beyond resolving conflicts after they happen, our long-term roadmap includes features to prevent them from occurring in the first place.

- **Real-Time "File in Use" Notifications:**
  - **Description:** Implement a real-time communication layer (e.g., WebSockets) between the backend and active BFF agents.
  - **User Experience:** When a user opens a file that is already being edited by another team member, they will receive an immediate, non-intrusive notification (e.g., "Jane Doe is also editing this file"). This social context significantly reduces the likelihood of conflicting edits on shared documents.
