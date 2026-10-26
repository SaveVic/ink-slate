using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using CsCheck;
using InkSlate.App.ViewModels;
using InkSlate.App.Views;
using InkSlate.Core.Models;
using Xunit;

namespace InkSlate.Tests;

public sealed class ViewportAndPaginationTests
{
    private static void RunInSta(Action action) => StaTestRunner.Run(action);

    private static void FlushDispatcher() =>
        System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
            () => { },
            System.Windows.Threading.DispatcherPriority.Background);

    [Fact]
    public void HydrateAndDehydrateRoundTripPreservesStrokesAndStylusPoints()
    {
        RunInSta(() =>
        {
            var model = new NotePage { PageNumber = 1, Width = 800, Height = 1000 };
            var page = new PageViewModel(model);

            Assert.False(page.IsHydrated);
            Assert.Null(page.Strokes);

            page.Hydrate();
            Assert.True(page.IsHydrated);
            Assert.NotNull(page.Strokes);
            Assert.Empty(page.Strokes);

            var points = new StylusPointCollection
            {
                new StylusPoint(10, 20),
                new StylusPoint(30, 40),
                new StylusPoint(50, 60)
            };
            var stroke = new Stroke(points);
            page.Strokes.Add(stroke);
            Assert.True(page.IsDirty);

            page.Dehydrate();
            Assert.False(page.IsHydrated);
            Assert.Null(page.Strokes);
            Assert.False(page.IsDirty);
            Assert.True(page.CompressedIsfByteCount > 0);

            page.Hydrate();
            Assert.True(page.IsHydrated);
            Assert.NotNull(page.Strokes);
            Assert.Single(page.Strokes);
            Assert.Equal(3, page.Strokes[0].StylusPoints.Count);
            Assert.Equal(10.0, page.Strokes[0].StylusPoints[0].X, precision: 1);
            Assert.Equal(20.0, page.Strokes[0].StylusPoints[0].Y, precision: 1);
        });
    }

    [Fact]
    public void CorruptedIsfBytesSafeFallbackToEmptyCollection()
    {
        RunInSta(() =>
        {
            var page = new PageViewModel(new NotePage { PageNumber = 1 });
            page.LoadIsfBytes([0x01, 0x02, 0x03, 0x04, 0x05, 0xAA, 0xBB, 0xCC]);

            page.Hydrate();
            Assert.True(page.IsHydrated);
            Assert.NotNull(page.Strokes);
            Assert.Empty(page.Strokes);
        });
    }

    [Fact]
    public void CsCheckFuzzingCorruptedIsfBytesNeverThrows()
    {
        Gen.Byte.Array[0, 512].Sample(randomBytes =>
        {
            RunInSta(() =>
            {
                var page = new PageViewModel(new NotePage { PageNumber = 1 });
                page.LoadIsfBytes(randomBytes);
                page.Hydrate();
                Assert.True(page.IsHydrated);
                Assert.NotNull(page.Strokes);
                page.Dehydrate();
                Assert.False(page.IsHydrated);
            });
        });
    }

    [Fact]
    public void StylusPointBudgetCapTruncatesExcessivePoints()
    {
        RunInSta(() =>
        {
            var page = new PageViewModel(new NotePage { PageNumber = 1 });
            page.Hydrate();

            // Create 3 strokes of 40,000 points = 120,000 points (exceeds 100,000 cap)
            for (var s = 0; s < 3; s++)
            {
                var points = new StylusPointCollection(40_000);
                for (var i = 0; i < 40_000; i++)
                {
                    points.Add(new StylusPoint(i, s));
                }
                page.Strokes!.Add(new Stroke(points));
            }

            var isf = page.ExportIsfBytes();
            Assert.NotNull(isf);

            var loadedPage = new PageViewModel(new NotePage { PageNumber = 2 });
            loadedPage.LoadIsfBytes(isf);
            loadedPage.Hydrate();

            var totalPoints = 0;
            foreach (var stroke in loadedPage.Strokes!)
            {
                totalPoints += stroke.StylusPoints.Count;
            }

            Assert.True(totalPoints <= PageViewModel.MaxStylusPointsPerPage);
            Assert.Equal(PageViewModel.MaxStylusPointsPerPage, totalPoints);
        });
    }

    [Fact]
    public void TwoHundredPageConsecutiveViewMaintainsLowMemoryAndHydrationWindow()
    {
        RunInSta(() =>
        {
            var startMem = GC.GetTotalMemory(true);
            var vm = new MainViewModel();

            // Add 199 more pages to reach 200 pages
            for (var i = 0; i < 199; i++)
            {
                vm.AddPageCommand.Execute(null);
            }

            Assert.Equal(200, vm.Pages.Count);

            // Simulate drawing on a few pages
            foreach (var p in vm.Pages)
            {
                p.Hydrate();
                p.Strokes!.Add(new Stroke(new StylusPointCollection([new StylusPoint(1, 1), new StylusPoint(2, 2)])));
            }

            // Trigger hydration for top of document (view at 0, viewport height 800)
            vm.UpdateVisiblePagesHydration(verticalOffset: 0.0, viewportHeight: 800.0);

            var hydratedCountAtTop = 0;
            foreach (var p in vm.Pages)
            {
                if (p.IsHydrated)
                {
                    hydratedCountAtTop++;
                }
            }

            // Only visible pages + 1-page cache margin above and below should be hydrated (<= 6 pages)
            Assert.True(hydratedCountAtTop <= 6);
            Assert.True(hydratedCountAtTop >= 1);

            // Scroll to the middle of the document (page 100)
            var page100Offset = vm.GetPageVerticalOffset(vm.Pages[99]);
            vm.UpdateVisiblePagesHydration(verticalOffset: page100Offset, viewportHeight: 800.0);

            // Page 1 should now be dehydrated, with strokes preserved in ISF bytes
            Assert.False(vm.Pages[0].IsHydrated);
            Assert.Null(vm.Pages[0].Strokes);
            Assert.True(vm.Pages[0].CompressedIsfByteCount > 0);

            // Middle pages should be hydrated
            Assert.True(vm.Pages[99].IsHydrated);
            Assert.NotNull(vm.Pages[99].Strokes);

            // Active page tracking should have updated to page 100
            Assert.Equal(100, vm.ActivePageIndex);

            // Verify memory growth is bounded (well under 35 MB for 200 dehydrated pages)
            var mem = GC.GetTotalMemory(true);
            Assert.True(mem - startMem < 35 * 1024 * 1024, $"Expected memory growth under 35MB, but was {(mem - startMem) / (1024 * 1024)}MB");
        });
    }

    [Fact]
    public void MainWindowDualViewModesSwitching()
    {
        RunInSta(() =>
        {
            var window = new MainWindow();
            FlushDispatcher();

            var consecutive = window.FindName("ConsecutivePagesControl") as System.Windows.Controls.ItemsControl;
            Assert.NotNull(consecutive);
            Assert.Equal(Visibility.Visible, consecutive.Visibility);

            var separated = window.FindName("SeparatedPageHost") as System.Windows.Controls.ContentControl;
            Assert.NotNull(separated);
            Assert.Equal(Visibility.Collapsed, separated.Visibility);

            // Toggle to Separated mode (F5)
            var handledF5 = window.HandleNavigationKey(Key.F5, ModifierKeys.None);
            Assert.True(handledF5);
            Assert.Equal(ViewMode.Separated, window.ViewModel.CurrentViewMode);
            FlushDispatcher();

            Assert.Equal(Visibility.Collapsed, consecutive.Visibility);
            Assert.Equal(Visibility.Visible, separated.Visibility);

            // Toggle back to Consecutive mode
            window.HandleNavigationKey(Key.F5, ModifierKeys.None);
            Assert.Equal(ViewMode.Consecutive, window.ViewModel.CurrentViewMode);
            FlushDispatcher();

            Assert.Equal(Visibility.Visible, consecutive.Visibility);
            Assert.Equal(Visibility.Collapsed, separated.Visibility);
        });
    }

    [Fact]
    public void MainWindowKeyboardNavigationSwitchesPages()
    {
        RunInSta(() =>
        {
            var window = new MainWindow();

            window.HandleNavigationKey(Key.Enter, ModifierKeys.Control); // Page 2
            window.HandleNavigationKey(Key.Enter, ModifierKeys.Control); // Page 3
            Assert.Equal(3, window.ViewModel.TotalPages);
            Assert.Equal(3, window.ViewModel.ActivePageIndex);

            // PageUp -> Page 2
            window.HandleNavigationKey(Key.PageUp, ModifierKeys.None);
            Assert.Equal(2, window.ViewModel.ActivePageIndex);

            // Left arrow -> Page 1
            window.HandleNavigationKey(Key.Left, ModifierKeys.None);
            Assert.Equal(1, window.ViewModel.ActivePageIndex);

            // Right arrow -> Page 2
            window.HandleNavigationKey(Key.Right, ModifierKeys.None);
            Assert.Equal(2, window.ViewModel.ActivePageIndex);

            // PageDown -> Page 3
            window.HandleNavigationKey(Key.PageDown, ModifierKeys.None);
            Assert.Equal(3, window.ViewModel.ActivePageIndex);

            // Zoom reset Ctrl+0
            window.ViewModel.SetZoom(2.5);
            Assert.Equal(2.5, window.ViewModel.ZoomLevel);
            window.HandleNavigationKey(Key.D0, ModifierKeys.Control);
            Assert.Equal(1.0, window.ViewModel.ZoomLevel);
        });
    }
}
