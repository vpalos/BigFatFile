# BigFatFile

## Working rules

- **Plain language, always.** Applies to everything written for the founder or left in the
  repos: chat answers, docs, code comments, commit messages, site copy. Write the way a
  teacher or a public speaker does: aim at everyone in the room, not at the one person who
  already knows the subject. The test for a sentence is whether it can be read more than one
  way. If it can, it is wrong; rewrite it.
    - Say the point first, then explain it, then give the detail. Never make the reader hold a
      whole paragraph in their head to find out what is being claimed.
    - Assume the reader does not know what you know. State the context before the conclusion.
    - Explain a term the first time it is used, in one short clause. No insider shorthand left
      undefined.
    - No metaphors or figures of speech for technical things. Write "a counter limits how many
      requests run at once", never "own the wire", "buys a guarantee", "the payoff".
    - Concrete nouns and real numbers: "six requests at a time", not "capacity" or "pressure".
    - Short sentences. One idea each. Everyday words.
    - No build-up and no drum rolls. Drop "here is where it gets interesting", "the elegant
      part is", "and that is the whole point".
    - Never dress a limitation as a virtue, and never restate a point in bigger words to make
      it land harder.
    - Explaining something in simpler terms means simpler words and shorter sentences, not a
      new analogy.
- **No large files.** Split code by logical component — one concern per file, across the
  whole codebase, not just Svelte (where ~400 lines is a smell and 600 forces a split).
  Code is written for humans first: prefer the structure a reader follows over the one a
  writer found convenient.
- **Code comments**: no comment is the default. Write one only when a reader would act
  wrongly without it. When you do, use simple, clear language, why not what. Short
  sentences, one idea each, everyday words; never dense prose or packed clauses. One
  short line is the normal size; genuinely non-obvious rationale (concurrency, security,
  browser quirks, billing semantics) may take up to three. Never: anything a reader can
  look up (what a setting or API does), history ("used to", "this replaces", tombstones),
  changelog dates (operational cutoffs the code still acts on are the one exception),
  roadmap phase numbers, restating the next line, `@param` lines that echo the name,
  jokes, anthropomorphism.
- **Mechanisms are explained in docs, not comments.** How a subsystem works as a whole
  (its layers, state machine, flows, corners to watch) lives in a readable doc under
  `bff-ops/docs/architecture/` in bullets and clear lines (`TRANSFER-ENGINE.md` is the
  model).
  Comments only add what is local to one spot; a comment growing past three lines means
  the doc is missing something. Update the doc when the mechanism changes.
- **Docs are current-state only.** Active docs describe the present and the decisions
  in force; when something changes, rewrite in place and move superseded material to
  `bff-ops/archive/`. A doc describing something not built yet belongs in
  `bff-ops/docs/research/`, never in `architecture/`. No override notes, no amendment parentheticals, no decision
  archaeology. Dates appear only as operational data (baselines, cutoffs, last-run
  records). Small declined ideas stay in chat; ROADMAP records big decisions.
- **Documentation**:
    - **Merge, don't restate.** A subject is described in one place. When a subject
      already has a summary somewhere (the Replica entry under Projects, the Jobs bullet
      under the Origin), new detail goes there and replaces the summary. Never add a
      second section elsewhere that says the same thing shorter. A reference to the one
      place is a link, not a recap.
    - **One idea per bullet.** A bullet that lists several things ("a, b, c and d") or
      joins two clauses with a semicolon becomes a parent bullet with one sub-bullet per
      item. The structure of the thing is the structure of the list: an attribute and
      its values become a bullet and its sub-bullets.
    - **Section titles are plain nouns.** "Repository structure", "Binaries",
      "Bootstrapping". Not "Layout", "Executables", "Foundations", and no verbs or
      sentences in a title.
    - **Top-level sections are numbered; subheadings are not.** `## 2. Projects` then
      `### Binaries`. Subheadings move too often for numbers to stay right. A reference
      names the file and the section number, or the subheading by name.
    - **First person is fine for a decision the founder made.** "I avoid hidden
      behaviours (like EF Core generators)" states who decided and that it is a choice,
      which an impersonal sentence hides.
- **Settings keys are coupled to the deployed bundle**: remove a key only after the
  code reading it is gone from production (rolling deploys run old bundles briefly).
  A missing settings section crashes the app at boot.
- **A migration's version number belongs to production, not to the working tree.** Never
  reuse one, even for a migration deleted before you thought it shipped — production
  consumed the number the moment it ran, and anything later carrying it counts as applied
  and is skipped in silence. A recorded version is not evidence the work happened: judge a
  run by querying the data. Before deploying one, check the control document is not locked
  (PLAYBOOKS → "Unwedge the migration lock") and rehearse against a restored production
  copy (→ "Dry-run a migration against production data").
- **i18n strings must not use single-quoted HTML attributes** — ICU eats one apostrophe
  per message and breaks the attribute.
- **Site copy**: plain sentences, no em-dashes (match each file's existing dash
  character), no defensive parentheticals.
- **No co-authoring references anywhere**: not in code, docs, or Git commit messages (no
  `Co-Authored-By` trailers, no "generated with" lines).
- Never publish claims stronger than what's built (availability numbers, privacy
  wording); the published pledges (never sell, never train AI, no ads) are commitments,
  not copy. That files go browser to storage without passing through our servers is
  described as a design choice only, never promised and never put in the Terms.
