Q&A/Blockers:
- ARIA SAVE3/Load: Save/load compatibility could not be exercised. The CLI reported that aria-save script/module could not be loaded; likely this feature is not wired in this build or requires additional assets/script path conventions.
- Edge tests surfaced warnings (e.g., unused variables in nested scopes, non-exhaustive match warnings) but no failures; consider addressing warnings for stricter CI.
- The integration test relies on v3.0 feature syntax; ensure regression tests cover any future language changes that might affect the parser.
