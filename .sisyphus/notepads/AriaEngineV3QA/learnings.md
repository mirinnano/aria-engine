QA Learnings:
- Integration test script for AriaEngine v3.0 created at .tmp/tests/integration_v3.aria to exercise v3.0 features (scope, local, readonly, match/case/default, endmatch, end_scope).
- CLI verification executed: aria-lint reported 0 errors with 2 warnings related to non-exhaustive match; aria-format normalized formatting; aria-doc generated doc.json and doc.md in .tmp/docs.
- Edge-case fixtures created: edge_empty.aria, edge_comments.aria, edge_nested_scope.aria for basic parsing and scope nesting validations. Edge nesting reports warnings for unused variables but no errors.
- Save/Load compatibility (ARIA SAVE3) could not be exercised in this build; CLI reported SCRIPT_LOAD failure for aria-save and assets/main script path resolution; not currently supported here.
- Next steps: validate ARIASAVE3 support in official release or a later build; consider adding a dedicated integration test that triggers a full save/load lifecycle if the CLI exposes such capability.
