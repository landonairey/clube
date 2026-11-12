# Standing brief for a batch session

Every batch session or agent reads this once, then its own short task. The planning chat's dispatch prompt only adds: the batch letter, GL IDs, the branch to cut from, the new branch name, and batch-specific notes.

## Before starting
- Read `CLAUDE.md`. Grep the objectives doc for your GL IDs and read those entries and their section's intro rules: they hold the user's exact words. Grep `Docs/history.md` for class names instead of reading it; read `Docs/architecture.md` only for the layers you touch.
- Git: `git fetch`, cut your branch from the one named in your task, merge `origin/main` (docs only; on a conflict in the objectives doc keep the branch's Done notes and add main's new lines).
- The user may be in the Unity Editor: check for Play mode and unsaved scene changes before any Editor action; wait and re-check rather than force; never discard their changes.

## While working
- Connect to the systems that exist; new kinds of things are data rows or definitions, not new switch statements; one responsibility per class; Core logic gets edit-mode tests; `Clube.Game` never references `Clube.Debug`.
- The user is away: choose sensible defaults and list them in the PR.
- Tokens: grep before reading, read slices, edit with the Edit tool, tiny Unity MCP scripts, at most one picture per visual item, the batch-mode suite once at the end (reusable clone: `E:/Repos/Unity3D/q2clone`). Do not spawn agents.
- Commit and push after each GL item, so a usage-limit stop loses nothing.

## When done
- Tick your GL items with a short *(Done …)* note naming classes; one short paragraph at the end of `Docs/history.md`; update `Docs/architecture.md` where it changed.
- Run the full edit-mode suite in batch mode and wait for it. Push. Open a PR to `main` (say that it includes the earlier stacked batches until they merge). The PR body carries the full report: what was built, defaults chosen, what was seen in Play mode, what is unverified or left. Commit messages end with `Co-Authored-By: Claude Fable 5.2 <noreply@anthropic.com>`; the PR body with `🤖 Generated with [Claude Code](https://claude.com/claude-code)`.
- Leave the checkout on your branch, clean, the Editor out of Play mode, no dev builds left.
- In `Docs/planning-handoff.md` (on your branch): move your batch to "Done" with its PR number and set "Chain tip" to your branch.
- Final message, at most 12 lines: PR link, test counts, decisions the user must make, what is unverified, absolute paths of pictures worth showing. Everything else lives in the PR.
