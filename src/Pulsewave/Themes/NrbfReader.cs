using System.IO;
using System.Text;

namespace Pulsewave.Themes;

/// <summary>A class record from a .NET binary-serialized stream, as plain data.</summary>
internal sealed class NrbfObject
{
    public string TypeName { get; init; } = "";
    public Dictionary<string, object?> Members { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Member by its simple name. Auto-properties are serialized as "&lt;Name&gt;k__BackingField",
    /// and inherited ones as "Base+&lt;Name&gt;k__BackingField"; all three spellings match.
    /// </summary>
    public object? Get(string name)
    {
        if (Members.TryGetValue(name, out var direct)) return direct;
        string backing = "<" + name + ">k__BackingField";
        foreach (var (key, value) in Members)
            if (key.EndsWith(backing, StringComparison.Ordinal) || key.EndsWith("+" + name, StringComparison.Ordinal))
                return value;
        return null;
    }

    public string? String(string name) => Get(name) as string;
    public NrbfObject? Object(string name) => Get(name) as NrbfObject;

    public double Number(string name, double fallback = 0) => Get(name) switch
    {
        int i => i, long l => l, double d => d, float f => f, short s => s, byte b => b,
        uint u => u, ushort us => us, ulong ul => ul, sbyte sb => sb,
        NrbfObject { TypeName: var t } e when e.Get("value__") is { } v && !t.StartsWith("System.Drawing.Color", StringComparison.Ordinal)
            => Convert.ToDouble(v, System.Globalization.CultureInfo.InvariantCulture),
        _ => fallback
    };

    public bool Bool(string name) => Get(name) is true;
}

/// <summary>
/// Reads the MS-NRBF format written by .NET BinaryFormatter (used by L-Connect .template
/// files) as plain data. It never resolves or instantiates the named types, which is what
/// makes BinaryFormatter itself unsafe; it only builds dictionaries, lists and primitives.
/// Every length and count is bounded by the input size and fixed limits.
/// </summary>
internal sealed class NrbfReader
{
    public const long MaxInputBytes = 256L * 1024 * 1024;
    private const int MaxRecords = 2_000_000;
    private const int MaxDepth = 64;

    private sealed record ClassInfo(string Name, string[] Members, (byte Type, object? Info)[]? Types);
    private sealed record Reference(int Id);
    private sealed class NullRun(int count) { public int Count { get; } = count; }

    private readonly BinaryReader _in;
    private readonly long _length;
    private readonly Dictionary<int, object?> _objects = new();
    private readonly Dictionary<int, ClassInfo> _classes = new();
    private int _records;

    private NrbfReader(Stream stream)
    {
        _in = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        _length = stream.Length;
    }

    /// <summary>Parses the root object; references are replaced by the objects they name.</summary>
    public static object? Read(Stream stream)
    {
        if (!stream.CanSeek || stream.Length > MaxInputBytes) throw new InvalidDataException("Theme file is too large.");
        var reader = new NrbfReader(stream);
        try
        {
            if (reader._in.ReadByte() != 0) throw new InvalidDataException("Not a .NET binary-serialized theme.");
            int rootId = reader._in.ReadInt32();
            reader._in.ReadBytes(12); // header id and format version
            while (true)
            {
                var record = reader.ReadRecord(0);
                if (record is End) break;
            }
            if (!reader._objects.TryGetValue(rootId, out var root)) throw new InvalidDataException("Theme root object is missing.");
            reader.Resolve();
            return root;
        }
        catch (EndOfStreamException ex) { throw new InvalidDataException("Theme file is truncated.", ex); }
    }

    private sealed class End { public static readonly End Instance = new(); }

    private object? ReadRecord(int depth)
    {
        if (++_records > MaxRecords || depth > MaxDepth) throw new InvalidDataException("Theme file structure is too large or too deep.");
        byte type = _in.ReadByte();
        switch (type)
        {
            case 1: // ClassWithId: reuses the layout of an earlier class record
            {
                int id = _in.ReadInt32(), metadataId = _in.ReadInt32();
                if (!_classes.TryGetValue(metadataId, out var info)) throw new InvalidDataException("Unknown class reference.");
                return ReadMembers(id, info, depth);
            }
            case 2: case 3: // members without type information: each value is a record
            {
                var (id, name, members) = ReadClassInfo();
                if (type == 3) _in.ReadInt32(); // library id
                var info = new ClassInfo(name, members, null);
                _classes[id] = info;
                return ReadMembers(id, info, depth);
            }
            case 4: case 5:
            {
                var (id, name, members) = ReadClassInfo();
                var types = ReadMemberTypes(members.Length);
                if (type == 5) _in.ReadInt32(); // library id
                var info = new ClassInfo(name, members, types);
                _classes[id] = info;
                return ReadMembers(id, info, depth);
            }
            case 6: { int id = _in.ReadInt32(); string text = ReadString(); _objects[id] = text; return text; }
            case 7: return ReadBinaryArray(depth);
            case 8: return ReadPrimitive(_in.ReadByte());
            case 9: return new Reference(_in.ReadInt32());
            case 10: return null;
            case 11: return End.Instance;
            case 12: _in.ReadInt32(); ReadString(); return ReadRecord(depth); // library declaration precedes a record
            case 13: return new NullRun(_in.ReadByte());
            case 14: return new NullRun(Count(_in.ReadInt32()));
            case 15: // ArraySinglePrimitive
            {
                int id = _in.ReadInt32(), length = Count(_in.ReadInt32());
                byte primitive = _in.ReadByte();
                object array = primitive == 2 ? ReadBytes(length) : ReadPrimitives(primitive, length);
                _objects[id] = array;
                return array;
            }
            case 16: case 17: // ArraySingleObject / ArraySingleString
            {
                int id = _in.ReadInt32(), length = Count(_in.ReadInt32());
                var list = new List<object?>();
                _objects[id] = list;
                FillElements(list, length, 2, null, depth);
                return list;
            }
            default: throw new InvalidDataException($"Unsupported record type {type} in theme file.");
        }
    }

    private (int Id, string Name, string[] Members) ReadClassInfo()
    {
        int id = _in.ReadInt32();
        string name = ReadString();
        int count = Count(_in.ReadInt32());
        var members = new string[count];
        for (int i = 0; i < count; i++) members[i] = ReadString();
        return (id, name, members);
    }

    private (byte, object?)[] ReadMemberTypes(int count)
    {
        var kinds = new byte[count];
        for (int i = 0; i < count; i++) kinds[i] = _in.ReadByte();
        var types = new (byte, object?)[count];
        for (int i = 0; i < count; i++) types[i] = (kinds[i], ReadTypeInfo(kinds[i]));
        return types;
    }

    private object? ReadTypeInfo(byte kind) => kind switch
    {
        0 or 7 => _in.ReadByte(),             // primitive type
        3 => ReadString(),                    // system class name
        4 => (ReadString(), _in.ReadInt32()), // class name + library
        _ => null
    };

    private NrbfObject ReadMembers(int id, ClassInfo info, int depth)
    {
        var obj = new NrbfObject { TypeName = info.Name };
        _objects[id] = obj;
        for (int i = 0; i < info.Members.Length; i++)
        {
            object? value = info.Types is { } types && types[i].Type == 0
                ? ReadPrimitive((byte)types[i].Info!)
                : ReadRecord(depth + 1);
            if (value is NullRun or End) throw new InvalidDataException("Malformed class member in theme file.");
            obj.Members[info.Members[i]] = value;
        }
        return obj;
    }

    private object ReadBinaryArray(int depth)
    {
        int id = _in.ReadInt32();
        byte shape = _in.ReadByte();
        int rank = _in.ReadInt32();
        if (rank is < 1 or > 8) throw new InvalidDataException("Unsupported array rank in theme file.");
        long total = 1;
        for (int i = 0; i < rank; i++) total *= Count(_in.ReadInt32());
        if (shape is 3 or 4 or 5) for (int i = 0; i < rank; i++) _in.ReadInt32(); // lower bounds
        byte kind = _in.ReadByte();
        object? info = ReadTypeInfo(kind);
        int length = Count(total > int.MaxValue ? -1 : (int)total);
        if (kind == 7 && info is byte primitive) // array of primitive arrays is still records; a flat primitive array is not
        {
            var nested = new List<object?>();
            _objects[id] = nested;
            FillElements(nested, length, kind, info, depth);
            return nested;
        }
        if (kind == 0)
        {
            object array = (byte)info! == 2 ? ReadBytes(length) : ReadPrimitives((byte)info!, length);
            _objects[id] = array;
            return array;
        }
        var list = new List<object?>();
        _objects[id] = list;
        FillElements(list, length, kind, info, depth);
        return list;
    }

    private void FillElements(List<object?> list, int length, byte kind, object? info, int depth)
    {
        while (list.Count < length)
        {
            var value = ReadRecord(depth + 1);
            if (value is End) throw new InvalidDataException("Theme array ends early.");
            if (value is NullRun run)
            {
                if (run.Count > length - list.Count) throw new InvalidDataException("Malformed theme array.");
                for (int i = 0; i < run.Count; i++) list.Add(null);
            }
            else list.Add(value);
        }
    }

    private object?[] ReadPrimitives(byte type, int length)
    {
        var values = new object?[length];
        for (int i = 0; i < length; i++) values[i] = ReadPrimitive(type);
        return values;
    }

    private object? ReadPrimitive(byte type) => type switch
    {
        1 => _in.ReadBoolean(),
        2 => _in.ReadByte(),
        3 => ReadChar(),
        5 => ReadString(), // decimal as text
        6 => _in.ReadDouble(),
        7 => _in.ReadInt16(),
        8 => _in.ReadInt32(),
        9 => _in.ReadInt64(),
        10 => _in.ReadSByte(),
        11 => _in.ReadSingle(),
        12 or 13 => _in.ReadInt64(), // TimeSpan ticks / DateTime
        14 => _in.ReadUInt16(),
        15 => _in.ReadUInt32(),
        16 => _in.ReadUInt64(),
        17 => null,
        18 => ReadString(),
        _ => throw new InvalidDataException($"Unsupported primitive type {type} in theme file.")
    };

    private char ReadChar()
    {
        // UTF-8 encoded single character.
        byte first = _in.ReadByte();
        int extra = first < 0x80 ? 0 : first < 0xE0 ? 1 : first < 0xF0 ? 2 : 3;
        var bytes = new byte[1 + extra]; bytes[0] = first;
        for (int i = 1; i <= extra; i++) bytes[i] = _in.ReadByte();
        var text = Encoding.UTF8.GetString(bytes);
        return text.Length > 0 ? text[0] : '\0';
    }

    private string ReadString()
    {
        int length = 0, shift = 0;
        while (true)
        {
            byte b = _in.ReadByte();
            length |= (b & 0x7F) << shift;
            if ((b & 0x80) == 0) break;
            shift += 7;
            if (shift > 28) throw new InvalidDataException("Malformed string length in theme file.");
        }
        return Encoding.UTF8.GetString(ReadBytes(length));
    }

    private byte[] ReadBytes(int length)
    {
        var bytes = _in.ReadBytes(Count(length));
        if (bytes.Length != length) throw new EndOfStreamException();
        return bytes;
    }

    // A count can never exceed the bytes left in the stream, which bounds every allocation.
    private int Count(int value)
    {
        if (value < 0 || value > _length - _in.BaseStream.Position)
            throw new InvalidDataException("Theme file declares a length larger than the file.");
        return value;
    }

    private void Resolve()
    {
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        foreach (var value in _objects.Values.ToList()) ResolveIn(value, visited);
    }

    private void ResolveIn(object? value, HashSet<object> visited)
    {
        if (value is null || !visited.Add(value)) return;
        if (value is NrbfObject obj)
            foreach (var key in obj.Members.Keys.ToList())
            {
                obj.Members[key] = Target(obj.Members[key]);
                ResolveIn(obj.Members[key], visited);
            }
        else if (value is List<object?> list)
            for (int i = 0; i < list.Count; i++)
            {
                list[i] = Target(list[i]);
                ResolveIn(list[i], visited);
            }
    }

    private object? Target(object? value) =>
        value is Reference r ? (_objects.TryGetValue(r.Id, out var target) ? target : null) : value;
}
