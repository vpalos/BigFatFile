# One UI stack

- Status: Accepted
- Recorded: 2026-11-08

## 1. Context

- Every client needs a configuration UI.
- Separate UI codebases per platform would multiply the design, component and
  translation work.

## 2. Decision

- All UIs are Svelte 5 apps that share design, components and translations:
    - Tailwind CSS, shadcn-svelte and Paraglide.
- The desktop client shows its UI in a Photino.NET window that hosts the platform's
  webview.

## 3. Alternatives

- Avalonia:
    - it would tie the desktop UI to a second UI stack.
- NeutralinoJS as the desktop window:
    - considered and dropped.

## 4. Consequences

- The web app and the desktop windows reuse the same components.
- Details: [Platform topology](../architecture/platform/topology.md), section 2.
