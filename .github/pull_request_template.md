## Summary
-

## Why
-

## Checklist
### Change Type (choose one)
- [ ] Docs/config only. (docs, policies, CI/workflow, scripts, repo config only; no app behavior/spec change)
- [ ] Includes product code or spec changes. (e.g. `src/**`, behavior-affecting tests, `docs/dev/Specification.md`, `Spec` attributes)

### Quality (as applicable)
- [ ] Added/updated tests in the appropriate layer (Unit / Integration / UI), or documented why tests are not needed.
- [ ] `./scripts/run-tests.ps1 -Configuration Release` succeeded locally, or documented why it is not needed.
- [ ] If `Specification.md` or `Spec` attributes changed: `./scripts/check-spec-coverage.ps1` succeeded locally.

### Related Issue (choose one)
- [ ] No related issue.
- [ ] Linked related issue.

### Specs (choose one)
- [ ] No spec impact.
- [ ] Updated specs/docs for behavioral changes.

### Changelog (choose one)
- [ ] No user-facing change (Changelog N/A).
- [ ] Updated `CHANGELOG.md` (`[Unreleased]`) for user-facing changes.
