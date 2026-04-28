using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace AriaEngine.Compiler;

/// <summary>
/// バイトコードファイルのヘッダー
/// </summary>
public struct BytecodeHeader
{
    public const int Size = 16;

    public string Magic;
    public ushort Version;
    public ushort FunctionCount;
    public uint StringTableOffset;
    public uint ConstantTableOffset;
    public uint CodeSize;

    public BytecodeHeader()
    {
        Magic = "ARIB";
        Version = 1;
        FunctionCount = 0;
        StringTableOffset = 0;
        ConstantTableOffset = 0;
        CodeSize = 0;
    }

    public byte[] ToBytes()
    {
        byte[] bytes = new byte[Size];

        // Magic (4 bytes)
        byte[] magicBytes = Encoding.ASCII.GetBytes(Magic.PadRight(4).Substring(0, 4));
        Array.Copy(magicBytes, 0, bytes, 0, 4);

        // Version (2 bytes, little endian)
        byte[] versionBytes = BitConverter.GetBytes(Version);
        if (!BitConverter.IsLittleEndian) Array.Reverse(versionBytes);
        Array.Copy(versionBytes, 0, bytes, 4, 2);

        // FunctionCount (2 bytes, little endian)
        byte[] functionCountBytes = BitConverter.GetBytes(FunctionCount);
        if (!BitConverter.IsLittleEndian) Array.Reverse(functionCountBytes);
        Array.Copy(functionCountBytes, 0, bytes, 6, 2);

        // StringTableOffset (4 bytes, little endian)
        byte[] stringOffsetBytes = BitConverter.GetBytes(StringTableOffset);
        if (!BitConverter.IsLittleEndian) Array.Reverse(stringOffsetBytes);
        Array.Copy(stringOffsetBytes, 0, bytes, 8, 4);

        // ConstantTableOffset (4 bytes, little endian)
        byte[] constantOffsetBytes = BitConverter.GetBytes(ConstantTableOffset);
        if (!BitConverter.IsLittleEndian) Array.Reverse(constantOffsetBytes);
        Array.Copy(constantOffsetBytes, 0, bytes, 12, 4);

        // CodeSize (4 bytes, little endian)
        byte[] codeSizeBytes = BitConverter.GetBytes(CodeSize);
        if (!BitConverter.IsLittleEndian) Array.Reverse(codeSizeBytes);
        Array.Copy(codeSizeBytes, 0, bytes, 16, 4);

        return bytes;
    }

    public static BytecodeHeader FromBytes(byte[] bytes)
    {
        if (bytes.Length < Size)
            throw new ArgumentException($"Invalid header size: {bytes.Length} < {Size}");

        if (Encoding.ASCII.GetString(bytes, 0, 4) != "ARIB")
            throw new InvalidDataException("Invalid magic number: expected 'ARIB'");

        ushort version = BitConverter.ToUInt16(bytes, 4);
        if (!BitConverter.IsLittleEndian) Array.Reverse(BitConverter.GetBytes(version));

        ushort functionCount = BitConverter.ToUInt16(bytes, 6);
        if (!BitConverter.IsLittleEndian) Array.Reverse(BitConverter.GetBytes(functionCount));

        uint stringTableOffset = BitConverter.ToUInt32(bytes, 8);
        if (!BitConverter.IsLittleEndian) Array.Reverse(BitConverter.GetBytes(stringTableOffset));

        uint constantTableOffset = BitConverter.ToUInt32(bytes, 12);
        if (!BitConverter.IsLittleEndian) Array.Reverse(BitConverter.GetBytes(constantTableOffset));

        uint codeSize = BitConverter.ToUInt32(bytes, 16);
        if (!BitConverter.IsLittleEndian) Array.Reverse(BitConverter.GetBytes(codeSize));

        return new BytecodeHeader
        {
            Magic = "ARIB",
            Version = version,
            FunctionCount = functionCount,
            StringTableOffset = stringTableOffset,
            ConstantTableOffset = constantTableOffset,
            CodeSize = codeSize
        };
    }
}

/// <summary>
/// 関数エントリ
/// </summary>
public struct FunctionEntry
{
    public const int Size = 16;

    public uint NameOffset;
    public uint EntryPoint;
    public ushort LocalCount;
    public ushort ParamCount;
    public byte ReturnType;
    public byte Flags;
    public ushort Reserved;

    public FunctionEntry()
    {
        NameOffset = 0;
        EntryPoint = 0;
        LocalCount = 0;
        ParamCount = 0;
        ReturnType = 0;
        Flags = 0;
        Reserved = 0;
    }

    public byte[] ToBytes()
    {
        byte[] bytes = new byte[Size];

        // NameOffset (4 bytes, little endian)
        byte[] nameOffsetBytes = BitConverter.GetBytes(NameOffset);
        if (!BitConverter.IsLittleEndian) Array.Reverse(nameOffsetBytes);
        Array.Copy(nameOffsetBytes, 0, bytes, 0, 4);

        // EntryPoint (4 bytes, little endian)
        byte[] entryPointBytes = BitConverter.GetBytes(EntryPoint);
        if (!BitConverter.IsLittleEndian) Array.Reverse(entryPointBytes);
        Array.Copy(entryPointBytes, 0, bytes, 4, 4);

        // LocalCount (2 bytes, little endian)
        byte[] localCountBytes = BitConverter.GetBytes(LocalCount);
        if (!BitConverter.IsLittleEndian) Array.Reverse(localCountBytes);
        Array.Copy(localCountBytes, 0, bytes, 8, 2);

        // ParamCount (2 bytes, little endian)
        byte[] paramCountBytes = BitConverter.GetBytes(ParamCount);
        if (!BitConverter.IsLittleEndian) Array.Reverse(paramCountBytes);
        Array.Copy(paramCountBytes, 0, bytes, 10, 2);

        // ReturnType (1 byte)
        bytes[12] = ReturnType;

        // Flags (1 byte)
        bytes[13] = Flags;

        // Reserved (2 bytes, little endian)
        byte[] reservedBytes = BitConverter.GetBytes(Reserved);
        if (!BitConverter.IsLittleEndian) Array.Reverse(reservedBytes);
        Array.Copy(reservedBytes, 0, bytes, 14, 2);

        return bytes;
    }

    public static FunctionEntry FromBytes(byte[] bytes, int offset)
    {
        if (bytes.Length < offset + Size)
            throw new ArgumentException($"Invalid function entry size");

        uint nameOffset = BitConverter.ToUInt32(bytes, offset);
        if (!BitConverter.IsLittleEndian) Array.Reverse(BitConverter.GetBytes(nameOffset));

        uint entryPoint = BitConverter.ToUInt32(bytes, offset + 4);
        if (!BitConverter.IsLittleEndian) Array.Reverse(BitConverter.GetBytes(entryPoint));

        ushort localCount = BitConverter.ToUInt16(bytes, offset + 8);
        if (!BitConverter.IsLittleEndian) Array.Reverse(BitConverter.GetBytes(localCount));

        ushort paramCount = BitConverter.ToUInt16(bytes, offset + 10);
        if (!BitConverter.IsLittleEndian) Array.Reverse(BitConverter.GetBytes(paramCount));

        byte returnType = bytes[offset + 12];
        byte flags = bytes[offset + 13];

        ushort reserved = BitConverter.ToUInt16(bytes, offset + 14);
        if (!BitConverter.IsLittleEndian) Array.Reverse(BitConverter.GetBytes(reserved));

        return new FunctionEntry
        {
            NameOffset = nameOffset,
            EntryPoint = entryPoint,
            LocalCount = localCount,
            ParamCount = paramCount,
            ReturnType = returnType,
            Flags = flags,
            Reserved = reserved
        };
    }
}

/// <summary>
/// 戻り値の型
/// </summary>
public enum ReturnType : byte
{
    Void = 0,
    Int = 1,
    Float = 2,
    String = 3,
    Bool = 4,
    Struct = 5
}

/// <summary>
/// 関数フラグ
/// </summary>
[Flags]
public enum FunctionFlags : byte
{
    None = 0,
    External = 1 << 0,
    Variadic = 1 << 1
}

/// <summary>
/// 定数の型
/// </summary>
public enum ConstantType : byte
{
    Int = 0,
    Float = 1,
    String = 2,
    Bool = 3
}

/// <summary>
/// 定数値
/// </summary>
public struct ConstantValue : IEquatable<ConstantValue>
{
    public ConstantType Type;
    public int IntValue;
    public float FloatValue;
    public int StringIndex;
    public bool BoolValue;

    public ConstantValue()
    {
        Type = ConstantType.Int;
        IntValue = 0;
        FloatValue = 0f;
        StringIndex = 0;
        BoolValue = false;
    }

    public static ConstantValue FromInt(int value) => new() { Type = ConstantType.Int, IntValue = value };
    public static ConstantValue FromFloat(float value) => new() { Type = ConstantType.Float, FloatValue = value };
    public static ConstantValue FromString(int index) => new() { Type = ConstantType.String, StringIndex = index };
    public static ConstantValue FromBool(bool value) => new() { Type = ConstantType.Bool, BoolValue = value };

    public bool Equals(ConstantValue other)
    {
        return Type == other.Type &&
               IntValue == other.IntValue &&
               FloatValue == other.FloatValue &&
               StringIndex == other.StringIndex &&
               BoolValue == other.BoolValue;
    }

    public override bool Equals(object? obj)
    {
        return obj is ConstantValue other && Equals(other);
    }

    public override int GetHashCode()
    {
        int hash = 17;
        hash = hash * 31 + Type.GetHashCode();
        hash = hash * 31 + IntValue.GetHashCode();
        hash = hash * 31 + FloatValue.GetHashCode();
        hash = hash * 31 + StringIndex.GetHashCode();
        hash = hash * 31 + BoolValue.GetHashCode();
        return hash;
    }

    public static bool operator ==(ConstantValue left, ConstantValue right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(ConstantValue left, ConstantValue right)
    {
        return !(left == right);
    }
}

/// <summary>
/// 行情報（デバッグ用）
/// </summary>
public struct LineInfo
{
    public const int Size = 12;

    public uint CodeOffset;
    public uint SourceLine;
    public uint SourceColumn;

    public LineInfo()
    {
        CodeOffset = 0;
        SourceLine = 0;
        SourceColumn = 0;
    }

    public byte[] ToBytes()
    {
        byte[] bytes = new byte[Size];

        byte[] codeOffsetBytes = BitConverter.GetBytes(CodeOffset);
        if (!BitConverter.IsLittleEndian) Array.Reverse(codeOffsetBytes);
        Array.Copy(codeOffsetBytes, 0, bytes, 0, 4);

        byte[] sourceLineBytes = BitConverter.GetBytes(SourceLine);
        if (!BitConverter.IsLittleEndian) Array.Reverse(sourceLineBytes);
        Array.Copy(sourceLineBytes, 0, bytes, 4, 4);

        byte[] sourceColumnBytes = BitConverter.GetBytes(SourceColumn);
        if (!BitConverter.IsLittleEndian) Array.Reverse(sourceColumnBytes);
        Array.Copy(sourceColumnBytes, 0, bytes, 8, 4);

        return bytes;
    }

    public static LineInfo FromBytes(byte[] bytes, int offset)
    {
        if (bytes.Length < offset + Size)
            throw new ArgumentException($"Invalid line info size");

        uint codeOffset = BitConverter.ToUInt32(bytes, offset);
        if (!BitConverter.IsLittleEndian) Array.Reverse(BitConverter.GetBytes(codeOffset));

        uint sourceLine = BitConverter.ToUInt32(bytes, offset + 4);
        if (!BitConverter.IsLittleEndian) Array.Reverse(BitConverter.GetBytes(sourceLine));

        uint sourceColumn = BitConverter.ToUInt32(bytes, offset + 8);
        if (!BitConverter.IsLittleEndian) Array.Reverse(BitConverter.GetBytes(sourceColumn));

        return new LineInfo
        {
            CodeOffset = codeOffset,
            SourceLine = sourceLine,
            SourceColumn = sourceColumn
        };
    }
}

/// <summary>
/// バイトコードファイル
/// </summary>
public class BytecodeFile
{
    public BytecodeHeader Header { get; set; }
    public List<FunctionEntry> Functions { get; set; } = new();
    public List<string> Strings { get; set; } = new();
    public List<ConstantValue> Constants { get; set; } = new();
    public List<LineInfo> DebugInfo { get; set; } = new();
    public byte[] Code { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// バイトコードファイルをバイナリにシリアライズ
    /// </summary>
    public byte[] ToBytes()
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // ヘッダーを書き込み（プレースホルダー）
        writer.Write(Header.ToBytes());

        // 関数テーブルを書き込み（プレースホルダー）
        foreach (var func in Functions)
        {
            writer.Write(func.ToBytes());
        }

        // 文字列テーブルのオフセットを記録
        uint stringTableOffset = (uint)ms.Position;
        Header = new BytecodeHeader
        {
            Magic = Header.Magic,
            Version = Header.Version,
            FunctionCount = Header.FunctionCount,
            StringTableOffset = stringTableOffset,
            ConstantTableOffset = 0, // 後で更新
            CodeSize = 0 // 後で更新
        };

        // 文字列テーブルを書き込み
        writer.Write(Strings.Count);
        foreach (var str in Strings)
        {
            byte[] strBytes = Encoding.UTF8.GetBytes(str);
            writer.Write(strBytes.Length);
            writer.Write(strBytes);
        }

        // 定数テーブルのオフセットを記録
        uint constantTableOffset = (uint)ms.Position;
        Header = new BytecodeHeader
        {
            Magic = Header.Magic,
            Version = Header.Version,
            FunctionCount = Header.FunctionCount,
            StringTableOffset = Header.StringTableOffset,
            ConstantTableOffset = constantTableOffset,
            CodeSize = 0 // 後で更新
        };

        // 定数テーブルを書き込み
        writer.Write(Constants.Count);
        foreach (var constant in Constants)
        {
            writer.Write((byte)constant.Type);
            switch (constant.Type)
            {
                case ConstantType.Int:
                    writer.Write(constant.IntValue);
                    break;
                case ConstantType.Float:
                    writer.Write(constant.FloatValue);
                    break;
                case ConstantType.String:
                    writer.Write(constant.StringIndex);
                    break;
                case ConstantType.Bool:
                    writer.Write(constant.BoolValue ? (byte)1 : (byte)0);
                    break;
            }
        }

        // デバッグ情報を書き込み
        if (DebugInfo.Count > 0)
        {
            uint debugInfoSize = (uint)(4 + 4 + DebugInfo.Count * LineInfo.Size);
            writer.Write(debugInfoSize);
            writer.Write(0); // SourceFilePath placeholder
            writer.Write(DebugInfo.Count);
            foreach (var lineInfo in DebugInfo)
            {
                writer.Write(lineInfo.ToBytes());
            }
        }

        // コードセクションのオフセットを記録
        uint codeOffset = (uint)ms.Position;
        Header = new BytecodeHeader
        {
            Magic = Header.Magic,
            Version = Header.Version,
            FunctionCount = Header.FunctionCount,
            StringTableOffset = Header.StringTableOffset,
            ConstantTableOffset = Header.ConstantTableOffset,
            CodeSize = (uint)Code.Length
        };

        // コードを書き込み
        writer.Write(Code);

        // ヘッダーを更新
        ms.Position = 0;
        writer.Write(Header.ToBytes());

        return ms.ToArray();
    }

    /// <summary>
    /// バイナリからバイトコードファイルをデシリアライズ
    /// </summary>
    public static BytecodeFile FromBytes(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        using var reader = new BinaryReader(ms);

        var file = new BytecodeFile();

        // ヘッダーを読み込み
        byte[] headerBytes = reader.ReadBytes(BytecodeHeader.Size);
        file.Header = BytecodeHeader.FromBytes(headerBytes);

        // 関数テーブルを読み込み
        for (int i = 0; i < file.Header.FunctionCount; i++)
        {
            byte[] funcBytes = reader.ReadBytes(FunctionEntry.Size);
            file.Functions.Add(FunctionEntry.FromBytes(funcBytes, 0));
        }

        // 文字列テーブルを読み込み
        ms.Position = file.Header.StringTableOffset;
        int stringCount = reader.ReadInt32();
        for (int i = 0; i < stringCount; i++)
        {
            int length = reader.ReadInt32();
            byte[] strBytes = reader.ReadBytes(length);
            file.Strings.Add(Encoding.UTF8.GetString(strBytes));
        }

        // 定数テーブルを読み込み
        ms.Position = file.Header.ConstantTableOffset;
        int constantCount = reader.ReadInt32();
        for (int i = 0; i < constantCount; i++)
        {
            ConstantType type = (ConstantType)reader.ReadByte();
            ConstantValue constant = type switch
            {
                ConstantType.Int => ConstantValue.FromInt(reader.ReadInt32()),
                ConstantType.Float => ConstantValue.FromFloat(reader.ReadSingle()),
                ConstantType.String => ConstantValue.FromString(reader.ReadInt32()),
                ConstantType.Bool => ConstantValue.FromBool(reader.ReadByte() != 0),
                _ => throw new InvalidDataException($"Unknown constant type: {type}")
            };
            file.Constants.Add(constant);
        }

        // デバッグ情報があれば読み込み
        if (ms.Position < bytes.Length - file.Header.CodeSize)
        {
            uint debugInfoSize = reader.ReadUInt32();
            if (debugInfoSize > 0)
            {
                // SourceFilePath placeholder (skip)
                reader.ReadUInt32();

                int lineInfoCount = reader.ReadInt32();
                for (int i = 0; i < lineInfoCount; i++)
                {
                    byte[] lineInfoBytes = reader.ReadBytes(LineInfo.Size);
                    file.DebugInfo.Add(LineInfo.FromBytes(lineInfoBytes, 0));
                }
            }
        }

        // コードセクションを読み込み
        long codeOffset = ms.Position;
        file.Code = reader.ReadBytes((int)file.Header.CodeSize);

        return file;
    }

    /// <summary>
    /// 関数名から関数エントリを検索
    /// </summary>
    public FunctionEntry? FindFunction(string name)
    {
        int nameOffset = GetOrAddString(name);
        return Functions.FirstOrDefault(f => f.NameOffset == nameOffset);
    }

    /// <summary>
    /// 文字列を追加またはインデックスを取得
    /// </summary>
    public int GetOrAddString(string str)
    {
        int index = Strings.IndexOf(str);
        if (index >= 0) return index;

        Strings.Add(str);
        return Strings.Count - 1;
    }

    /// <summary>
    /// 定数を追加またはインデックスを取得
    /// </summary>
    public int GetOrAddConstant(ConstantValue constant)
    {
        int index = Constants.IndexOf(constant);
        if (index >= 0) return index;

        Constants.Add(constant);
        return Constants.Count - 1;
    }
}
