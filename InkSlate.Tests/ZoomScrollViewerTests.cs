using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.Input;
using CsCheck;
using InkSlate.App.Controls;
using InkSlate.Core.Models;
using Xunit;

namespace InkSlate.Tests;

public sealed class ZoomScrollViewerTests
{
    private static void RunInSta(Action action) => StaTestRunner.Run(action);

    [Fact]
    public void InitialPropertiesHaveExpectedDefaults()
    {
        RunInSta(() =>
        {
            var viewer = new ZoomScrollViewer();

            Assert.Equal(1.0, viewer.ZoomLevel);
            Assert.Equal(ViewMode.Consecutive, viewer.CurrentViewMode);
            Assert.Equal(ToolMode.Pointer, viewer.ActiveToolMode);
            Assert.True(viewer.IsManipulationEnabled);
            Assert.False(System.Windows.Input.Stylus.GetIsPressAndHoldEnabled(viewer));
            Assert.False(viewer.IsPanning);
            Assert.Equal(0.0, viewer.TargetHorizontalOffset);
            Assert.Equal(0.0, viewer.TargetVerticalOffset);
            Assert.NotNull(viewer.ContentScaleTransform);
            Assert.Equal(1.0, viewer.ContentScaleTransform.ScaleX);
            Assert.Equal(1.0, viewer.ContentScaleTransform.ScaleY);
        });
    }

    [Fact]
    public void ContentLayoutTransformIsSyncedWithContentScaleTransform()
    {
        RunInSta(() =>
        {
            var viewer = new ZoomScrollViewer();
            var content = new Border { Width = 800, Height = 600 };
            viewer.Content = content;

            Assert.Same(viewer.ContentScaleTransform, content.LayoutTransform);

            viewer.ZoomLevel = 2.5;
            Assert.Equal(2.5, viewer.ContentScaleTransform.ScaleX);
            Assert.Equal(2.5, viewer.ContentScaleTransform.ScaleY);
        });
    }

    [Fact]
    public void ZoomLevelClampingEnforcesBounds()
    {
        RunInSta(() =>
        {
            var viewer = new ZoomScrollViewer();

            viewer.ZoomLevel = 0.1;
            Assert.Equal(0.25, viewer.ZoomLevel);

            viewer.ZoomLevel = 10.0;
            Assert.Equal(5.0, viewer.ZoomLevel);

            viewer.ZoomLevel = double.NaN;
            Assert.Equal(1.0, viewer.ZoomLevel);

            viewer.ZoomLevel = double.PositiveInfinity;
            Assert.Equal(1.0, viewer.ZoomLevel);

            viewer.ZoomLevel = double.NegativeInfinity;
            Assert.Equal(1.0, viewer.ZoomLevel);
        });
    }

    [Fact]
    public void HandleMouseWheelZoomCalculatesCorrectFactorsWhenCtrlPressed()
    {
        RunInSta(() =>
        {
            var viewer = new ZoomScrollViewer();

            // Zero delta does nothing
            Assert.False(viewer.HandleMouseWheelZoomOrScroll(0, new Point(50, 50), isControlPressed: true));

            // Positive delta zooms in by factor 1.1
            var handledUp = viewer.HandleMouseWheelZoomOrScroll(120, new Point(50, 50), isControlPressed: true);
            Assert.True(handledUp);
            Assert.Equal(1.1, viewer.ZoomLevel, 3);

            // Negative delta zooms out by factor 0.9
            var handledDown = viewer.HandleMouseWheelZoomOrScroll(-120, new Point(50, 50), isControlPressed: true);
            Assert.True(handledDown);
            Assert.Equal(0.99, viewer.ZoomLevel, 3);
        });
    }

    [Fact]
    public void HandleMouseWheelInSeparatedModeFlipsPagesAtBoundaries()
    {
        RunInSta(() =>
        {
            var viewer = new ZoomScrollViewer
            {
                CurrentViewMode = ViewMode.Separated
            };

            var previousCalled = false;
            var nextCalled = false;

            viewer.PreviousPageCommand = new RelayCommand(() => previousCalled = true);
            viewer.NextPageCommand = new RelayCommand(() => nextCalled = true);

            // When at top (VerticalOffset = 0), wheel up flips to previous page
            var handledPrev = viewer.HandleMouseWheelZoomOrScroll(120, new Point(50, 50), isControlPressed: false);
            Assert.True(handledPrev);
            Assert.True(previousCalled);

            // When at bottom (VerticalOffset >= ScrollableHeight - 0.5), wheel down flips to next page
            var handledNext = viewer.HandleMouseWheelZoomOrScroll(-120, new Point(50, 50), isControlPressed: false);
            Assert.True(handledNext);
            Assert.True(nextCalled);
        });
    }

    [Fact]
    public void HandleMouseWheelInConsecutiveModeDoesNotInterceptNormalScroll()
    {
        RunInSta(() =>
        {
            var viewer = new ZoomScrollViewer
            {
                CurrentViewMode = ViewMode.Consecutive
            };

            var handled = viewer.HandleMouseWheelZoomOrScroll(120, new Point(50, 50), isControlPressed: false);
            Assert.False(handled);
        });
    }

    [Fact]
    public void PanningLifecycleUpdatesOffsetsAndClampsAtZero()
    {
        RunInSta(() =>
        {
            var viewer = new ZoomScrollViewer();
            viewer.ApplyScrollOffsets(100.0, 100.0);

            viewer.BeginPan(new Point(50, 50));
            Assert.True(viewer.IsPanning);

            // Drag mouse left/up by 20/30 -> viewport scrolls right/down
            var updated = viewer.UpdatePan(new Point(30, 20));
            Assert.True(updated);
            Assert.Equal(120.0, viewer.TargetHorizontalOffset);
            Assert.Equal(130.0, viewer.TargetVerticalOffset);

            // Drag mouse far right/down -> viewport scrolls left/up to 0 (clamped)
            viewer.UpdatePan(new Point(300, 300));
            Assert.Equal(0.0, viewer.TargetHorizontalOffset);
            Assert.Equal(0.0, viewer.TargetVerticalOffset);

            viewer.EndPan();
            Assert.False(viewer.IsPanning);
        });
    }

    [Fact]
    public void ZoomAtPointCalculatesAnchorOffsetsAccurately()
    {
        RunInSta(() =>
        {
            var viewer = new ZoomScrollViewer();
            viewer.ApplyScrollOffsets(200.0, 100.0);

            // Initial: Zoom = 1.0, Offset = (200, 100)
            // Anchor = (50, 50)
            // Zoom multiplier = 2.0 (New Zoom = 2.0)
            // ratio = 2.0
            // expected H = (200 + 50) * 2 - 50 = 450
            // expected V = (100 + 50) * 2 - 50 = 250
            viewer.ZoomAtPoint(2.0, new Point(50, 50));

            Assert.Equal(2.0, viewer.ZoomLevel);
            Assert.Equal(450.0, viewer.TargetHorizontalOffset);
            Assert.Equal(250.0, viewer.TargetVerticalOffset);
        });
    }

    [Fact]
    public void CsCheckPropertyFuzzing_ZoomAtPointAlwaysMaintainsValidBoundsAndOffsets()
    {
        Gen.Select(
            Gen.Double[-10.0, 10.0],
            Gen.Double[-500.0, 2000.0],
            Gen.Double[-500.0, 2000.0])
        .Sample((multiplier, anchorX, anchorY) =>
        {
            RunInSta(() =>
            {
                var viewer = new ZoomScrollViewer();
                viewer.ZoomAtPoint(multiplier, new Point(anchorX, anchorY));

                Assert.True(viewer.ZoomLevel >= 0.25);
                Assert.True(viewer.ZoomLevel <= 5.0);
                Assert.True(double.IsFinite(viewer.ZoomLevel));
                Assert.True(viewer.TargetHorizontalOffset >= 0.0);
                Assert.True(double.IsFinite(viewer.TargetHorizontalOffset));
                Assert.True(viewer.TargetVerticalOffset >= 0.0);
                Assert.True(double.IsFinite(viewer.TargetVerticalOffset));
            });
        });
    }
}
