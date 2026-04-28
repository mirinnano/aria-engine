# Ariaバイトコードファイルフォーマット仕様 (.arib)

## 概要

`.arib`ファイルは、`.aria`スクリプトを事前コンパイルしたバイトコードファイルです。
バイナリ形式で、高速なロードと実行を可能にします。

## ファイル構造

```
+------------------+
|     Header       | 16 bytes
+------------------+
|  Function Table  | FunctionCount * 16 bytes
+------------------+
|  String Table    | Variable length
+------------------+
|  Constant Table  | Variable length
+------------------+
|  Debug Info      | Variable length (optional)
+------------------+
|  Code Section    | Variable length
+------------------+
```

## 1. ヘッダー (16 bytes)

| Offset | Size | Type   | Description                              |
|--------|------|--------|------------------------------------------|
| 0x00   | 4    | string | Magic: "ARIB" (0x41 0x52 0x49 0x42)     |
| 0x04   | 2    | uint16 | Version: 1 (リトルエンディアン)          |
| 0x06   | 2    | uint16 | FunctionCount: 関数の数                 |
| 0x08   | 4    | uint32 | StringTableOffset: 文字列テーブルの位置 |
| 0x0C   | 4    | uint32 | ConstantTableOffset: 定数テーブルの位置 |
| 0x10   | 4    | uint32 | CodeSize: コードセクションのサイズ      |

## 2. 関数テーブル (FunctionCount * 16 bytes)

各関数のエントリ:

| Offset | Size | Type   | Description                           |
|--------|------|--------|---------------------------------------|
| +0x00  | 4    | uint32 | NameOffset: 関数名の文字列テーブル内オフセット |
| +0x04  | 4    | uint32 | EntryPoint: コードセクション内の開始位置 |
| +0x08  | 2    | uint16 | LocalCount: ローカル変数の数          |
| +0x0A  | 2    | uint16 | ParamCount: パラメータの数            |
| +0x0C  | 1    | uint8  | ReturnType: 戻り値の型                |
| +0x0D  | 1    | uint8  | Flags: 関数フラグ                     |
| +0x0E  | 2    | uint16 | Reserved: 予約済み（0で埋める）       |

### 戻り値の型 (ReturnType)

| Value | Type      | Description |
|-------|-----------|-------------|
| 0     | Void      | 戻り値なし |
| 1     | Int       | 整数        |
| 2     | Float     | 浮動小数点  |
| 3     | String    | 文字列      |
| 4     | Bool      | 真偽値      |
| 5     | Struct    | 構造体      |

### 関数フラグ (Flags)

| Bit | Description        |
|-----|--------------------|
| 0   | External (外部関数)|
| 1   | Variadic (可変長引数)|
| 2-7 | Reserved           |

## 3. 文字列テーブル

```
+------------------+
|  StringCount     | 4 bytes (uint32)
+------------------+
|  String 1 Length | 4 bytes (uint32)
+------------------+
|  String 1 Data   | Variable (UTF-8)
+------------------+
|  String 2 Length | 4 bytes (uint32)
+------------------+
|  String 2 Data   | Variable (UTF-8)
+------------------+
|  ...             |
+------------------+
```

文字列はUTF-8エンコーディングで、長さを含みます。

## 4. 定数テーブル

```
+------------------+
|  ConstantCount   | 4 bytes (uint32)
+------------------+
|  Constant 1 Type | 1 byte (uint8)
+------------------+
|  Constant 1 Data | Variable
+------------------+
|  Constant 2 Type | 1 byte (uint8)
+------------------+
|  Constant 2 Data | Variable
+------------------+
|  ...             |
+------------------+
```

### 定数の型

| Value | Type   | Data Size |
|-------|--------|-----------|
| 0     | Int    | 4 bytes   |
| 1     | Float  | 4 bytes   |
| 2     | String | String table offset |
| 3     | Bool   | 1 byte    |

## 5. デバッグ情報 (Optional)

```
+------------------+
|  DebugInfoSize   | 4 bytes (uint32)
+------------------+
|  SourceFilePath  | String table offset
+------------------+
|  LineInfoCount   | 4 bytes (uint32)
+------------------+
|  Line Info 1     | 12 bytes
+------------------+
|  Line Info 2     | 12 bytes
+------------------+
|  ...             |
+------------------+
```

### 行情報 (Line Info: 12 bytes)

| Offset | Size | Type   | Description                    |
|--------|------|--------|--------------------------------|
| +0x00  | 4    | uint32 | CodeOffset: コードセクション内の位置 |
| +0x04  | 4    | uint32 | SourceLine: ソースコードの行番号  |
| +0x08  | 4    | uint32 | SourceColumn: ソースコードの列番号 |

## 6. コードセクション

バイトコード命令のシーケンス。

### 命令フォーマット

各命令は以下の形式です:

```
+------------------+
|  OpCode          | 1 byte
+------------------+
|  Operands        | Variable (if any)
+------------------+
```

### オペランドのエンコーディング

#### 整数 (Int)
- 4 bytes, リトルエンディアン, int32

#### 浮動小数点数 (Float)
- 4 bytes, リトルエンディアン, float32

#### 文字列インデックス (String Index)
- 4 bytes, リトルエンディアン, uint32 (文字列テーブルへのオフセット)

#### 関数インデックス (Function Index)
- 2 bytes, リトルエンディアン, uint16 (関数テーブル内のインデックス)

#### ローカル変数インデックス (Local Index)
- 2 bytes, リトルエンディアン, uint16

#### レジスタ番号 (Register Number)
- 1 byte, uint8

#### ラベル/ジャンプターゲット (Jump Target)
- 4 bytes, リトルエンディアン, uint32 (コードセクション内のオフセット)

## 例

### ソースコード

```aria
func add(a: int, b: int) -> int
    return a + b
endfunc

func main() -> void
    local int %result = add(10, 20)
endfunc
```

### バイトコード表現

```
Header:
  Magic: "ARIB"
  Version: 1
  FunctionCount: 2
  StringTableOffset: 64
  ConstantTableOffset: 100
  CodeSize: 32

Function Table:
  Function 0:
    NameOffset: 0 ("add")
    EntryPoint: 0
    LocalCount: 0
    ParamCount: 2
    ReturnType: 1 (Int)
    Flags: 0

  Function 1:
    NameOffset: 4 ("main")
    EntryPoint: 6
    LocalCount: 1
    ParamCount: 0
    ReturnType: 0 (Void)
    Flags: 0

String Table:
  StringCount: 2
  "add" (length 3)
  "main" (length 4)

Constant Table:
  ConstantCount: 2
  Int 10
  Int 20

Code Section:
  # func add(a: int, b: int) -> int
  0x10 0x0000  ; LoadLocal 0 (a)
  0x10 0x0001  ; LoadLocal 1 (b)
  0x20         ; Add
  0x55         ; ReturnValue

  # func main() -> void
  0x01 0x0A 0x00 0x00 0x00  ; PushInt 10
  0x01 0x14 0x00 0x00 0x00  ; PushInt 20
  0x53 0x00 0x00            ; Call 0 (add)
  0x11 0x0000              ; StoreLocal 0
  0x54                     ; Return
```

## バリデーション

バイトコードファイルの読み込み時に以下のバリデーションを行う必要があります:

1. **マジックナンバーの確認**: ファイルの先頭4バイトが"ARIB"であること
2. **バージョン互換性**: サポートされているバージョンであること
3. **オフセットの範囲チェック**: すべてのオフセットがファイルサイズ内であること
4. **関数テーブルの整合性**: エントリポイントがコードセクション内であること
5. **文字列テーブルの整合性**: すべての文字列オフセットが有効であること

## エンディアン

すべての数値はリトルエンディアン（Intel x86互換）でエンコードされます。

## 拡張性

将来の拡張のために、以下の拡張ポイントが用意されています:

1. **ヘッダーのReservedフィールド**: 将来のフラグやオプション用
2. **関数テーブルのReservedフィールド**: 将来の関数属性用
3. **Debug Info**: オプションで省略可能
4. **拡張命令 (0xE0)**: プラットフォーム固有の命令用

## セキュリティ

バイトコードファイルは信頼できないソースから来る可能性があるため、実行時に以下のセキュリティチェックを行う必要があります:

1. **スタックオーバーフローの検出**
2. **無限ループの検出（タイムアウト）**
3. **メモリアクセスの境界チェック**
4. **関数呼び出しの深さ制限**
5. **オペランドの型チェック**
