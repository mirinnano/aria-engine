using System;

namespace AriaEngine.VM;

/// <summary>
/// レジスタファイル
/// ASM的な低レイヤー操作をサポート
/// </summary>
public class RegisterFile
{
    // ========================================
    // 定数
    // ========================================

    public const int RegisterCount = 256;
    public const int GeneralPurposeStart = 0;
    public const int GeneralPurposeEnd = 31;
    public const int SpecialStart = 32;
    public const int SpecialEnd = 63;
    public const int ReservedStart = 64;
    public const int ReservedEnd = 255;

    // 特殊レジスタ
    public const int R0 = 0;   // 汎用レジスタ0
    public const int R1 = 1;   // 汎用レジスタ1
    public const int R2 = 2;   // 汎用レジスタ2
    public const int R3 = 3;   // 汎用レジスタ3
    public const int R4 = 4;   // 汎用レジスタ4
    public const int R5 = 5;   // 汎用レジスタ5
    public const int R6 = 6;   // 汎用レジスタ6
    public const int R7 = 7;   // 汎用レジスタ7
    public const int R8 = 8;   // 汎用レジスタ8
    public const int R9 = 9;   // 汎用レジスタ9
    public const int R10 = 10; // 汎用レジスタ10
    public const int R11 = 11; // 汎用レジスタ11
    public const int R12 = 12; // 汎用レジスタ12
    public const int R13 = 13; // 汎用レジスタ13
    public const int R14 = 14; // 汎用レジスタ14
    public const int R15 = 15; // 汎用レジスタ15

    // 特殊レジスタ（システム用）
    public const int SP = 32;  // スタックポインタ（読み取り専用）
    public const int FP = 33;  // フレームポインタ（読み取り専用）
    public const int PC = 34;  // プログラムカウンタ（読み取り専用）
    public const int FLAGS = 35; // フラグレジスタ

    // ========================================
    // フラグ定義
    // ========================================

    public const int FlagZero = 1 << 0;       // ゼロフラグ
    public const int FlagSign = 1 << 1;       // 符号フラグ
    public const int FlagOverflow = 1 << 2;   // オーバーフローフラグ
    public const int FlagCarry = 1 << 3;      // キャリーフラグ
    public const int FlagEqual = 1 << 4;      // 等値フラグ
    public const int FlagLess = 1 << 5;       // 小なりフラグ
    public const int FlagGreater = 1 << 6;    // 大なりフラグ

    // ========================================
    // 状態
    // ========================================

    private readonly int[] _registers;
    private readonly RegisterInfo[] _registerInfos;

    // 外部からの更新用コールバック
    private Action<int>? _onRegisterChanged;

    // ========================================
    // インデクサー
    // ========================================

    /// <summary>
    /// レジスタへのアクセス
    /// </summary>
    public int this[int index]
    {
        get
        {
            ValidateRegisterIndex(index);
            return _registers[index];
        }
        set
        {
            ValidateRegisterIndex(index);
            _registers[index] = value;
            _registerInfos[index].LastModified = DateTime.UtcNow;
            _onRegisterChanged?.Invoke(index);
        }
    }

    /// <summary>
    /// 汎用レジスタへの名前付きアクセス
    /// </summary>
    public int R0Value
    {
        get => this[R0];
        set => this[R0] = value;
    }

    public int R1Value
    {
        get => this[R1];
        set => this[R1] = value;
    }

    public int R2Value
    {
        get => this[R2];
        set => this[R2] = value;
    }

    public int R3Value
    {
        get => this[R3];
        set => this[R3] = value;
    }

    public int R4Value
    {
        get => this[R4];
        set => this[R4] = value;
    }

    public int R5Value
    {
        get => this[R5];
        set => this[R5] = value;
    }

    public int R6Value
    {
        get => this[R6];
        set => this[R6] = value;
    }

    public int R7Value
    {
        get => this[R7];
        set => this[R7] = value;
    }

    public int R8Value
    {
        get => this[R8];
        set => this[R8] = value;
    }

    public int R9Value
    {
        get => this[R9];
        set => this[R9] = value;
    }

    public int R10Value
    {
        get => this[R10];
        set => this[R10] = value;
    }

    public int R11Value
    {
        get => this[R11];
        set => this[R11] = value;
    }

    public int R12Value
    {
        get => this[R12];
        set => this[R12] = value;
    }

    public int R13Value
    {
        get => this[R13];
        set => this[R13] = value;
    }

    public int R14Value
    {
        get => this[R14];
        set => this[R14] = value;
    }

    public int R15Value
    {
        get => this[R15];
        set => this[R15] = value;
    }

    // ========================================
    // フラグ操作
    // ========================================

    /// <summary>
    /// フラグレジスタ
    /// </summary>
    public int Flags
    {
        get => this[FLAGS];
        set => this[FLAGS] = value;
    }

    /// <summary>
    /// ゼロフラグ
    /// </summary>
    public bool ZeroFlag
    {
        get => (Flags & FlagZero) != 0;
        set => Flags = value ? Flags | FlagZero : Flags & ~FlagZero;
    }

    /// <summary>
    /// 符号フラグ
    /// </summary>
    public bool SignFlag
    {
        get => (Flags & FlagSign) != 0;
        set => Flags = value ? Flags | FlagSign : Flags & ~FlagSign;
    }

    /// <summary>
    /// オーバーフローフラグ
    /// </summary>
    public bool OverflowFlag
    {
        get => (Flags & FlagOverflow) != 0;
        set => Flags = value ? Flags | FlagOverflow : Flags & ~FlagOverflow;
    }

    /// <summary>
    /// キャリーフラグ
    /// </summary>
    public bool CarryFlag
    {
        get => (Flags & FlagCarry) != 0;
        set => Flags = value ? Flags | FlagCarry : Flags & ~FlagCarry;
    }

    /// <summary>
    /// 等値フラグ
    /// </summary>
    public bool EqualFlag
    {
        get => (Flags & FlagEqual) != 0;
        set => Flags = value ? Flags | FlagEqual : Flags & ~FlagEqual;
    }

    /// <summary>
    /// 小なりフラグ
    /// </summary>
    public bool LessFlag
    {
        get => (Flags & FlagLess) != 0;
        set => Flags = value ? Flags | FlagLess : Flags & ~FlagLess;
    }

    /// <summary>
    /// 大なりフラグ
    /// </summary>
    public bool GreaterFlag
    {
        get => (Flags & FlagGreater) != 0;
        set => Flags = value ? Flags | FlagGreater : Flags & ~FlagGreater;
    }

    // ========================================
    // コンストラクタ
    // ========================================

    public RegisterFile()
    {
        _registers = new int[RegisterCount];
        _registerInfos = new RegisterInfo[RegisterCount];

        for (int i = 0; i < RegisterCount; i++)
        {
            _registerInfos[i] = new RegisterInfo
            {
                Index = i,
                Name = GetRegisterName(i),
                Type = GetRegisterType(i),
                IsReadOnly = i >= SpecialStart && i <= SpecialEnd,
                LastModified = DateTime.MinValue
            };
        }
    }

    // ========================================
    // 操作メソッド
    // ========================================

    /// <summary>
    /// レジスタをクリア
    /// </summary>
    public void Clear()
    {
        Array.Clear(_registers, 0, RegisterCount);
        Flags = 0;
    }

    /// <summary>
    /// 汎用レジスタをクリア
    /// </summary>
    public void ClearGeneralPurpose()
    {
        Array.Clear(_registers, GeneralPurposeStart, GeneralPurposeEnd - GeneralPurposeStart + 1);
    }

    /// <summary>
    /// レジスタの値をコピー
    /// </summary>
    public void Copy(int source, int destination)
    {
        ValidateRegisterIndex(source);
        ValidateRegisterIndex(destination);

        if (_registerInfos[destination].IsReadOnly)
            throw new InvalidOperationException($"Register {destination} is read-only");

        _registers[destination] = _registers[source];
        _registerInfos[destination].LastModified = DateTime.UtcNow;
        _onRegisterChanged?.Invoke(destination);
    }

    /// <summary>
    /// レジスタの値を交換
    /// </summary>
    public void Swap(int reg1, int reg2)
    {
        ValidateRegisterIndex(reg1);
        ValidateRegisterIndex(reg2);

        if (_registerInfos[reg1].IsReadOnly || _registerInfos[reg2].IsReadOnly)
            throw new InvalidOperationException($"Cannot swap read-only registers");

        int temp = _registers[reg1];
        _registers[reg1] = _registers[reg2];
        _registers[reg2] = temp;

        DateTime now = DateTime.UtcNow;
        _registerInfos[reg1].LastModified = now;
        _registerInfos[reg2].LastModified = now;

        _onRegisterChanged?.Invoke(reg1);
        _onRegisterChanged?.Invoke(reg2);
    }

    /// <summary>
    /// レジスタをダンプ（デバッグ用）
    /// </summary>
    public string Dump()
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("=== Register File ===");

        // 汎用レジスタ
        sb.AppendLine("\nGeneral Purpose Registers:");
        for (int i = GeneralPurposeStart; i <= GeneralPurposeEnd; i++)
        {
            sb.AppendLine($"  R{i:D2} ({i,3}): 0x{_registers[i]:X8} ({_registers[i],11})");
        }

        // 特殊レジスタ
        sb.AppendLine("\nSpecial Registers:");
        sb.AppendLine($"  SP  ( 32): 0x{_registers[SP]:X8} ({_registers[SP],11})");
        sb.AppendLine($"  FP  ( 33): 0x{_registers[FP]:X8} ({_registers[FP],11})");
        sb.AppendLine($"  PC  ( 34): 0x{_registers[PC]:X8} ({_registers[PC],11})");
        sb.AppendLine($"  FLAGS( 35): 0x{_registers[FLAGS]:X8}");

        // フラグの詳細
        sb.AppendLine("\nFlags:");
        sb.AppendLine($"  Zero:     {(ZeroFlag ? "1" : "0")}");
        sb.AppendLine($"  Sign:     {(SignFlag ? "1" : "0")}");
        sb.AppendLine($"  Overflow: {(OverflowFlag ? "1" : "0")}");
        sb.AppendLine($"  Carry:    {(CarryFlag ? "1" : "0")}");
        sb.AppendLine($"  Equal:    {(EqualFlag ? "1" : "0")}");
        sb.AppendLine($"  Less:     {(LessFlag ? "1" : "0")}");
        sb.AppendLine($"  Greater:  {(GreaterFlag ? "1" : "0")}");

        // 使用中の予約済みレジスタ
        sb.AppendLine("\nUsed Reserved Registers:");
        bool hasUsedReserved = false;
        for (int i = ReservedStart; i <= ReservedEnd; i++)
        {
            if (_registers[i] != 0)
            {
                sb.AppendLine($"  R{i:D3} ({i,3}): 0x{_registers[i]:X8} ({_registers[i],11})");
                hasUsedReserved = true;
            }
        }
        if (!hasUsedReserved)
        {
            sb.AppendLine("  (none)");
        }

        return sb.ToString();
    }

    /// <summary>
    /// 16進数ダンプ（コンパクト）
    /// </summary>
    public string DumpHex()
    {
        var sb = new System.Text.StringBuilder();

        // 汎用レジスタ（1行に8つ）
        sb.Append("GP: ");
        for (int i = 0; i < 16; i++)
        {
            sb.Append($"{_registers[i]:X8} ");
            if ((i + 1) % 8 == 0) sb.Append("\n    ");
        }
        sb.AppendLine();

        // 特殊レジスタ
        sb.AppendLine($"SP: {_registers[SP]:X8}  FP: {_registers[FP]:X8}  PC: {_registers[PC]:X8}  FLAGS: {_registers[FLAGS]:X8}");

        return sb.ToString();
    }

    /// <summary>
    /// レジスタ変更イベントハンドラを設定
    /// </summary>
    public void SetChangeHandler(Action<int> handler)
    {
        _onRegisterChanged = handler;
    }

    // ========================================
    // 内部ヘルパーメソッド
    // ========================================

    private void ValidateRegisterIndex(int index)
    {
        if (index < 0 || index >= RegisterCount)
            throw new IndexOutOfRangeException($"Register index out of range: {index} (0-{RegisterCount - 1})");
    }

    private static string GetRegisterName(int index)
    {
        return index switch
        {
            < 16 => $"R{index}",
            SP => "SP",
            FP => "FP",
            PC => "PC",
            FLAGS => "FLAGS",
            _ => $"R{index}"
        };
    }

    private static RegisterType GetRegisterType(int index)
    {
        return index switch
        {
            >= GeneralPurposeStart and <= GeneralPurposeEnd => RegisterType.GeneralPurpose,
            >= SpecialStart and <= SpecialEnd => RegisterType.Special,
            _ => RegisterType.Reserved
        };
    }
}

/// <summary>
/// レジスタタイプ
/// </summary>
public enum RegisterType
{
    GeneralPurpose,
    Special,
    Reserved
}

/// <summary>
/// レジスタ情報
/// </summary>
public struct RegisterInfo
{
    public int Index;
    public string Name;
    public RegisterType Type;
    public bool IsReadOnly;
    public DateTime LastModified;

    public RegisterInfo()
    {
        Index = 0;
        Name = string.Empty;
        Type = RegisterType.GeneralPurpose;
        IsReadOnly = false;
        LastModified = DateTime.MinValue;
    }
}
