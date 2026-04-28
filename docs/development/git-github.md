# Git/GitHub Workflow

AriaEngine の変更確認と GitHub 連携で使う最小手順です。

## 変更確認

```powershell
.\scripts\git-report.ps1
```

表示する内容:

- 現在のブランチ
- 未コミット差分
- 直近コミット
- 変更ファイル一覧
- GitHub リモート

## 検証

```powershell
.\scripts\smoke.ps1
dotnet test src\AriaEngine.Tests\AriaEngine.Tests.csproj -c Release
```

`dotnet` が `project.assets.json` や workload 情報で失敗する場合は、コード修正前に復元環境を直してください。

```powershell
dotnet restore src\AriaEngine\AriaEngine.csproj /p:NuGetAudit=false
dotnet restore src\AriaEngine.Tests\AriaEngine.Tests.csproj /p:NuGetAudit=false
```

## ブランチとPR

```powershell
git switch -c codex/fix-parser-expression-safety
git add src docs scripts .github
git commit -m "fix: harden parser and expression edge cases"
git push -u origin codex/fix-parser-expression-safety
```

PR では次を必ず書きます。

- 変更内容
- 修正したバグ
- 実行した検証コマンド
- 残っている環境依存の問題

## 今回の回帰ポイント

- 式パーサは末尾トークンを残した式を受け入れない
- 文字列比較は数値変換ではなく文字列値で比較する
- 配列の負インデックスは配列生成前に拒否する
- `while` の閉じ忘れは未解決ジャンプではなく構文エラーにする
- ローカル文字列補間は通常の `$name` 参照と同じスコープを見る
