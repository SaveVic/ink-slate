using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using CsCheck;
using InkSlate.Core.Ink;
using Xunit;

namespace InkSlate.Tests;

public sealed class StrokeSplitterTests
{
    private static void RunInSta(Action action) => StaTestRunner.Run(action);

    [Fact]
    public void StrokeWithinCapIsReturnedUnchanged()
    {
        RunInSta(() =>
        {
            var points = new StylusPointCollection();
            for (var i = 0; i < 500; i++)
            {
                points.Add(new StylusPoint(i, i));
            }

            var stroke = new Stroke(points);
            Assert.False(StrokeSplitter.IsOversized(stroke));

            var result = StrokeSplitter.Split(stroke);
            Assert.Single(result);
            Assert.Same(stroke, result[0]);
        });
    }

    [Fact]
    public void StrokeExactlyAtCapIsReturnedUnchanged()
    {
        RunInSta(() =>
        {
            var points = new StylusPointCollection();
            for (var i = 0; i < 100; i++)
            {
                points.Add(new StylusPoint(i, i));
            }

            var stroke = new Stroke(points);
            var result = StrokeSplitter.Split(stroke, maxPoints: 100);

            Assert.Single(result);
            Assert.Same(stroke, result[0]);
        });
    }

    [Fact]
    public void StrokeOverCapSplitsIntoContinuationChunksWithSharedOverlapPoint()
    {
        RunInSta(() =>
        {
            // 25 points, max 10 points per chunk -> step = 9 -> chunks: [0..9] (10), [9..18] (10), [18..24] (7) = 3 chunks
            var points = new StylusPointCollection();
            for (var i = 0; i < 25; i++)
            {
                points.Add(new StylusPoint(i * 2.0, i * 3.0));
            }

            var stroke = new Stroke(points);
            Assert.True(StrokeSplitter.IsOversized(stroke, maxPoints: 10));

            var chunks = StrokeSplitter.Split(stroke, maxPoints: 10);
            Assert.Equal(3, chunks.Count);

            Assert.Equal(10, chunks[0].StylusPoints.Count);
            Assert.Equal(10, chunks[1].StylusPoints.Count);
            Assert.Equal(7, chunks[2].StylusPoints.Count);

            // Overlap check: chunk 1 starts on chunk 0's last point
            Assert.Equal(chunks[0].StylusPoints[^1].X, chunks[1].StylusPoints[0].X);
            Assert.Equal(chunks[0].StylusPoints[^1].Y, chunks[1].StylusPoints[0].Y);

            // Overlap check: chunk 2 starts on chunk 1's last point
            Assert.Equal(chunks[1].StylusPoints[^1].X, chunks[2].StylusPoints[0].X);
            Assert.Equal(chunks[1].StylusPoints[^1].Y, chunks[2].StylusPoints[0].Y);

            // First point of chunk 0 is original point 0, last point of chunk 2 is original point 24
            Assert.Equal(0.0, chunks[0].StylusPoints[0].X);
            Assert.Equal(48.0, chunks[2].StylusPoints[^1].X);
        });
    }

    [Fact]
    public void ChunksPreservePressureDescriptionAndCloneDrawingAttributes()
    {
        RunInSta(() =>
        {
            var points = new StylusPointCollection();
            for (var i = 0; i < 15; i++)
            {
                points.Add(new StylusPoint(i, i, 0.25f + (i * 0.04f)));
            }

            var da = new DrawingAttributes
            {
                Color = Colors.DarkBlue,
                Width = 4.5,
                FitToCurve = true
            };
            var stroke = new Stroke(points, da);

            var chunks = StrokeSplitter.Split(stroke, maxPoints: 8);
            Assert.Equal(2, chunks.Count);

            foreach (var chunk in chunks)
            {
                Assert.NotSame(da, chunk.DrawingAttributes);
                Assert.Equal(Colors.DarkBlue, chunk.DrawingAttributes.Color);
                Assert.Equal(4.5, chunk.DrawingAttributes.Width);
                Assert.True(chunk.DrawingAttributes.FitToCurve);
            }

            // Pressure preservation
            Assert.Equal(points[0].PressureFactor, chunks[0].StylusPoints[0].PressureFactor);
            Assert.Equal(points[7].PressureFactor, chunks[0].StylusPoints[7].PressureFactor);
            Assert.Equal(points[7].PressureFactor, chunks[1].StylusPoints[0].PressureFactor);
            Assert.Equal(points[^1].PressureFactor, chunks[1].StylusPoints[^1].PressureFactor);
        });
    }

    [Fact]
    public void SplitAllLeavesNormalStrokesAndSplitsOversizedOnes()
    {
        RunInSta(() =>
        {
            var normal1 = new Stroke(new StylusPointCollection { new StylusPoint(0, 0), new StylusPoint(5, 5) });
            var normal2 = new Stroke(new StylusPointCollection { new StylusPoint(10, 10), new StylusPoint(15, 15) });

            var oversizedPoints = new StylusPointCollection();
            for (var i = 0; i < 20; i++)
            {
                oversizedPoints.Add(new StylusPoint(i, i));
            }
            var oversized = new Stroke(oversizedPoints);

            var collection = new StrokeCollection { normal1, oversized, normal2 };
            var result = StrokeSplitter.SplitAll(collection, maxPoints: 8);

            // normal1 (1) + oversized split into 3 chunks + normal2 (1) = 5
            Assert.Equal(5, result.Count);
            Assert.Same(normal1, result[0]);
            Assert.Same(normal2, result[4]);
            Assert.All(result, s => Assert.True(s.StylusPoints.Count <= 8));
        });
    }

    [Fact]
    public void InvalidMaxPointsThrows()
    {
        RunInSta(() =>
        {
            var stroke = new Stroke(new StylusPointCollection { new StylusPoint(0, 0), new StylusPoint(1, 1) });
            Assert.Throws<ArgumentOutOfRangeException>(() => StrokeSplitter.Split(stroke, maxPoints: 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => StrokeSplitter.Split(stroke, maxPoints: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => StrokeSplitter.Split(stroke, maxPoints: -5));

            var collection = new StrokeCollection { stroke };
            Assert.Throws<ArgumentOutOfRangeException>(() => StrokeSplitter.SplitAll(collection, maxPoints: 1));
        });
    }

    [Fact]
    public void CsCheckPropertyFuzzing_SplitIsLosslessAndBounded()
    {
        var countGen = Gen.Int[2, 5_000];
        var maxPointsGen = Gen.Int[2, 500];

        Gen.Select(countGen, maxPointsGen).Sample((n, maxPoints) =>
        {
            RunInSta(() =>
            {
                var points = new StylusPointCollection();
                for (var i = 0; i < n; i++)
                {
                    points.Add(new StylusPoint(i, i * 2.0));
                }

                var stroke = new Stroke(points);
                var chunks = StrokeSplitter.Split(stroke, maxPoints);

                if (n <= maxPoints)
                {
                    Assert.Single(chunks);
                    Assert.Same(stroke, chunks[0]);
                    return;
                }

                var step = maxPoints - 1;
                var expectedChunkCount = (n - 1 + step - 1) / step;
                Assert.Equal(expectedChunkCount, chunks.Count);

                // Reconstruct points by skipping overlap points
                var reconstructed = new List<StylusPoint>();
                for (var c = 0; c < chunks.Count; c++)
                {
                    var chunkPoints = chunks[c].StylusPoints;
                    Assert.InRange(chunkPoints.Count, 2, maxPoints);

                    var startIndex = c == 0 ? 0 : 1;
                    for (var p = startIndex; p < chunkPoints.Count; p++)
                    {
                        reconstructed.Add(chunkPoints[p]);
                    }
                }

                Assert.Equal(n, reconstructed.Count);
                for (var i = 0; i < n; i++)
                {
                    Assert.Equal(points[i].X, reconstructed[i].X);
                    Assert.Equal(points[i].Y, reconstructed[i].Y);
                }
            });
        });
    }
}

