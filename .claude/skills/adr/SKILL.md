---
name: adr
description: Write or review an Architecture Decision Record in docs/adr. Use when an architectural or hard-to-reverse decision is settled (framework, provider, data model, CI or deploy process), when an earlier decision is replaced, or when reviewing an ADR draft.
---

# Architecture Decision Records

An ADR records one settled decision: the problem, the options weighed, the choice and its cost. It is written once and never rewritten; a changed decision gets a new ADR.

## Workflow

1. Read every ADR in `docs/adr/` to find the next number and any decision the new one touches or contradicts.
2. Ask the user for the context, the options they considered and the option they chose. The decision is the user's; never invent options, reasons or a choice.
3. Write the draft and wait for the user's approval of its content.
4. When it replaces an earlier ADR, change only that ADR's Status line (rule 15).
5. Run `./build.ps1 Check`.

## Scope

1. Write an ADR for a decision that shapes many parts of the system or is costly to reverse: architecture, frameworks, external providers, data model conventions, CI and deploy processes.
2. Do not write one for a decision that one pull request can undo, or for how-to or operational detail; that goes in `docs/*.md`, and the ADR links to it.
3. Suggest an ADR when a conversation settles such a decision, but write it only when the user asks.

## File and Title

4. Name the file `NNNN-<verb-phrase>.md`: the next free four-digit number and a lowercase, hyphenated phrase that states the decision (`0003-use-tbls-for-database-docs.md`).
5. Title it `# N. <Decision in Title Case>`, with the number without leading zeros. The title states the same decision as the file name and may use fuller words (`0003-use-tbls-for-database-docs.md` is `# 3. Use tbls for Database Documentation`).
6. Below the title, write `Date: YYYY-MM-DD`, the date the decision was accepted.

## Sections

7. Use exactly these sections, in this order: `## Status`, `## Context`, `## Considered Options`, `## Decision`, `## Consequences`.
8. Context states the problem, the forces and the constraints, and names no solution.
9. Considered Options lists at least two real options as bullets, worded neutrally and at the same level of detail, including the one that was chosen.
10. Decision opens with one sentence, `choco-api uses X.`, then bullets for the rules the decision implies, then one paragraph that says why each rejected option lost.
11. Consequences has two lists, `Easier:` and `Harder:`. Harder lists the real costs, each one concrete; it is never empty.

## Style

12. Write plain English sentences in present tense. Name no classes, methods or files from the code, except a doc the reader is pointed to; an ADR outlives the code.
13. Wrap lines at about 78 characters, as the existing ADRs do.

## Status and Change

14. Status is one of `Proposed`, `Accepted` or `Superseded by [N](NNNN-file.md)`. A new ADR is `Accepted` once the user has approved its content.
15. Never edit the content of an accepted ADR. To change a decision, write a new ADR whose Context names the one it replaces, and change only the old ADR's Status to `Superseded by` the new one.
