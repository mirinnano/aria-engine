# 仮想マシン

このドキュメントでは、AriaEngineの仮想マシン（Virtual Machine）について詳しく説明します。

## 仮想マシンの概要

AriaEngineの仮想マシンは、カスタムスクリプト言語（.aria）を実行するためのエンジンです。NScripter互換の構文をサポートし、63種類のオペコードを実行できます。

### 主な役割

- パースされた命令の実行
- プログラムカウンタの管理
- コールスタックの管理
- ゲーム状態の管理
- 制御フローの制御
- 入力待機の管理

## プログラムカウンタ

プログラムカウンタ（PC）は、現在実行中の命令のインデックスを保持します。

### プログラムカウンタの動作

```csharp
public int ProgramCounter { get; set; } = 0;
```

### プログラムカウンタの更新

- **順次実行**: 命令実行後にPCを1増加
- **ジャンプ**: ジャンプ命令でPCを直接設定
- **コール**: サブルーチン呼び出し時に現在のPCをスタックに保存
- **リターン**: スタックからPCを復元

```csharp
// 順次実行
ProgramCounter++;

// ジャンプ
ProgramCounter = targetAddress;

// サブルーチン呼び出し
CallStack.Push(ProgramCounter + 1);
ProgramCounter = subroutineAddress;

// リターン
ProgramCounter = CallStack.Pop();
```

## コールスタック

コールスタックは、サブルーチン呼び出し時の復帰アドレスを管理します。

### コールスタックの構造

```csharp
public Stack<int> CallStack { get; set; } = new Stack<int>();
```

### コールスタックの使用例

```aria
*main
    gosub *subroutine  ; PCをスタックに保存
    text "戻りました"
    end

*subroutine
    text "サブルーチンです"
    return              ; スタックからPCを復元
```

### コールスタックの状態遷移

```
1. *mainで実行中 (PC = 0)
2. gosub *subroutine実行
   - PC = 1 をスタックにプッシュ
   - PC = 10 にジャンプ（*subroutineのアドレス）

3. *subroutineで実行中 (PC = 10)
4. return実行
   - スタックから PC = 1 をポップ
   - PC = 1 に戻る

5. *mainで実行再開 (PC = 1)
```

## VMの状態

VMは複数の状態を持つことができます。

### VMの状態定義

```csharp
public enum VmState
{
    Running,           // 実行中
    WaitingForClick,   // クリック待機中
    WaitingForButton,  // ボタン待機中
    WaitingForTimer,   // タイマー待機中
    Paused,            // 一時停止中
    Stopped            // 停止中
}
```

### 状態遷移

```
Running
  ↓ (wait_click)
WaitingForClick
  ↓ (クリック)
Running

Running
  ↓ (btnwait)
WaitingForButton
  ↓ (ボタンクリック)
Running

Running
  ↓ (wait_timer)
WaitingForTimer
  ↓ (タイマー完了)
Running

Running
  ↓ (end)
Stopped
```

## ゲーム状態の管理

VMは`GameState`オブジェクトを通じてゲームの状態を管理します。

### GameStateの主要プロパティ

```csharp
public class GameState
{
    // レジスタ
    public int[] Registers { get; set; } = new int[10];  // %0-%9

    // 文字列レジスタ
    public Dictionary<string, string> StringRegisters { get; set; } = new();

    // スプライト
    public Dictionary<int, Sprite> Sprites { get; set; } = new();

    // フラグ
    public Dictionary<string, bool> Flags { get; set; } = new();

    // カウンター
    public Dictionary<string, int> Counters { get; set; } = new();

    // VM状態
    public VmState State { get; set; } = VmState.Running;

    // テキスト状態
    public string CurrentText { get; set; } = "";
    public bool TextVisible { get; set; } = true;

    // オーディオ状態
    public string CurrentBgm { get; set; } = "";
    public float BgmVolume { get; set; } = 1.0f;
}
```

## 命令の実行フロー

### 命令実行のメインループ

```csharp
public void Step()
{
    if (State != VmState.Running)
        return;

    if (ProgramCounter >= Instructions.Count)
    {
        State = VmState.Stopped;
        return;
    }

    var instruction = Instructions[ProgramCounter];
    ExecuteInstruction(instruction);
    ProgramCounter++;
}
```

### 命令のディスパッチ

```csharp
private void ExecuteInstruction(Instruction instruction)
{
    switch (instruction.Op)
    {
        case OpCode.Text:
            ExecuteText(instruction);
            break;
        case OpCode.Wait:
            ExecuteWait(instruction);
            break;
        case OpCode.Lsp:
            ExecuteLsp(instruction);
            break;
        // ... その他のオペコード
    }
}
```

## 制御フローの実装

### 条件分岐

```csharp
private void ExecuteIf(Instruction instruction)
{
    // 条件を評価
    bool condition = EvaluateCondition(instruction.ConditionalTokens);

    if (condition)
    {
        // 真の場合、命令を実行
        ExecuteInstruction(instruction.Arguments);
    }
}
```

### ジャンプ

```csharp
private void ExecuteJmp(Instruction instruction)
{
    string targetLabel = instruction.Arguments[0].TrimStart('*');

    if (Labels.TryGetValue(targetLabel, out int targetAddress))
    {
        ProgramCounter = targetAddress - 1; // -1 は Step()での +1 を考慮
    }
    else
    {
        _reporter.Report(new AriaError($"未定義のラベル: {targetLabel}"));
    }
}
```

### 条件ジャンプ

```csharp
private void ExecuteBeq(Instruction instruction)
{
    string targetLabel = instruction.Arguments[0].TrimStart('*');
    int registerIndex = int.Parse(instruction.Arguments[1].Substring(1));

    if (Registers[registerIndex] == 0)
    {
        if (Labels.TryGetValue(targetLabel, out int targetAddress))
        {
            ProgramCounter = targetAddress - 1;
        }
    }
}
```

### ループ

```csharp
private void ExecuteFor(Instruction instruction)
{
    // for %i = start to end
    string varName = instruction.Arguments[0];
    int start = int.Parse(instruction.Arguments[2]);
    int end = int.Parse(instruction.Arguments[4]);

    int varIndex = int.Parse(varName.Substring(1));
    Registers[varIndex] = start;

    // ループの終了条件をチェックするラベルを生成
    string loopEndLabel = $"_loop_end_{ProgramCounter}";

    // ループ本体の実行
    // （実際の実装ではもっと複雑な処理が必要）
}
```

## 入力待機の管理

### クリック待機

```csharp
private void ExecuteWaitClick(Instruction instruction)
{
    State = VmState.WaitingForClick;
    // 実際のクリック待機は InputHandler が処理
}
```

### ボタン待機

```csharp
private void ExecuteBtnWait(Instruction instruction)
{
    string resultVar = instruction.Arguments[0];
    int resultIndex = int.Parse(resultVar.Substring(1));

    State = VmState.WaitingForButton;
    // ボタンクリック時に InputHandler が結果を設定
}
```

### タイマー待機

```csharp
private void ExecuteWaitTimer(Instruction instruction)
{
    int milliseconds = int.Parse(instruction.Arguments[0]);
    TimerTarget = DateTime.Now.AddMilliseconds(milliseconds);
    State = VmState.WaitingForTimer;
}
```

## 変数の管理

### 整数レジスタ

```csharp
private void ExecuteLet(Instruction instruction)
{
    string varName = instruction.Arguments[0];
    int value = int.Parse(instruction.Arguments[1]);

    int varIndex = int.Parse(varName.Substring(1));
    Registers[varIndex] = value;
}

private void ExecuteInc(Instruction instruction)
{
    string varName = instruction.Arguments[0];
    int varIndex = int.Parse(varName.Substring(1));

    if (instruction.Arguments.Count > 1)
    {
        int amount = int.Parse(instruction.Arguments[1]);
        Registers[varIndex] += amount;
    }
    else
    {
        Registers[varIndex]++;
    }
}
```

### 文字列レジスタ

```csharp
private void ExecuteLetString(Instruction instruction)
{
    string varName = instruction.Arguments[0];
    string value = instruction.Arguments[1].Trim('"');

    StringRegisters[varName] = value;
}
```

## スプライト操作

### スプライトの作成

```csharp
private void ExecuteLsp(Instruction instruction)
{
    int id = int.Parse(instruction.Arguments[0]);
    string path = instruction.Arguments[1].Trim('"');
    int x = int.Parse(instruction.Arguments[2]);
    int y = int.Parse(instruction.Arguments[3]);

    var sprite = new Sprite
    {
        Id = id,
        Type = SpriteType.Image,
        ImagePath = path,
        X = x,
        Y = y,
        Visible = true
    };

    State.Sprites[id] = sprite;
}
```

### スプライトのプロパティ設定

```csharp
private void ExecuteSpAlpha(Instruction instruction)
{
    int id = int.Parse(instruction.Arguments[0]);
    int alpha = int.Parse(instruction.Arguments[1]);

    if (State.Sprites.TryGetValue(id, out Sprite sprite))
    {
        sprite.Alpha = alpha;
    }
}
```

## フラグとカウンターの管理

### フラグの設定

```csharp
private void ExecuteSetFlag(Instruction instruction)
{
    string flagName = instruction.Arguments[0].Trim('"');
    int value = int.Parse(instruction.Arguments[1]);

    State.Flags[flagName] = (value == 1);
}
```

### カウンターの操作

```csharp
private void ExecuteIncCounter(Instruction instruction)
{
    string counterName = instruction.Arguments[0].Trim('"');
    int amount = instruction.Arguments.Count > 1 ?
        int.Parse(instruction.Arguments[1]) : 1;

    if (!State.Counters.ContainsKey(counterName))
    {
        State.Counters[counterName] = 0;
    }

    State.Counters[counterName] += amount;
}
```

## エラーハンドリング

### エラー報告

```csharp
private void ReportError(string message, int sourceLine)
{
    _reporter.Report(new AriaError(
        message,
        sourceLine,
        CurrentScriptFile,
        AriaErrorLevel.Error
    ));
}
```

### ランタイムエラーの例

- 未定義のラベルへのジャンプ
- 存在しないスプライトの操作
- 不正な引数の型
- 配列の範囲外アクセス

## パフォーマンスの最適化

### ジャンプテーブル

```csharp
private readonly Dictionary<OpCode, Action<Instruction>> _opcodeHandlers;

public VirtualMachine()
{
    _opcodeHandlers = new Dictionary<OpCode, Action<Instruction>>
    {
        { OpCode.Text, ExecuteText },
        { OpCode.Wait, ExecuteWait },
        { OpCode.Lsp, ExecuteLsp },
        // ... その他のオペコード
    };
}

private void ExecuteInstruction(Instruction instruction)
{
    if (_opcodeHandlers.TryGetValue(instruction.Op, out var handler))
    {
        handler(instruction);
    }
}
```

### 命令キャッシュ

頻繁に実行される命令をキャッシュして、解析コストを削減します。

```csharp
private readonly Dictionary<int, Instruction> _instructionCache;
```

## デバッグ機能

### デバッグモード

```csharp
public bool DebugMode { get; set; } = false;

public void UpdateDebugInfo()
{
    if (DebugMode)
    {
        // デバッグ情報を表示
        Console.WriteLine($"PC: {ProgramCounter}");
        Console.WriteLine($"State: {State}");
        Console.WriteLine($"Registers: {string.Join(", ", Registers)}");
    }
}
```

### ブレークポイント

```csharp
public HashSet<int> Breakpoints { get; set; } = new HashSet<int>();

public void Step()
{
    if (Breakpoints.Contains(ProgramCounter))
    {
        State = VmState.Paused;
        // ブレークポイントで停止
    }

    // 通常の実行
}
```

## まとめ

AriaEngineの仮想マシンは以下の特徴を持っています：

1. **シンプルな設計**: 命令セットが明確で理解しやすい
2. **効率的な実行**: ジャンプテーブルと命令キャッシュで高速化
3. **柔軟な状態管理**: 複数の待機状態をサポート
4. **拡張性**: 新しいオペコードを簡単に追加可能
5. **デバッグ支援**: デバッグモードとブレークポイントをサポート

次のドキュメントで、パーサーの詳細について学びましょう：

- [パーサー](parser.md) - スクリプト解析の詳細
- [レンダリング](rendering.md) - スプライト描画の仕組み
