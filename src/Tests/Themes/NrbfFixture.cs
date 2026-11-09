using System.IO;
using System.Text;

namespace Pulsewave.Regression;

// Writes small MS-NRBF streams shaped like L-Connect templates, so importer checks need no
// third-party theme files. Only the record kinds the importer must handle are produced.
internal sealed class NrbfFixture
{
    private readonly MemoryStream _stream = new();
    private readonly BinaryWriter _w;
    private int _nextId = 1;

    public NrbfFixture()
    {
        _w = new BinaryWriter(_stream, Encoding.UTF8);
        _w.Write((byte)0); _w.Write(1); _w.Write(-1); _w.Write(1); _w.Write(0); // header, root id 1
        _w.Write((byte)12); _w.Write(2); Str("lianli.ThemeEngine, Version=1.2.0.135");
    }

    public int NewId() => ++_nextId + 100;

    public void Str(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        int n = bytes.Length;
        while (n >= 0x80) { _w.Write((byte)(n | 0x80)); n >>= 7; }
        _w.Write((byte)n);
        _w.Write(bytes);
    }

    // Member type kinds: 0 primitive (with primitive code), 1 string, 2 object, 3 system class.
    public void ClassWithTypes(int id, string name, (string Member, byte Kind, object? Info)[] members, bool system = false)
    {
        _w.Write((byte)(system ? 4 : 5)); _w.Write(id); Str(name); _w.Write(members.Length);
        foreach (var m in members) Str(m.Member);
        foreach (var m in members) _w.Write(m.Kind);
        foreach (var m in members)
            if (m.Kind == 0) _w.Write((byte)m.Info!);
            else if (m.Kind == 3) Str((string)m.Info!);
        if (!system) _w.Write(2); // library id
    }

    public void ClassWithId(int id, int metadataId) { _w.Write((byte)1); _w.Write(id); _w.Write(metadataId); }
    public void String(string text) { _w.Write((byte)6); _w.Write(NewId()); Str(text); }
    public void Null() => _w.Write((byte)10);
    public void Reference(int id) { _w.Write((byte)9); _w.Write(id); }
    public void ObjectArray(int id, int length) { _w.Write((byte)16); _w.Write(id); _w.Write(length); }
    public void Int(int value) => _w.Write(value);
    public void Long(long value) => _w.Write(value);
    public void Short(short value) => _w.Write(value);
    public void Bool(bool value) => _w.Write(value);
    public void Raw(byte[] bytes) => _w.Write(bytes);

    public byte[] Finish()
    {
        _w.Write((byte)11);
        _w.Flush();
        return _stream.ToArray();
    }

    /// <summary>A two-element theme: a centered CPUTEMP Data item and a "CPU" Text item sharing one font.</summary>
    public static byte[] Theme(string name = "Fixture Theme", int width = 1920, int height = 480)
    {
        var f = new NrbfFixture();
        f.ClassWithTypes(1, "ThemeEngine.Theme",
        [
            ("<name>k__BackingField", 1, null), ("<width>k__BackingField", 0, (byte)8),
            ("<height>k__BackingField", 0, (byte)8), ("<GraphList>k__BackingField", 2, null)
        ]);
        f.String(name); f.Int(width); f.Int(height);
        f.ObjectArray(3, 2);

        // Item 1: full class definitions.
        f.ClassWithTypes(10, "ThemeEngine.GraphItem",
        [
            ("GraphItem+<TypeName>k__BackingField", 1, null), ("GraphItem+<posX>k__BackingField", 0, (byte)8),
            ("GraphItem+<posY>k__BackingField", 0, (byte)8), ("GraphItem+<m_data>k__BackingField", 2, null),
            ("GraphItem+<fontConfig>k__BackingField", 2, null)
        ]);
        f.String("Data"); f.Int(207); f.Int(218);
        f.ClassWithTypes(11, "ThemeEngine.M_Data",
            [("<DataName>k__BackingField", 1, null), ("<ShowUnit>k__BackingField", 0, (byte)1), ("_Value", 1, null)]);
        f.String("CPUTEMP"); f.Bool(true); f.String("88");
        f.ClassWithTypes(12, "ThemeEngine.FontConfig",
        [
            ("<name>k__BackingField", 1, null), ("<size>k__BackingField", 0, (byte)8), ("<isBold>k__BackingField", 0, (byte)1),
            ("<color>k__BackingField", 3, "System.Drawing.Color"), ("<alignment>k__BackingField", 2, null)
        ]);
        f.String("Digital-7"); f.Int(68); f.Bool(false);
        f.ClassWithTypes(13, "System.Drawing.Color",
            [("name", 1, null), ("value", 0, (byte)9), ("knownColor", 0, (byte)7), ("state", 0, (byte)7)], system: true);
        f.Null(); f.Long(0xFFFFAA5F); f.Short(0); f.Short(2);
        f.ClassWithTypes(14, "ThemeEngine.TextAlignment", [("<index>k__BackingField", 0, (byte)8)]);
        f.Int(1);

        // Item 2: reuses the class layouts by id and shares item 1's font by reference.
        f.ClassWithId(20, 10);
        f.String("Text"); f.Int(100); f.Int(20);
        f.ClassWithId(21, 11);
        f.Null(); f.Bool(false); f.String("CPU");
        f.Reference(12);
        return f.Finish();
    }
}
