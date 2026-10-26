using System.Collections.ObjectModel;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using InkSlate.App.Controls;
using InkSlate.App.Converters;
using InkSlate.App.ViewModels;
using InkSlate.Core.Models;
using InkSlate.Core.Models.CanvasElements;
using InkSlate.Core.Services;
using InkSlate.Core.UndoRedo;
using Xunit;

namespace InkSlate.Tests;

public sealed class MemoryAndRenderingAuditTests
{
    private static void RunInSta(Action action) => StaTestRunner.Run(action);

    [Fact]
    public void CanvasElementAdornerBrushesAndPensAreFrozen()
    {
        RunInSta(() =>
        {
            Assert.True(CanvasElementAdorner.AccentBorderBrush.IsFrozen);
            Assert.True(CanvasElementAdorner.BorderPen.IsFrozen);
            Assert.True(CanvasElementAdorner.BorderPen.Brush.IsFrozen);
            Assert.True(CanvasElementAdorner.ThumbBackgroundBrush.IsFrozen);
        });
    }

    [Fact]
    public void HexToFrozenBrushConverterAlwaysOutputsFrozenBrushes()
    {
        RunInSta(() =>
        {
            var converter = new HexToFrozenBrushConverter();

            var frozen1 = Assert.IsType<SolidColorBrush>(converter.Convert("#FFFFFF", typeof(Brush), null, System.Globalization.CultureInfo.InvariantCulture));
            var frozen2 = Assert.IsType<SolidColorBrush>(converter.Convert("#1E1E1E", typeof(Brush), null, System.Globalization.CultureInfo.InvariantCulture));
            var frozenFallback = Assert.IsType<SolidColorBrush>(converter.Convert("invalid-hex", typeof(Brush), null, System.Globalization.CultureInfo.InvariantCulture));

            Assert.True(frozen1.IsFrozen);
            Assert.True(frozen2.IsFrozen);
            Assert.True(frozenFallback.IsFrozen);
        });
    }

    [Fact]
    public void PageCanvasControlUnloadedDetachesHandlersAndLoadedReattachesIdempotently()
    {
        RunInSta(() =>
        {
            var undoRedo = new UndoRedoManager();
            var strokes = new StrokeCollection();
            var elements = new ObservableCollection<CanvasElementViewModel>();

            var control = new PageCanvasControl
            {
                UndoRedoManager = undoRedo,
                PageId = Guid.NewGuid(),
                PageStrokes = strokes,
                PageElements = elements
            };

            Assert.True(control.AreCanvasHandlersAttached);

            // Simulating Unloaded in virtualized scroll view
            control.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            Assert.False(control.AreCanvasHandlersAttached);

            // Unloaded repeated call is safe and idempotent
            control.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            Assert.False(control.AreCanvasHandlersAttached);

            // Mutating strokes while unloaded should not trigger undo operations or leak
            var stroke = new Stroke(new StylusPointCollection { new StylusPoint(10, 10), new StylusPoint(20, 20) });
            strokes.Add(stroke);
            Assert.Equal(0, undoRedo.UndoCount);

            // Simulating Loaded when scrolled back into view
            control.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            Assert.True(control.AreCanvasHandlersAttached);

            // Loaded repeated call is idempotent
            control.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            Assert.True(control.AreCanvasHandlersAttached);
        });
    }

    [Fact]
    public void CanvasElementContainerUnloadedDetachesAdornerAndUnsubscribesEvents()
    {
        RunInSta(() =>
        {
            var textElement = new TextCanvasElement { Id = Guid.NewGuid(), X = 10, Y = 10, Text = "Audit" };
            var elementVm = new TextCanvasElementViewModel(textElement);
            var container = new CanvasElementContainer(elementVm);

            container.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            Assert.True(container.IsPropertyChangedSubscribed);

            // Unload container
            container.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            Assert.False(container.IsPropertyChangedSubscribed);

            // Re-load container
            container.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            Assert.True(container.IsPropertyChangedSubscribed);
        });
    }

    [Fact]
    public async Task CrashRecoveryServiceManagesSecureDirectoryAndSnapshotRoundtrip()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "InkSlateRecoveryTest_" + Guid.NewGuid().ToString("N"));
        try
        {
            var doc = new NoteDocument
            {
                Id = Guid.NewGuid(),
                Title = "Recovery Test Document"
            };
            var page = new NotePage { Id = Guid.NewGuid(), PageNumber = 1, Width = 800, Height = 1000 };
            doc.Pages.Add(page);

            // Ensure secure directory
            CrashRecoveryService.EnsureSecureRecoveryDirectory(tempDir);
            Assert.True(Directory.Exists(tempDir));

            var dirInfo = new DirectoryInfo(tempDir);
            var dirSecurity = dirInfo.GetAccessControl();
            Assert.True(dirSecurity.AreAccessRulesProtected);

            // Write recovery snapshot
            var writtenPath = await CrashRecoveryService.WriteRecoverySnapshotAsync(doc, tempDir);
            Assert.True(File.Exists(writtenPath));
            Assert.EndsWith(".notex.recovery", writtenPath, StringComparison.OrdinalIgnoreCase);

            var fileInfo = new FileInfo(writtenPath);
            var fileSecurity = fileInfo.GetAccessControl();
            Assert.True(fileSecurity.AreAccessRulesProtected);

            // Load back snapshot via FilePackageService
            var reloaded = await FilePackageService.LoadAsync(writtenPath);
            Assert.Equal(doc.Id, reloaded.Id);
            Assert.Equal("Recovery Test Document", reloaded.Title);
            Assert.Single(reloaded.Pages);
            Assert.Equal(page.Id, reloaded.Pages[0].Id);

            // Delete recovery file
            CrashRecoveryService.DeleteRecoveryFile(doc.Id, tempDir);
            Assert.False(File.Exists(writtenPath));

            // Deleting when non-existent does not throw
            CrashRecoveryService.DeleteRecoveryFile(doc.Id, tempDir);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task MainViewModelAutoSaveAndRecoveryLifecycleCleansUpOnCleanSaveAndLoad()
    {
        var tempRecoveryDir = Path.Combine(Path.GetTempPath(), "InkSlateVmRecovery_" + Guid.NewGuid().ToString("N"));
        var tempDocPath = Path.Combine(Path.GetTempPath(), "InkSlateSave_" + Guid.NewGuid().ToString("N") + ".notex");

        try
        {
            var vm = new MainViewModel
            {
                RecoveryDirectoryOverride = tempRecoveryDir
            };

            var originalDocId = vm.Document.Id;

            // Trigger dirty state
            vm.DocumentTitle = "Auto-save recovery test";
            Assert.True(vm.IsDirty);

            // Snapshot writing
            var snapshotPath = await vm.WriteRecoverySnapshotAsync();
            Assert.True(File.Exists(snapshotPath));
            Assert.Equal(CrashRecoveryService.GetRecoveryFilePath(originalDocId, tempRecoveryDir), snapshotPath);

            // Clean save removes recovery file
            await vm.SaveToFileAsync(tempDocPath);
            Assert.False(vm.IsDirty);
            Assert.False(File.Exists(snapshotPath));

            // Make dirty again and write snapshot
            vm.DocumentTitle = "Another unsaved modification";
            Assert.True(vm.IsDirty);
            await vm.WriteRecoverySnapshotAsync();
            Assert.True(File.Exists(snapshotPath));

            // Explicit DeleteCurrentRecoveryFile cleans up
            vm.DeleteCurrentRecoveryFile();
            Assert.False(File.Exists(snapshotPath));

            // Re-write snapshot, then test LoadDocument deletes previous recovery file
            await vm.WriteRecoverySnapshotAsync();
            Assert.True(File.Exists(snapshotPath));

            var nextDoc = new NoteDocument { Id = Guid.NewGuid(), Title = "Next Document" };
            vm.LoadDocument(nextDoc);

            Assert.False(File.Exists(snapshotPath));
            Assert.Equal(nextDoc.Id, vm.Document.Id);
        }
        finally
        {
            if (Directory.Exists(tempRecoveryDir))
            {
                Directory.Delete(tempRecoveryDir, recursive: true);
            }
            if (File.Exists(tempDocPath))
            {
                File.Delete(tempDocPath);
            }
        }
    }
}

