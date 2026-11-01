---
name: github-issue
description: Write, review, link or close a GitHub Issue on trduc2712/choco-api. Use when pending work, a bug or a known gap should be tracked, when starting a branch for an issue, or when closing one.
---

# GitHub Issues

Pending work on choco-api is tracked as GitHub Issues on `trduc2712/choco-api`. An issue states a problem and when it is solved; it is not a design document.

## Workflow

1. Read the issue form for the type in `.github/ISSUE_TEMPLATE/` and list open issues with `gh issue list` to avoid a duplicate.
2. Draft the title, body and labels, and wait for the user's approval.
3. Write the body to a file in the scratchpad and run `gh issue create --repo trduc2712/choco-api --title "..." --body-file <file> --label "..."`. Give the user the issue link.

## Running gh

1. Run `gh` from PowerShell after reloading `PATH`: `$env:Path = [Environment]::GetEnvironmentVariable('Path','Machine') + ';' + [Environment]::GetEnvironmentVariable('Path','User')`. It is not on the Bash tool's `PATH`.
2. Create, edit, comment on or close an issue only after the user approves that action and its text.
3. Suggest an issue when work is deferred or a gap is found, but create it only when the user asks.

## Type

4. Pick the form whose type fits: `feature` is new behavior for API clients; `chore` is internal work with no new behavior for clients (base, tooling, CI, docs); `bug` is behavior that differs from what the API promises.
5. The forms in `.github/ISSUE_TEMPLATE/` are the source of truth for the sections. `gh issue create` does not use them, so build the body from the form being read, not from memory.

## Title

6. Write the title in English, short, with no prefix such as `feat:` or `[Bug]`.
7. Start a feature or chore title with a verb (`Add a health check endpoint`). Write a bug title as the symptom and when it happens.

## Body

8. Use one `## <Label>` heading per form field, with the form's label as is, in the form's order. Leave out an optional field that would be empty.
9. Context states what is missing or wrong today and why it matters, and names no solution.
10. Done When is a `- [ ]` checklist, one checkable condition per line, ending with `` - [ ] `./build.ps1 Check` passes `` for any change to the repository.
11. Notes holds decisions still open, decisions already made and links. Put a design question here as a question, not as a decision.
12. A bug's Steps to Reproduce gives requests to send or the failing test; Actual quotes the status code, ProblemDetails body, exception or log lines.

## Labels

13. Give every issue exactly one `type:` label, which the type's form names, and at most one `area:` label. Pick from `gh label list`; ask the user before creating a label.
14. Do not use priority labels, milestones or projects.

## Branches and Closing

15. Link a branch to an issue with `gh issue develop <n> --repo trduc2712/choco-api --name <type>/<kebab-description> --checkout`, where `<type>` is a Conventional Commit type. A number in the branch name does not link it, so leave it out.
16. Close an issue with `Fixes #n` in the commit or pull request that resolves it. Close dropped work with `gh issue close <n> --reason "not planned"` and a comment that says why.
