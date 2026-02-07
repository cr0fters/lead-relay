# AGENTS.md

## Purpose
This file defines the default engineering standards for all contributors (human and AI agents) in this repository.

## Scope
- Applies to all new code.
- Applies to modifications of existing code where practical.

## Core Standards
- Write tests for all new behavior.
- Keep code aligned with SOLID principles.
- Prefer simple, maintainable solutions (KISS, YAGNI).
- Avoid duplication (DRY).
- Keep boundaries clear between Domain, Application, Infrastructure, and Web layers.

## Testing Requirements
- Every new feature or behavior change must include automated tests.
- Bug fixes must include a regression test.
- Tests should validate observable behavior, not private implementation details.
- Maintain or improve coverage in changed areas.
- Use:
  - `tests/LeadRelay.UnitTests` for unit-level logic.
  - `tests/LeadRelay.IntegrationTests` for cross-layer/data-access behavior.

## SOLID Guidance (Practical)
- `S`: One reason to change per class/module.
- `O`: Extend with new implementations before modifying stable logic.
- `L`: Preserve behavioral contracts when substituting implementations.
- `I`: Keep interfaces focused and minimal.
- `D`: Depend on abstractions in Application/Domain-facing logic.

## Code Quality Rules
- Favor small, composable methods and classes.
- Name things by domain intent, not technical shortcuts.
- Use explicit error handling and meaningful logging at boundaries.
- Do not introduce dead code, commented-out blocks, or speculative abstractions.
- Preserve backward compatibility for public endpoints/contracts unless explicitly changing them.

## Security and Configuration
- Never hardcode secrets or tokens.
- Use environment/configuration for sensitive values.
- Validate and sanitize external inputs at system boundaries.

## Pull Request / Change Checklist
- [ ] New/changed behavior has tests.
- [ ] Existing tests pass.
- [ ] Code follows SOLID and keeps layer boundaries clean.
- [ ] No duplicated logic introduced without clear reason.
- [ ] Documentation updated when behavior/config changes.

## Working Agreement for Agents
- If a requested change cannot reasonably include tests, explain why and propose the smallest acceptable follow-up.
- Prefer incremental, reviewable changes over broad rewrites.
