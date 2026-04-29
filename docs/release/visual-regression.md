# Visual Regression

## Capture

```powershell
scripts/visual-regression.ps1
```

Save screenshots into:

```text
artifacts/visual-regression/current/
```

Promote accepted captures:

```powershell
scripts/visual-regression.ps1 -PromoteCurrent
```

## Compare

```powershell
scripts/visual-compare.ps1
```

Outputs:

- `artifacts/visual-regression/diff/*.png`
- `artifacts/visual-regression/diff/visual-compare.json`

The compare gate fails when changed pixels exceed the configured ratio.
