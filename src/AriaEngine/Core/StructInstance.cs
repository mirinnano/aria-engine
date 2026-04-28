using System;
using System.Buffers.Binary;
using System.Text;

namespace AriaEngine.Core;

/// <summary>
/// 構造体インスタンス（バイト配列ベース）
/// </summary>
public class StructInstance
{
    private readonly byte[] _data;
    private readonly StructDefinition _definition;
    
    public StructDefinition Definition => _definition;
    public int Size => _data.Length;
    public byte[] RawData => _data;
    
    public StructInstance(StructDefinition definition)
    {
        _definition = definition;
        _data = new byte[definition.TotalSize];
    }
    
    /// <summary>
    /// int型フィールドを設定
    /// </summary>
    public void SetInt(string fieldName, int value)
    {
        var field = _definition.GetField(fieldName);
        if (field == null) return;
        
        BinaryPrimitives.WriteInt32LittleEndian(_data.AsSpan(field.Offset), value);
    }
    
    /// <summary>
    /// int型フィールドを取得
    /// </summary>
    public int GetInt(string fieldName)
    {
        var field = _definition.GetField(fieldName);
        if (field == null) return 0;
        
        return BinaryPrimitives.ReadInt32LittleEndian(_data.AsSpan(field.Offset));
    }
    
    /// <summary>
    /// float型フィールドを設定
    /// </summary>
    public void SetFloat(string fieldName, float value)
    {
        var field = _definition.GetField(fieldName);
        if (field == null) return;
        
        BinaryPrimitives.WriteSingleLittleEndian(_data.AsSpan(field.Offset), value);
    }
    
    /// <summary>
    /// float型フィールドを取得
    /// </summary>
    public float GetFloat(string fieldName)
    {
        var field = _definition.GetField(fieldName);
        if (field == null) return 0f;
        
        return BinaryPrimitives.ReadSingleLittleEndian(_data.AsSpan(field.Offset));
    }
    
    /// <summary>
    /// bool型フィールドを設定
    /// </summary>
    public void SetBool(string fieldName, bool value)
    {
        var field = _definition.GetField(fieldName);
        if (field == null) return;
        
        _data[field.Offset] = value ? (byte)1 : (byte)0;
    }
    
    /// <summary>
    /// bool型フィールドを取得
    /// </summary>
    public bool GetBool(string fieldName)
    {
        var field = _definition.GetField(fieldName);
        if (field == null) return false;
        
        return _data[field.Offset] != 0;
    }
    
    /// <summary>
    /// string型フィールドを設定（固定長256バイト）
    /// </summary>
    public void SetString(string fieldName, string value)
    {
        var field = _definition.GetField(fieldName);
        if (field == null) return;
        
        var bytes = Encoding.UTF8.GetBytes(value);
        int length = Math.Min(bytes.Length, field.Size - 1);
        bytes.AsSpan(0, length).CopyTo(_data.AsSpan(field.Offset));
        _data[field.Offset + length] = 0; // null終端
    }
    
    /// <summary>
    /// string型フィールドを取得
    /// </summary>
    public string GetString(string fieldName)
    {
        var field = _definition.GetField(fieldName);
        if (field == null) return "";
        
        int length = 0;
        while (length < field.Size && _data[field.Offset + length] != 0)
            length++;
        
        return Encoding.UTF8.GetString(_data, field.Offset, length);
    }
}
