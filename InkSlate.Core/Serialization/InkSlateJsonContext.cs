using System.Text.Json.Serialization;
using InkSlate.Core.Models;
using InkSlate.Core.Models.CanvasElements;

namespace InkSlate.Core.Serialization;

[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
    MaxDepth = 16)]
[JsonSerializable(typeof(NoteDocument))]
[JsonSerializable(typeof(NotePage))]
[JsonSerializable(typeof(CanvasElement))]
[JsonSerializable(typeof(TextCanvasElement))]
[JsonSerializable(typeof(ImageCanvasElement))]
[JsonSerializable(typeof(UserSettings))]
[JsonSerializable(typeof(CustomPenPreset))]
[JsonSerializable(typeof(CanvasClipboardPayload))]
public partial class InkSlateJsonContext : JsonSerializerContext;

