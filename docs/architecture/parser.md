# パーサー

このドキュメントでは、AriaEngineのパーサー（Parser）について詳しく説明します。

## パーサーの概要

パーサーは、.ariaスクリプトファイルを読み込み、仮想マシンが実行可能な命令リストに変換します。NScripter互換の構文をサポートし、インライン構文の展開も行います。

### 主な役割

- スクリプトファイルの読み込み
- トークン化（Tokenization）
- 命令の生成
- ラベルの解析と解決
- 構文エラーの検出
- インライン構文の展開

## パース処理のフロー

### 全体フロー

```
1. ファイル読み込み
   ↓
2. 行単位の分割
   ↓
3. コメント除去
   ↓
4. トークン化
   ↓
5. 命令生成
   ↓
6. ラベル解決
   ↓
7. 命令リストの出力
```

## トークン化

### トークンの種類

パーサーは以下のトークンを認識します：

1. **コマンド**: `text`, `wait`, `lsp` など
2. **ラベル**: `*label_name`
3. **文字列リテラル**: `"テキスト"`
4. **数値リテラル**: `123`, `45.67`
5. **変数**: `%0`, `$name`
6. **演算子**: `+`, `-`, `*`, `/`, `==`, `!=`
7. **キーワード**: `if`, `else`, `for`, `to`, `next`
8. **コメント**: `; これはコメント`

### トークン化の実装

```csharp
private List<string> Tokenize(string line)
{
    var tokens = new List<string>();
    var currentToken = new StringBuilder();
    bool inString = false;

    for (int i = 0; i < line.Length; i++)
    {
        char c = line[i];

        if (c == '"' && (i == 0 || line[i - 1] != '\\'))
        {
            inString = !inString;
            currentToken.Append(c);
        }
        else if (inString)
        {
            currentToken.Append(c);
        }
        else if (char.IsWhiteSpace(c))
        {
            if (currentToken.Length > 0)
            {
                tokens.Add(currentToken.ToString());
                currentToken.Clear();
            }
        }
        else if (c == ',')
        {
            if (currentToken.Length > 0)
            {
                tokens.Add(currentToken.ToString());
                currentToken.Clear();
            }
            // カンマはトークンとして追加しない
        }
        else
        {
            currentToken.Append(c);
        }
    }

    if (currentToken.Length > 0)
    {
        tokens.Add(currentToken.ToString());
    }

    return tokens;
}
```

## コメント除去

### コメントの処理

`;`で始まる行はコメントとして無視されます。

```csharp
private string StripComments(string line)
{
    int commentIndex = line.IndexOf(';');
    if (commentIndex >= 0)
    {
        // 文字列内のセミコロンを考慮
        bool inString = false;
        for (int i = 0; i < commentIndex; i++)
        {
            if (line[i] == '"' && (i == 0 || line[i - 1] != '\\'))
            {
                inString = !inString;
            }
        }

        if (!inString)
        {
            return line.Substring(0, commentIndex);
        }
    }

    return line;
}
```

## ラベルの解析

### ラベルの定義

`*`で始まる行はラベルとして認識されます。

```aria
*label_name
    text "これはラベルの内容です"
```

### ラベルの収集

```csharp
// プリパスでラベルを収集
var labels = new Dictionary<string, int>();

for (int i = 0; i < lines.Length; i++)
{
    var line = StripComments(lines[i]).TrimStart();

    if (line.StartsWith("*"))
    {
        var labelName = line.Substring(1).Trim()
            .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[0];

        labels[labelName] = -1; // 一時的なプレースホルダー
    }
}
```

### ラベルの解決

```csharp
// 本パスでラベルのアドレスを解決
for (int i = 0; i < lines.Length; i++)
{
    var line = StripComments(lines[i]).TrimStart();

    if (line.StartsWith("*"))
    {
        var labelName = line.Substring(1).Trim()
            .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[0];

        labels[labelName] = instructions.Count; // 現在の命令数をアドレスとして設定
        continue;
    }

    // 命令の処理...
}
```

## 命令の生成

### 命令オブジェクト

```csharp
public class Instruction
{
    public OpCode Op { get; set; }              // オペコード
    public List<string> Arguments { get; set; } // 引数リスト
    public int SourceLine { get; set; }        // ソース行番号
    public List<string> ConditionalTokens { get; set; } // 条件トークン（if文用）
}
```

### コマンドのマッピング

```csharp
private static readonly Dictionary<string, OpCode> KnownCommands =
    new(StringComparer.OrdinalIgnoreCase)
{
    { "text", OpCode.Text },
    { "wait", OpCode.Wait },
    { "lsp", OpCode.Lsp },
    { "lsp_text", OpCode.LspText },
    { "lsp_rect", OpCode.LspRect },
    // ... その他のコマンド
};
```

### 命令の生成プロセス

```csharp
if (KnownCommands.TryGetValue(firstToken, out OpCode op))
{
    var args = parts.Skip(1).ToList();
    instructions.Add(new Instruction(op, args, i + 1));
}
```

## 条件分岐の解析

### if文の構文

```aria
if %0 == 1
    text "条件が真です"
endif
```

### if文のパース

```csharp
if (firstToken.Equals("if", StringComparison.OrdinalIgnoreCase))
{
    int cmdIndex = -1;

    // 条件とコマンドを分割
    for (int j = 1; j < parts.Count; j++)
    {
        if (KnownCommands.ContainsKey(parts[j]) || defsubs.Contains(parts[j]))
        {
            cmdIndex = j;
            break;
        }
    }

    if (cmdIndex > 1)
    {
        var condTokens = parts.GetRange(1, cmdIndex - 1);
        var cmdToken = parts[cmdIndex];
        var opArgs = parts.Skip(cmdIndex + 1).ToList();

        if (KnownCommands.TryGetValue(cmdToken, out OpCode op))
        {
            instructions.Add(new Instruction(op, opArgs, i + 1, condTokens));
        }
    }
    continue;
}
```

### 条件の評価

条件はVM側で評価されます。

```csharp
// VM側での条件評価
private bool EvaluateCondition(List<string> condTokens)
{
    // 簡易実装
    if (condTokens.Count >= 3)
    {
        string left = condTokens[0];
        string op = condTokens[1];
        string right = condTokens[2];

        int leftVal = EvaluateExpression(left);
        int rightVal = EvaluateExpression(right);

        return op switch
        {
            "==" => leftVal == rightVal,
            "!=" => leftVal != rightVal,
            ">" => leftVal > rightVal,
            "<" => leftVal < rightVal,
            ">=" => leftVal >= rightVal,
            "<=" => leftVal <= rightVal,
            _ => false
        };
    }

    return false;
}
```

## インライン構文の展開

### インラインテキスト構文

```aria
キャラクター「テキスト」
```

この構文は以下のように展開されます：

```aria
textclear
text "キャラクター「テキスト」\\"
```

### インライン構文のパース

```csharp
string textData = stmt.TrimEnd().Replace("\\n", "\n");
var match = Regex.Match(textData, @"^([^「]+?)「(.*?)」(\\?|@?)$");

if (match.Success)
{
    // 構文シュガー: Name「Text」 の場合、自動で textclear を挿入
    instructions.Add(new Instruction(OpCode.TextClear, new List<string>(), i + 1));

    // 末尾に \ や @ がなければ自動で \（クリック待ち＆改ページ）を付与
    if (match.Groups[3].Value == "")
    {
        textData += "\\";
    }
}
```

### テキスト制御文字の処理

```aria
text "1行目\"
text "2行目@続き"
```

```csharp
string buf = "";
for (int c = 0; c < textData.Length; c++)
{
    if (textData[c] == '\\')
    {
        if (buf.Length > 0)
        {
            instructions.Add(new Instruction(OpCode.Text, new List<string> { buf }, i + 1));
            buf = "";
        }
        instructions.Add(new Instruction(OpCode.WaitClickClear, new List<string>(), i + 1));
    }
    else if (textData[c] == '@')
    {
        if (buf.Length > 0)
        {
            instructions.Add(new Instruction(OpCode.Text, new List<string> { buf }, i + 1));
            buf = "";
        }
        instructions.Add(new Instruction(OpCode.WaitClick, new List<string>(), i + 1));
    }
    else
    {
        buf += textData[c];
    }
}

if (buf.Length > 0)
{
    instructions.Add(new Instruction(OpCode.Text, new List<string> { buf }, i + 1));
}
```

## サブルーチンの解析

### defsubの定義

```aria
defsub show_message
    text "これはサブルーチンです"
    return
```

### defsubの収集

```csharp
var defsubs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

for (int i = 0; i < lines.Length; i++)
{
    var line = StripComments(lines[i]).TrimStart();

    if (line.StartsWith("defsub ", StringComparison.OrdinalIgnoreCase))
    {
        var parts = Tokenize(line);
        if (parts.Count > 1)
        {
            defsubs.Add(parts[1]);
        }
    }
}
```

### サブルーチンの呼び出し

```csharp
else if (defsubs.Contains(firstToken))
{
    instructions.Add(new Instruction(
        OpCode.Gosub,
        new List<string> { firstToken }.Concat(parts.Skip(1)).ToList(),
        i + 1
    ));
}
```

## エラー処理

### エラーの種類

1. **構文エラー**: 不正な構文
2. **未定義のラベル**: 存在しないラベルへのジャンプ
3. **引数エラー**: 不正な数や型の引数
4. **ファイルエラー**: ファイルが見つからない

### エラー報告

```csharp
public class ErrorReporter
{
    public void Report(AriaError error)
    {
        Errors.Add(error);

        if (error.Level == AriaErrorLevel.Error)
        {
            Console.WriteLine($"Error at line {error.Line}: {error.Message}");
        }
    }
}

public class AriaError
{
    public string Message { get; set; }
    public int Line { get; set; }
    public string File { get; set; }
    public AriaErrorLevel Level { get; set; }
}

public enum AriaErrorLevel
{
    Warning,
    Error,
    Fatal
}
```

### ラベル未定義エラーの検出

```csharp
foreach (var inst in instructions)
{
    if (inst.Op == OpCode.Jmp || inst.Op == OpCode.Gosub)
    {
        if (inst.Arguments.Count > 0)
        {
            string target = inst.Arguments[0].TrimStart('*');
            if (!labels.ContainsKey(target))
            {
                _reporter.Report(new AriaError(
                    $"未定義のラベル '*{target}' へのジャンプです。",
                    inst.SourceLine,
                    scriptFile,
                    AriaErrorLevel.Error
                ));
            }
        }
    }
}
```

## パフォーマンスの最適化

### トークンキャッシュ

頻繁に使用されるトークンをキャッシュして、解析コストを削減します。

```csharp
private readonly Dictionary<string, List<string>> _tokenCache =
    new Dictionary<string, List<string>>();

private List<string> TokenizeCached(string line)
{
    if (_tokenCache.TryGetValue(line, out var cachedTokens))
    {
        return cachedTokens;
    }

    var tokens = Tokenize(line);
    _tokenCache[line] = tokens;
    return tokens;
}
```

### 文字列インターン

頻繁に使用される文字列をインターンしてメモリを節約します。

```csharp
private string InternString(string str)
{
    return string.IsInterned(str) ? str : string.Intern(str);
}
```

## 拡張機能

### 新しいコマンドの追加

1. `OpCode.cs`に新しいオペコードを追加
2. `KnownCommands`辞書にマッピングを追加
3. VMに実装を追加

```csharp
// 1. OpCode.cs
public enum OpCode
{
    // ... 既存のオペコード
    MyNewCommand,
}

// 2. Parser.cs
{ "my_new_command", OpCode.MyNewCommand },

// 3. VirtualMachine.cs
case OpCode.MyNewCommand:
    ExecuteMyNewCommand(instruction);
    break;
```

### カスタム構文の追加

インライン構文のパターンを拡張して、新しい構文をサポートできます。

```csharp
// カスタム構文の例
private bool TryParseCustomSyntax(string line, out Instruction instruction)
{
    // カスタム構文のパターンマッチ
    var match = Regex.Match(line, @"^\[([^\]]+)\]\s*=\s*(.+)$");

    if (match.Success)
    {
        var variable = match.Groups[1].Value;
        var value = match.Groups[2].Value;

        instruction = new Instruction(
            OpCode.Let,
            new List<string> { variable, value },
            0
        );

        return true;
    }

    instruction = null;
    return false;
}
```

## まとめ

AriaEngineのパーサーは以下の特徴を持っています：

1. **シンプルな設計**: 明確なパースフロー
2. **NScripter互換**: 既存のスクリプトを再利用可能
3. **柔軟な構文**: インライン構文をサポート
4. **堅牢なエラー処理**: 詳細なエラー報告
5. **拡張性**: 新しいコマンドと構文を簡単に追加可能

次のドキュメントで、レンダリングシステムの詳細について学びましょう：

- [レンダリング](rendering.md) - スプライト描画の仕組み
