using System.Windows.Ink;
using System.Windows.Input;

namespace InkSlate.Core.Ink;

public static class StrokeSplitter
{
    public const int MaxPointsPerStroke = 10_000;

    public static bool IsOversized(Stroke stroke, int maxPoints = MaxPointsPerStroke)
    {
        ArgumentNullException.ThrowIfNull(stroke);
        return stroke.StylusPoints.Count > maxPoints;
    }

    public static IReadOnlyList<Stroke> Split(Stroke stroke, int maxPoints = MaxPointsPerStroke)
    {
        ArgumentNullException.ThrowIfNull(stroke);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxPoints, 2);

        var source = stroke.StylusPoints;
        if (source.Count <= maxPoints)
        {
            return [stroke];
        }

        var step = maxPoints - 1;
        var chunkCount = (source.Count - 1 + step - 1) / step;
        var chunks = new List<Stroke>(chunkCount);

        for (var start = 0; start < source.Count - 1; start += step)
        {
            var end = Math.Min(start + maxPoints - 1, source.Count - 1);
            var count = end - start + 1;
            if (count < 2)
            {
                break;
            }

            var chunkPoints = new StylusPointCollection(source.Description, count);
            for (var i = start; i <= end; i++)
            {
                chunkPoints.Add(source[i]);
            }

            chunks.Add(new Stroke(chunkPoints, stroke.DrawingAttributes.Clone()));
        }

        return chunks;
    }

    public static StrokeCollection SplitAll(StrokeCollection source, int maxPoints = MaxPointsPerStroke)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxPoints, 2);

        var result = new StrokeCollection();
        foreach (var stroke in source)
        {
            if (stroke.StylusPoints.Count <= maxPoints)
            {
                result.Add(stroke);
            }
            else
            {
                var chunks = Split(stroke, maxPoints);
                foreach (var chunk in chunks)
                {
                    result.Add(chunk);
                }
            }
        }

        return result;
    }
}

