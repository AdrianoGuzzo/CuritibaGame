using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

namespace Curitiba.Core.BeatEmUp
{
    /// <summary>
    /// Minimal POCOs for the subset of the Tiled JSON map format (<c>.tmj</c>) the importer reads:
    /// map/layer custom properties, object groups (spawns/set pieces/walkzone) and image layers
    /// (backgrounds). Everything else in the file is ignored. Parsed with System.Text.Json using
    /// <see cref="StageLoader.JsonOptions"/> (case-insensitive), so Tiled's lower-case keys bind.
    /// </summary>
    public sealed class TmjMap
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int TileWidth { get; set; }
        public int TileHeight { get; set; }
        public List<TmjProperty> Properties { get; set; } = new List<TmjProperty>();
        public List<TmjLayer> Layers { get; set; } = new List<TmjLayer>();
    }

    public sealed class TmjLayer
    {
        public string Type { get; set; }    // "objectgroup", "imagelayer", "tilelayer"
        public string Name { get; set; }
        public string Image { get; set; }    // image layers
        public List<TmjObject> Objects { get; set; } = new List<TmjObject>();
        public List<TmjProperty> Properties { get; set; } = new List<TmjProperty>();
    }

    public sealed class TmjObject
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string Class { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public bool Point { get; set; }
        public List<TmjProperty> Properties { get; set; } = new List<TmjProperty>();
    }

    public sealed class TmjProperty
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public JsonElement Value { get; set; }

        public string AsString() =>
            Value.ValueKind == JsonValueKind.String ? Value.GetString()
            : Value.ValueKind == JsonValueKind.Undefined ? null
            : Value.ToString();

        public int AsInt()
        {
            if (Value.ValueKind == JsonValueKind.Number)
                return Value.TryGetInt32(out int i) ? i : (int)Value.GetDouble();
            if (Value.ValueKind == JsonValueKind.String && int.TryParse(Value.GetString(), out int j))
                return j;
            return 0;
        }

        public float AsFloat()
        {
            if (Value.ValueKind == JsonValueKind.Number)
                return (float)Value.GetDouble();
            if (Value.ValueKind == JsonValueKind.String &&
                float.TryParse(Value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out float f))
                return f;
            return 0f;
        }

        public bool AsBool()
        {
            if (Value.ValueKind == JsonValueKind.True) return true;
            if (Value.ValueKind == JsonValueKind.False) return false;
            if (Value.ValueKind == JsonValueKind.String && bool.TryParse(Value.GetString(), out bool b)) return b;
            return false;
        }
    }
}
