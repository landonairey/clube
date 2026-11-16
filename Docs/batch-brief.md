# Standing brief for a batch session

Every batch session reads this once, then its own short task. The driver's dispatch prompt (from `Docs/handoff-20261116.md`) only adds: the batch name, GL IDs, the new branch name, and batch-specific notes.

## Before starting
- Read `CLAUDE.md`. Grep the objectives doc for your GL IDs and read those entries and their section's intro rules: they hold the user's exact words. Grep `Docs/history.md` for class names instead of reading it; read `Docs/architecture.md` only for the layers you touch.
- Git: `git fetch`, check out `main`, `git pull`, cut your branch from it. The batch before yours has merged; branches are no longer stacked.
- The user may be in the Unity Editor: check for Play mode and unsaved scene changes before any Editor action; wait and re-check rather than force; never discard their changes.

## While working
- Connect to the systems that exist; new kinds of things are data rows or definitions, not new switch statements; one responsibility per class; Core logic gets edit-mode tests; `Clube.Game` never references `Clube.Debug`.
- The user is away: choose sensible defaults and list them in the PR.
- Tokens: grep before reading, read slices, edit with the Edit tool, tiny Unity MCP scripts, at most one picture per visual item, the batch-mode suite once at the end (reusable clone: `E:/Repos/Unity3D/q2clone`). Do not spawn agents.
- Commit and push after each GL item, so a usage-limit stop loses nothing.
- Stuck: if a problem survives two real attempts, or you know a result is wrong and can't fix it, stop, push what works, and start your final message with `NEEDS FABLE:` and the reason. Don't guess past it.

## When done
- Tick your GL items with a short *(Done …)* note naming classes; one short paragraph at the end of `Docs/history.md`; update `Docs/architecture.md` where it changed.
- Run the full edit-mode suite in batch mode and wait for it. Push. Open a PR to `main`. The PR body carries the full report: what was built, defaults chosen, what was seen in Play mode, what is unverified or left. Commit messages end with `Co-Authored-By: <your model, e.g. Claude Sonnet 5.5> <noreply@anthropic.com>`; the PR body with `🤖 Generated with [Claude Code](https://claude.com/claude-code)`.
- In `Docs/handoff-20261116.md` (on your branch, before the PR): tick your batch in the queue with its PR number and a one-line result.
- Leave the checkout on your branch, clean, the Editor out of Play mode, no dev builds left.
- Final message, at most 12 lines: PR link, test counts (passed/failed), decisions the user must make, what is unverified, absolute paths of pictures worth showing. Everything else lives in the PR.
