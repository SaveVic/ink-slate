using System.Text.Json.Serialization;

namespace InkSlate.Core.Models.CanvasElements;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(TextCanvasElement), typeDiscriminator: "text")]
[JsonDerivedType(typeof(ImageCanvasElement), typeDiscriminator: "image")]
public abstract class CanvasElement
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 200.0;
    public double Height { get; set; } = 100.0;
    public int ZIndex { get; set; }
}

