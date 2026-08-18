# Autonomous MVP workflow

## Purpose

This file defines how Codex should make useful, bounded progress through the LeadRelay MVP backlog when asked to `Continue the MVP.` It complements `AGENTS.md`; it does not replace its engineering, security, verification, review, or authorization rules. The phrase also explicitly authorizes Codex to commit and push each completed, reviewed milestone directly to `main` as described below.

`TASKS.md` describes what remains to be done. This file describes how to choose and complete an appropriate portion of that work without running uncontrolled across the roadmap.

## Start every autonomous run

1. Read `AGENTS.md` and `TASKS.md` in full before selecting work.
2. Inspect the working tree and preserve any existing user changes.
3. Inspect the code, tests, migrations, configuration, and directly relevant documentation for candidate work. Confirm what is already implemented rather than relying only on checkbox state.
4. Review the MVP exit criteria and identify the highest-value unmet dependency that can be advanced safely in the current repository and environment.

## Select a coherent milestone

- Prioritize Priority 0 work and prerequisites that unblock the MVP exit criteria. Use Priority 1 or Priority 2 work only when the user asks for it or it is a necessary prerequisite to safe Priority 0 delivery.
- Respect dependencies between product decisions, provider setup, data models, endpoints, access policies, user journeys, and tests.
- Do not process `TASKS.md` mechanically from top to bottom. Group related backlog items or sub-items into a milestone with one clear outcome and a reviewable change set.
- Prefer a vertical slice that leaves complete, usable behavior over several disconnected partial implementations.
- Choose the next milestone that can be completed safely with the decisions, credentials, services, and permissions already available.
- Before editing, state the milestone selected, why it is the appropriate next step, and any nearby items intentionally excluded.

A suitable milestone normally:

- advances one customer journey, platform capability, or launch-safety outcome;
- includes the necessary data, server behavior, UI, recovery states, documentation, and tests for that outcome where applicable;
- can be reviewed as one cohesive change; and
- does not require speculative architecture for distant roadmap work.

## Handle human actions and blockers

Do not invent product, commercial, legal, compliance, or architectural decisions merely to continue working.

When an item requires a product or business decision, external account configuration, credentials or secrets, legal review, manual third-party setup, live-provider mutation, or another human action:

1. Treat it as blocked for implementation unless an established decision or safe local substitute already exists.
2. Record it clearly in the run report as a blocker or required human action, including why it matters and the smallest decision or action needed.
3. Keep the item incomplete in `TASKS.md`. Add a concise clarifying note there only when it improves the roadmap's accuracy; do not remove the item or its human context.
4. Continue with other work in the same milestone when it remains useful and safe without creating a misleading or unusable partial feature.
5. If the blocker prevents a coherent outcome, stop rather than building placeholders or pretending the dependency is complete.

## Execute the milestone

For implementation work:

1. Trace the complete affected journey and inspect the existing implementation before modifying it.
2. Follow the repository's architecture, conventions, security boundaries, tenant model, and public contracts.
3. Implement complete behavior rather than placeholders, dead routes, mock production integrations, or UI that claims unavailable functionality.
4. Include appropriate validation, authorization, tenant isolation, failure handling, recovery UX, configuration, migrations, observability, and documentation where relevant.
5. Add or update automated tests for the new behavior, including important negative and failure cases.
6. Run the relevant tests, build, lint, formatting, migration, and static checks that are safe and permitted by `AGENTS.md` and the current environment. Respect any explicit local verification constraint; use CI as the authority where required and report checks that could not be run.
7. Fix failures caused by the changes and perform the full iterative code review required by `AGENTS.md`.
8. Mark a `TASKS.md` item complete only when its described outcome is genuinely implemented and the required verification exists. Do not mark external or human work complete based solely on local code.
9. Continue onto closely related backlog work only while it remains part of the same coherent milestone and stays within the stopping rules below.

`Continue the MVP.` authorizes commits and non-force pushes directly to `main` for milestones completed under this workflow. It does not authorize deployments, live-service changes, external communications, provider-account mutations, purchases, credential changes, or other external side effects.

## Publish and continue

After a milestone passes the required review and permitted local checks:

1. Confirm that the working tree contains only the milestone's intended changes and that local `main` is based on the current `origin/main`.
2. Commit the coherent milestone with a terse, accurate message and push it directly to `main`. Never force-push or rewrite shared history.
3. Monitor the resulting GitHub Actions run. If CI fails because of the milestone, diagnose, fix, review, commit, and push the correction before selecting more work. If CI cannot be inspected, report that limitation and do not claim it passed.
4. Add newly identified product decisions, credentials, legal review, provider setup, and other human actions to a cumulative run list. Keep their `TASKS.md` items incomplete.
5. Re-read the relevant backlog and choose the next safe coherent milestone. Continue the loop while useful progress remains possible and the stopping rules are not met.

Keep commits milestone-sized even when one autonomous run completes several milestones. Do not combine unrelated product areas into one commit merely because they were handled in the same run.

## Stop the autonomous run

Stop and hand control back to the user when any of the following is true:

- a genuine blocker requires user input or human action before the milestone can remain coherent;
- continuing requires an important product, business, legal, provider, or architectural decision that is not already established;
- continuing would make the change set unnecessarily large, risky, or difficult to review; or
- no further safe implementation work materially advances the MVP exit criteria.

Completing one milestone is a checkpoint, not normally the end of an autonomous run. Publish it, verify CI where possible, then select the next safe milestone. The workflow remains bounded: stop before the accumulated run becomes difficult to supervise or review, even though each milestone has its own commit.

## End-of-run report

Report:

- the milestone selected and why;
- what was completed;
- important implementation decisions made and the established requirements they were based on;
- tests and checks run, their results, and anything deferred to CI;
- the exact `TASKS.md` items marked complete, if any;
- a consolidated checklist of remaining blockers, risks, decisions, credentials, provider setup, legal review, and other human actions required;
- the recommended next coherent milestone; and
- whether changes are uncommitted, committed, pushed, or otherwise published.

Be explicit about partial results and unavailable verification. Do not describe the MVP, a milestone, or a backlog item as complete when known required work remains.
