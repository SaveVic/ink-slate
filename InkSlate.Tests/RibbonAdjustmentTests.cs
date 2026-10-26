using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using InkSlate.App.Controls;
using InkSlate.App.ViewModels;
using InkSlate.App.Views;
using InkSlate.Core.Models;
using InkSlate.Core.Models.CanvasElements;
using Xunit;

namespace InkSlate.Tests;

public sealed class RibbonAdjustmentTests
{
    private static void RunInSta(Action action) => StaTestRunner.Run(action);

    [Fact]
    public void DefaultToolbarState_IsPointerToolAndTopDocked()
    {
        var vm = new ToolbarViewModel();

        Assert.Equal(ToolMode.Pointer, vm.ActiveTool);
        Assert.True(vm.IsPointerToolActive);
        Assert.False(vm.IsPenToolActive);
        Assert.False(vm.IsHighlighterToolActive);
        Assert.False(vm.IsEraserToolActive);
        Assert.False(vm.IsLassoToolActive);
        Assert.False(vm.IsTextToolActive);
        Assert.False(vm.IsImageToolActive);

        Assert.Equal(ToolbarPlacement.TopDocked, vm.Placement);
        Assert.True(vm.IsTopDocked);
        Assert.False(vm.IsLeftDocked);
        Assert.False(vm.IsTopFloating);
    }

    [Fact]
    public void ToolbarPlacement_CycleCommand_RotatesThroughAllPlacements()
    {
        var vm = new ToolbarViewModel();
        Assert.Equal(ToolbarPlacement.TopDocked, vm.Placement);

        vm.CyclePlacementCommand.Execute(null);
        Assert.Equal(ToolbarPlacement.LeftDocked, vm.Placement);
        Assert.True(vm.IsLeftDocked);
        Assert.False(vm.IsTopDocked);

        vm.CyclePlacementCommand.Execute(null);
        Assert.Equal(ToolbarPlacement.TopFloating, vm.Placement);
        Assert.True(vm.IsTopFloating);

        vm.CyclePlacementCommand.Execute(null);
        Assert.Equal(ToolbarPlacement.TopDocked, vm.Placement);
        Assert.True(vm.IsTopDocked);

        vm.SetPlacementCommand.Execute(ToolbarPlacement.LeftDocked);
        Assert.Equal(ToolbarPlacement.LeftDocked, vm.Placement);
    }

    [Fact]
    public void ActiveToolSelection_UpdatesOnlySelectedToolFlag()
    {
        var vm = new ToolbarViewModel();

        var tools = new[]
        {
            (ToolMode.Pointer, nameof(vm.IsPointerToolActive)),
            (ToolMode.Pen, nameof(vm.IsPenToolActive)),
            (ToolMode.Highlighter, nameof(vm.IsHighlighterToolActive)),
            (ToolMode.Eraser, nameof(vm.IsEraserToolActive)),
            (ToolMode.Lasso, nameof(vm.IsLassoToolActive)),
            (ToolMode.Text, nameof(vm.IsTextToolActive)),
            (ToolMode.Image, nameof(vm.IsImageToolActive))
        };

        foreach (var (tool, activeProp) in tools)
        {
            vm.SelectToolCommand.Execute(tool);
            Assert.Equal(tool, vm.ActiveTool);

            Assert.Equal(tool == ToolMode.Pointer, vm.IsPointerToolActive);
            Assert.Equal(tool == ToolMode.Pen, vm.IsPenToolActive);
            Assert.Equal(tool == ToolMode.Highlighter, vm.IsHighlighterToolActive);
            Assert.Equal(tool == ToolMode.Eraser, vm.IsEraserToolActive);
            Assert.Equal(tool == ToolMode.Lasso, vm.IsLassoToolActive);
            Assert.Equal(tool == ToolMode.Text, vm.IsTextToolActive);
            Assert.Equal(tool == ToolMode.Image, vm.IsImageToolActive);
        }
    }

    [Fact]
    public void PageCanvasControl_PointerMode_EnablesElementInteractivityAndDisablesInk()
    {
        RunInSta(() =>
        {
            var control = new PageCanvasControl
            {
                ActiveToolMode = ToolMode.Pointer
            };

            var model = new TextCanvasElement { X = 10, Y = 10, Width = 100, Height = 50, Text = "Sample" };
            var textVm = new TextCanvasElementViewModel(model);
            var elements = new List<CanvasElementViewModel> { textVm };

            control.PageElements = elements;

            Assert.Equal(InkCanvasEditingMode.None, control.InkSurface.EditingMode);

            var container = Assert.Single(control.ElementContainers);
            Assert.True(container.IsHitTestVisible);
        });
    }

    [Fact]
    public void MainWindow_Shortcuts_VSelectsPointerTool()
    {
        RunInSta(() =>
        {
            var window = new MainWindow();

            // Switch away to Pen
            window.ViewModel.Toolbar.SelectToolCommand.Execute(ToolMode.Pen);
            Assert.Equal(ToolMode.Pen, window.ViewModel.Toolbar.ActiveTool);

            // Press V -> switches to Pointer
            Assert.True(window.HandleNavigationKey(Key.V, ModifierKeys.None));
            Assert.Equal(ToolMode.Pointer, window.ViewModel.Toolbar.ActiveTool);
            Assert.True(window.ViewModel.Toolbar.IsPointerToolActive);
        });
    }

    [Fact]
    public void MainWindow_ToolbarButtons_HaveHandCursorAndActiveMarkers()
    {
        RunInSta(() =>
        {
            var window = new MainWindow();

            var pointerBtn = window.FindName("PointerToolButton") as Wpf.Ui.Controls.Button;
            var penBtn = window.FindName("PenToolButton") as Wpf.Ui.Controls.Button;
            var eraserBtn = window.FindName("EraserToolButton") as Wpf.Ui.Controls.Button;
            var placementBtn = window.FindName("CyclePlacementButton") as Wpf.Ui.Controls.Button;

            Assert.NotNull(pointerBtn);
            Assert.NotNull(penBtn);
            Assert.NotNull(eraserBtn);
            Assert.NotNull(placementBtn);

            Assert.Equal(Cursors.Hand, pointerBtn.Cursor);
            Assert.Equal(Cursors.Hand, penBtn.Cursor);
            Assert.Equal(Cursors.Hand, eraserBtn.Cursor);
            Assert.Equal(Cursors.Hand, placementBtn.Cursor);
        });
    }

    [Fact]
    public void ZoomScrollViewer_PointerMode_PansOnEmptySpaceAndClearsSelection()
    {
        RunInSta(() =>
        {
            var mainVm = new MainViewModel();
            Assert.NotNull(mainVm.ActivePage);

            var textModel = new TextCanvasElement { X = 50, Y = 50, Width = 100, Height = 40 };
            var textElem = new TextCanvasElementViewModel(textModel) { IsSelected = true };
            mainVm.ActivePage.Elements.Add(textElem);
            Assert.True(textElem.IsSelected);

            var viewer = new ZoomScrollViewer
            {
                DataContext = mainVm,
                ActiveToolMode = ToolMode.Pointer
            };

            // Call BeginPan directly simulating empty space click
            viewer.BeginPan(new Point(100, 100));
            Assert.True(viewer.IsPanning);

            // Update pan
            viewer.UpdatePan(new Point(80, 70));
            Assert.Equal(20.0, viewer.TargetHorizontalOffset);
            Assert.Equal(30.0, viewer.TargetVerticalOffset);

            viewer.EndPan();
            Assert.False(viewer.IsPanning);
        });
    }

    [Fact]
    public void WheelScrollStep_And_RequestBringIntoView_AreHandled()
    {
        RunInSta(() =>
        {
            Assert.Equal(24.0, ZoomScrollViewer.WheelScrollStep);

            var ctor = typeof(RequestBringIntoViewEventArgs).GetConstructor(
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
                null,
                [typeof(System.Windows.Media.Visual), typeof(Rect)],
                null);
            Assert.NotNull(ctor);

            var viewer = new ZoomScrollViewer();
            var argsViewer = (RequestBringIntoViewEventArgs)ctor.Invoke([viewer, Rect.Empty]);
            argsViewer.RoutedEvent = FrameworkElement.RequestBringIntoViewEvent;
            viewer.RaiseEvent(argsViewer);
            Assert.True(argsViewer.Handled);

            var pageCanvas = new PageCanvasControl();
            var argsCanvas = (RequestBringIntoViewEventArgs)ctor.Invoke([pageCanvas, Rect.Empty]);
            argsCanvas.RoutedEvent = FrameworkElement.RequestBringIntoViewEvent;
            pageCanvas.RaiseEvent(argsCanvas);
            Assert.True(argsCanvas.Handled);
        });
    }

    [Fact]
    public void TextTool_Placement_SwitchesToDefaultPointerAndEntersEditing()
    {
        RunInSta(() =>
        {
            var toolbar = new ToolbarViewModel();
            toolbar.SelectToolCommand.Execute(ToolMode.Text);
            Assert.Equal(ToolMode.Text, toolbar.ActiveTool);

            var page = new NotePage();
            var pageVm = new PageViewModel(page, null, toolbar);

            var textVm = pageVm.AddTextElement(new Point(100, 150));

            Assert.Single(pageVm.Elements);
            Assert.True(textVm.IsSelected);
            Assert.True(textVm.IsEditing);
            Assert.Equal(ToolMode.Pointer, toolbar.ActiveTool);
            Assert.True(toolbar.IsPointerToolActive);
            Assert.False(toolbar.IsTextOptionsOpen);
        });
    }

    [Fact]
    public void EmptyTextBox_AutoDeletesWhenEditingEndsOrWhitespace()
    {
        RunInSta(() =>
        {
            var toolbar = new ToolbarViewModel();
            var page = new NotePage();
            var pageVm = new PageViewModel(page, null, toolbar);

            var textVm = pageVm.AddTextElement(new Point(50, 50));
            Assert.Single(pageVm.Elements);

            // User finishes editing with empty text
            textVm.IsEditing = false;
            Assert.Empty(pageVm.Elements);

            // User enters only whitespace
            var textVm2 = pageVm.AddTextElement(new Point(50, 50));
            Assert.Single(pageVm.Elements);
            textVm2.Text = "   \n\t  ";
            textVm2.IsEditing = false;
            Assert.Empty(pageVm.Elements);
        });
    }

    [Fact]
    public void SelectSingleElement_WhileEditingEmptyTextBox_SafelyDeselectsWithoutConcurrentModificationException()
    {
        RunInSta(() =>
        {
            var toolbar = new ToolbarViewModel();
            var page = new NotePage();
            var pageVm = new PageViewModel(page, null, toolbar);

            var textVm = pageVm.AddTextElement(new Point(50, 50));
            Assert.True(textVm.IsEditing);
            Assert.Single(pageVm.Elements);

            // Simulating clicking on empty canvas while text box is currently being edited
            pageVm.SelectSingleElement(null);

            // No exception thrown, and empty text is cleaned up
            Assert.Empty(pageVm.Elements);
        });
    }

    [Fact]
    public void NonEmptyTextBox_PersistsWhenEditingEnds()
    {
        RunInSta(() =>
        {
            var toolbar = new ToolbarViewModel();
            var page = new NotePage();
            var pageVm = new PageViewModel(page, null, toolbar);

            var textVm = pageVm.AddTextElement(new Point(50, 50));
            textVm.Text = "Hello InkSlate";
            textVm.IsEditing = false;

            Assert.Single(pageVm.Elements);
            Assert.Equal("Hello InkSlate", textVm.Text);
        });
    }

    [Fact]
    public void SingleObjectSelection_PointerExclusivelySelectsOneElement()
    {
        RunInSta(() =>
        {
            var page = new NotePage();
            var pageVm = new PageViewModel(page);

            var text1 = pageVm.AddTextElement(new Point(10, 10));
            text1.Text = "Item 1";
            text1.IsEditing = false;

            var text2 = pageVm.AddTextElement(new Point(100, 100));
            text2.Text = "Item 2";
            text2.IsEditing = false;

            // Selecting text1 exclusively deselects text2
            pageVm.SelectSingleElement(text1);
            text1.IsSelected = true;

            Assert.True(text1.IsSelected);
            Assert.False(text2.IsSelected);

            // Selecting text2 exclusively deselects text1
            pageVm.SelectSingleElement(text2);
            text2.IsSelected = true;

            Assert.False(text1.IsSelected);
            Assert.True(text2.IsSelected);

            // Clearing selection
            pageVm.SelectSingleElement(null);
            Assert.False(text1.IsSelected);
            Assert.False(text2.IsSelected);
        });
    }

    [Fact]
    public void ToolbarViewModel_SubtoolSelectionStates_And_TextSubtool_WorkCorrectly()
    {
        RunInSta(() =>
        {
            var vm = new ToolbarViewModel();

            // Default pen thickness 2.5 is selected
            var opt25 = Assert.Single(vm.ThicknessOptions, o => Math.Abs(o.ThicknessDip - 2.5) < 0.01);
            Assert.True(opt25.IsSelected);

            // Default pen color #FF1E1E1E is selected
            var blackSwatch = Assert.Single(vm.ColorSwatches, s => s.Hex == "#FF1E1E1E");
            Assert.True(blackSwatch.IsSelected);

            // Select 4.0 thickness
            vm.SelectPenThicknessCommand.Execute(4.0);
            var opt40 = Assert.Single(vm.ThicknessOptions, o => Math.Abs(o.ThicknessDip - 4.0) < 0.01);
            Assert.True(opt40.IsSelected);
            Assert.False(opt25.IsSelected);

            // Eraser size selection in Point mode
            vm.SelectEraserModeCommand.Execute(EraserMode.Point);
            vm.SelectEraserSizeCommand.Execute(32.0);
            var optEraser32 = Assert.Single(vm.EraserSizeOptions, e => Math.Abs(e.SizeDip - 32.0) < 0.01);
            Assert.True(optEraser32.IsSelected);

            // Text tool popover toggle
            vm.SelectToolCommand.Execute(ToolMode.Text);
            Assert.False(vm.IsTextOptionsOpen);
            vm.SelectToolCommand.Execute(ToolMode.Text);
            Assert.True(vm.IsTextOptionsOpen);

            // Text font formatting commands
            vm.IncreaseTextFontSizeCommand.Execute(null);
            Assert.Equal(20.0, vm.SelectedTextFontSize);
            vm.DecreaseTextFontSizeCommand.Execute(null);
            Assert.Equal(18.0, vm.SelectedTextFontSize);

            Assert.False(vm.IsTextBold);
            vm.ToggleTextBoldCommand.Execute(null);
            Assert.True(vm.IsTextBold);

            Assert.False(vm.IsTextItalic);
            vm.ToggleTextItalicCommand.Execute(null);
            Assert.True(vm.IsTextItalic);

            // Text color swatches
            vm.SelectTextColorCommand.Execute("#FF2563EB");
            Assert.Equal("#FF2563EB", vm.SelectedTextColorHex);
            var blueTextSwatch = Assert.Single(vm.TextColorSwatches, s => s.Hex == "#FF2563EB");
            Assert.True(blueTextSwatch.IsSelected);
            var blackTextSwatch = Assert.Single(vm.TextColorSwatches, s => s.Hex == "#FF1E1E1E");
            Assert.False(blackTextSwatch.IsSelected);

            // Selecting another tool closes text options
            vm.SelectToolCommand.Execute(ToolMode.Pointer);
            Assert.False(vm.IsTextOptionsOpen);
        });
    }

    [Fact]
    public void ToolbarTextFormatting_SynchronizesWithActivePageTextElement()
    {
        RunInSta(() =>
        {
            var mainVm = new MainViewModel();
            Assert.NotNull(mainVm.ActivePage);

            var textElem = mainVm.ActivePage.AddTextElement(new Point(50, 50));
            textElem.Text = "Sample note";
            textElem.IsSelected = true;

            // Changing toolbar font size updates selected element
            mainVm.Toolbar.IncreaseTextFontSizeCommand.Execute(null);
            Assert.Equal(mainVm.Toolbar.SelectedTextFontSize, textElem.FontSize);

            // Changing bold / italic updates selected element
            mainVm.Toolbar.ToggleTextBoldCommand.Execute(null);
            Assert.True(textElem.IsBold);

            mainVm.Toolbar.ToggleTextItalicCommand.Execute(null);
            Assert.True(textElem.IsItalic);

            // Changing color updates selected element
            mainVm.Toolbar.SelectTextColorCommand.Execute("#FFDC2626");
            Assert.Equal("#FFDC2626", textElem.ForegroundHex);
        });
    }

    [Fact]
    public void SelectedObject_DeletedViaDeleteKey_IsRemovedFromPage()
    {
        RunInSta(() =>
        {
            var mainVm = new MainViewModel();
            var window = new MainWindow(mainVm);
            Assert.NotNull(mainVm.ActivePage);

            var textElem = mainVm.ActivePage.AddTextElement(new Point(50, 50));
            textElem.Text = "Note to delete";
            textElem.IsEditing = false;
            textElem.IsSelected = true;

            var handled = window.HandleNavigationKey(Key.Delete, ModifierKeys.None);
            Assert.True(handled);
            Assert.Empty(mainVm.ActivePage.Elements);
        });
    }

    [Fact]
    public void SelectedObject_DeletedViaBackspaceKey_IsRemovedFromPage()
    {
        RunInSta(() =>
        {
            var mainVm = new MainViewModel();
            var window = new MainWindow(mainVm);
            Assert.NotNull(mainVm.ActivePage);

            var textElem = mainVm.ActivePage.AddTextElement(new Point(50, 50));
            textElem.Text = "Note to delete with backspace";
            textElem.IsEditing = false;
            textElem.IsSelected = true;

            var handled = window.HandleNavigationKey(Key.Back, ModifierKeys.None);
            Assert.True(handled);
            Assert.Empty(mainVm.ActivePage.Elements);
        });
    }

    [Fact]
    public void SelectedObject_DeleteCommandOnElement_RemovesElementFromPage()
    {
        RunInSta(() =>
        {
            var mainVm = new MainViewModel();
            Assert.NotNull(mainVm.ActivePage);

            var textElem = mainVm.ActivePage.AddTextElement(new Point(50, 50));
            textElem.Text = "Mini-bar trash button test";
            Assert.Single(mainVm.ActivePage.Elements);

            textElem.DeleteCommand.Execute(null);
            Assert.Empty(mainVm.ActivePage.Elements);
        });
    }

    [Fact]
    public void ObjectInEditingMode_NotRemovedByDeleteKey()
    {
        RunInSta(() =>
        {
            var mainVm = new MainViewModel();
            var window = new MainWindow(mainVm);
            Assert.NotNull(mainVm.ActivePage);

            var textElem = mainVm.ActivePage.AddTextElement(new Point(50, 50));
            textElem.Text = "Currently typing";
            textElem.IsSelected = true;
            textElem.IsEditing = true;

            var handled = window.HandleNavigationKey(Key.Delete, ModifierKeys.None);
            Assert.False(handled);
            Assert.Single(mainVm.ActivePage.Elements);
        });
    }

    [Fact]
    public void DeletedObject_CanBeRestoredWithUndo()
    {
        RunInSta(() =>
        {
            var mainVm = new MainViewModel();
            Assert.NotNull(mainVm.ActivePage);

            var textElem = mainVm.ActivePage.AddTextElement(new Point(50, 50));
            textElem.Text = "Undoable deleted note";
            textElem.IsEditing = false;
            textElem.IsSelected = true;

            mainVm.DeleteSelectedCommand.Execute(null);
            Assert.Empty(mainVm.ActivePage.Elements);

            Assert.True(mainVm.UndoCommand.CanExecute(null));
            mainVm.UndoCommand.Execute(null);

            Assert.Single(mainVm.ActivePage.Elements);
            var restored = Assert.IsType<TextCanvasElementViewModel>(mainVm.ActivePage.Elements[0]);
            Assert.Equal("Undoable deleted note", restored.Text);
        });
    }

    [Fact]
    public void CanvasElementAdorner_ResizeCornerDrag_PreservesSelectionAndScalesProportionally()
    {
        RunInSta(() =>
        {
            var model = new ImageCanvasElement
            {
                AssetKey = "test_img",
                X = 100,
                Y = 100,
                Width = 200,
                Height = 100
            };
            var imgVm = new ImageCanvasElementViewModel(model)
            {
                IsSelected = true
            };

            var container = new CanvasElementContainer(imgVm);
            var adorner = new CanvasElementAdorner(container, imgVm, null, Guid.NewGuid());

            // Check visual thumbs exist
            Assert.Equal(4, adorner.GetType().GetProperty("VisualChildrenCount", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(adorner));

            var getVisualChildMethod = adorner.GetType().GetMethod("GetVisualChild", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(getVisualChildMethod);

            var bottomRightThumb = Assert.IsType<System.Windows.Controls.Primitives.Thumb>(getVisualChildMethod.Invoke(adorner, [2]));

            // Simulate drag started and drag delta on bottom-right corner thumb
            bottomRightThumb.RaiseEvent(new System.Windows.Controls.Primitives.DragStartedEventArgs(0, 0)
            {
                RoutedEvent = System.Windows.Controls.Primitives.Thumb.DragStartedEvent
            });

            bottomRightThumb.RaiseEvent(new System.Windows.Controls.Primitives.DragDeltaEventArgs(50, 25)
            {
                RoutedEvent = System.Windows.Controls.Primitives.Thumb.DragDeltaEvent
            });

            // Selection is preserved
            Assert.True(imgVm.IsSelected);
            // Width increased from 200 to 250, aspect ratio (2.0) preserved: height = 125
            Assert.Equal(250.0, imgVm.Width, 2);
            Assert.Equal(125.0, imgVm.Height, 2);
        });
    }

    [Fact]
    public void PageCanvasControl_IsInsideElementOrAdorner_RecognizesThumbsAndAdorners()
    {
        RunInSta(() =>
        {
            var isInsideMethod = typeof(PageCanvasControl).GetMethod("IsInsideElementOrAdorner", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.NotNull(isInsideMethod);

            var model = new ImageCanvasElement { AssetKey = "test_asset", Width = 100, Height = 100 };
            var imgVm = new ImageCanvasElementViewModel(model);
            var container = new CanvasElementContainer(imgVm);
            var adorner = new CanvasElementAdorner(container, imgVm, null, Guid.NewGuid());

            var getVisualChildMethod = adorner.GetType().GetMethod("GetVisualChild", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(getVisualChildMethod);
            var thumb = Assert.IsType<System.Windows.Controls.Primitives.Thumb>(getVisualChildMethod.Invoke(adorner, [0]));

            // Adorner thumb is recognized as interactive element
            Assert.True((bool)isInsideMethod.Invoke(null, [thumb])!);
            // Adorner is recognized
            Assert.True((bool)isInsideMethod.Invoke(null, [adorner])!);
            // Container is recognized
            Assert.True((bool)isInsideMethod.Invoke(null, [container])!);
            // Arbitrary grid is not
            Assert.False((bool)isInsideMethod.Invoke(null, [new Grid()])!);
        });
    }

    [Fact]
    public void PageViewModel_IsEmpty_AccuratelyReflectsContent()
    {
        RunInSta(() =>
        {
            var page = new NotePage();
            var pageVm = new PageViewModel(page);
            pageVm.Hydrate();

            // Brand new page is empty
            Assert.True(pageVm.IsEmpty);

            // Adding non-empty text element makes it not empty
            var textElem = pageVm.AddTextElement(new Point(10, 10));
            textElem.Text = "Hello InkSlate";
            Assert.False(pageVm.IsEmpty);

            // Removing text element makes it empty again
            pageVm.RemoveElement(textElem);
            Assert.True(pageVm.IsEmpty);

            // Adding a stroke makes it not empty
            var stroke = new Stroke([new StylusPoint(10, 10), new StylusPoint(20, 20)]);
            pageVm.Strokes?.Add(stroke);
            Assert.False(pageVm.IsEmpty);
        });
    }

    [Fact]
    public void MainViewModel_DeleteSpecificPage_EmptyPage_DeletesWithoutConfirmation()
    {
        RunInSta(() =>
        {
            var mainVm = new MainViewModel();
            mainVm.AddPageCommand.Execute(null); // Now 2 pages
            Assert.Equal(2, mainVm.Pages.Count);

            var confirmCalled = false;
            mainVm.ConfirmDialog = (_, _) =>
            {
                confirmCalled = true;
                return true;
            };

            var page2 = mainVm.Pages[1];
            Assert.True(page2.IsEmpty);

            mainVm.DeleteSpecificPageCommand.Execute(page2);

            Assert.False(confirmCalled);
            Assert.Single(mainVm.Pages);
            Assert.Equal(1, mainVm.Pages[0].PageNumber);
        });
    }

    [Fact]
    public void MainViewModel_DeleteSpecificPage_NonEmptyPage_PromptsConfirmation()
    {
        RunInSta(() =>
        {
            var mainVm = new MainViewModel();
            mainVm.AddPageCommand.Execute(null); // Now 2 pages
            Assert.Equal(2, mainVm.Pages.Count);

            var page2 = mainVm.Pages[1];
            page2.Hydrate();
            var text = page2.AddTextElement(new Point(50, 50));
            text.Text = "Important notes on page 2";
            Assert.False(page2.IsEmpty);

            // User clicks "Cancel" (false)
            var promptCount = 0;
            mainVm.ConfirmDialog = (msg, title) =>
            {
                promptCount++;
                Assert.Contains("Page 2", msg);
                return false;
            };

            mainVm.DeleteSpecificPageCommand.Execute(page2);
            Assert.Equal(1, promptCount);
            Assert.Equal(2, mainVm.Pages.Count); // Not deleted!

            // User clicks "Yes" (true)
            mainVm.ConfirmDialog = (_, _) =>
            {
                promptCount++;
                return true;
            };

            mainVm.DeleteSpecificPageCommand.Execute(page2);
            Assert.Equal(2, promptCount);
            Assert.Single(mainVm.Pages); // Deleted!
            Assert.Equal(1, mainVm.Pages[0].PageNumber);
        });
    }

    [Fact]
    public void MainViewModel_DeleteSpecificPage_OnlyOnePageLeft_DisabledAndDoesNotShowDialog()
    {
        RunInSta(() =>
        {
            var mainVm = new MainViewModel();
            Assert.Single(mainVm.Pages);
            Assert.False(mainVm.CanDeletePages);
            Assert.False(mainVm.DeleteSpecificPageCommand.CanExecute(mainVm.Pages[0]));

            var dialogCalled = false;
            mainVm.InfoDialog = (_, _) => dialogCalled = true;
            mainVm.ConfirmDialog = (_, _) => { dialogCalled = true; return true; };

            mainVm.DeleteSpecificPageCommand.Execute(mainVm.Pages[0]);

            Assert.False(dialogCalled);
            Assert.Single(mainVm.Pages);
            Assert.Equal(1, mainVm.Pages[0].PageNumber);

            // Adding a second page enables CanDeletePages and command execution
            mainVm.AddPageCommand.Execute(null);
            Assert.Equal(2, mainVm.Pages.Count);
            Assert.True(mainVm.CanDeletePages);
            Assert.True(mainVm.DeleteSpecificPageCommand.CanExecute(mainVm.Pages[1]));
        });
    }

    [Fact]
    public void DocumentTitleAndSaveStatus_InitialAndLifecycleStates()
    {
        RunInSta(() =>
        {
            var mainVm = new MainViewModel();

            // 1. Initial State: Untitled, Not saved
            Assert.Equal("Untitled", mainVm.DocumentTitle);
            Assert.Null(mainVm.CurrentFilePath);
            Assert.False(mainVm.IsDirty);
            Assert.False(mainVm.IsSaved);
            Assert.Equal("Not saved", mainVm.SaveStatusDisplay);
            Assert.Contains("Untitled", mainVm.WindowTitleDisplay);
            Assert.Contains("•", mainVm.WindowTitleDisplay);

            // 2. Modifying active page marks document dirty and unsaved
            Assert.NotNull(mainVm.ActivePage);
            var text = mainVm.ActivePage.AddTextElement(new Point(20, 20));
            text.Text = "Note in progress";
            Assert.True(mainVm.IsDirty);
            Assert.False(mainVm.IsSaved);
            Assert.Equal("Unsaved", mainVm.SaveStatusDisplay);

            // 3. Simulating file save marks document saved and updates title
            mainVm.CurrentFilePath = @"C:\docs\ProjectNotes.notex";
            mainVm.DocumentTitle = "ProjectNotes";
            mainVm.IsDirty = false;
            Assert.True(mainVm.IsSaved);
            Assert.Equal("Saved", mainVm.SaveStatusDisplay);
            Assert.Equal("ProjectNotes - InkSlate", mainVm.WindowTitleDisplay);

            // 4. Adding a page marks document unsaved again
            mainVm.AddPageCommand.Execute(null);
            Assert.True(mainVm.IsDirty);
            Assert.False(mainVm.IsSaved);
            Assert.Equal("Unsaved", mainVm.SaveStatusDisplay);
            Assert.Equal("ProjectNotes • - InkSlate", mainVm.WindowTitleDisplay);

            // 5. Loading document restores saved status and title
            var loaded = new NoteDocument
            {
                Title = "MeetingSummary"
            };
            mainVm.LoadDocument(loaded, @"C:\docs\MeetingSummary.notex");
            Assert.Equal("MeetingSummary", mainVm.DocumentTitle);
            Assert.True(mainVm.IsSaved);
            Assert.Equal("Saved", mainVm.SaveStatusDisplay);
            Assert.Equal("MeetingSummary - InkSlate", mainVm.WindowTitleDisplay);
        });
    }

    [Fact]
    public void DocumentTitle_EditLifecycle_BeginCancelAndCommit()
    {
        RunInSta(() =>
        {
            var mainVm = new MainViewModel();
            Assert.Equal("Untitled", mainVm.DocumentTitle);
            Assert.False(mainVm.IsEditingTitle);
            Assert.Empty(mainVm.TitleDraft);

            // 1. Begin edit initializes draft and flags editing mode
            mainVm.BeginEditTitleCommand.Execute(null);
            Assert.True(mainVm.IsEditingTitle);
            Assert.Equal("Untitled", mainVm.TitleDraft);

            // 2. Cancel edit restores draft and leaves title unchanged
            mainVm.TitleDraft = "Work in Progress";
            mainVm.CancelTitleEditCommand.Execute(null);
            Assert.False(mainVm.IsEditingTitle);
            Assert.Equal("Untitled", mainVm.DocumentTitle);
            Assert.Equal("Untitled", mainVm.TitleDraft);
            Assert.False(mainVm.IsDirty);

            // 3. Commit edit updates document title, marks dirty, and updates WindowTitleDisplay
            mainVm.BeginEditTitleCommand.Execute(null);
            mainVm.TitleDraft = "Chemistry Notes";
            mainVm.CommitTitleEditCommand.Execute(null);
            Assert.False(mainVm.IsEditingTitle);
            Assert.Equal("Chemistry Notes", mainVm.DocumentTitle);
            Assert.Equal("Chemistry Notes", mainVm.Document.Title);
            Assert.True(mainVm.IsDirty);
            Assert.Contains("Chemistry Notes", mainVm.WindowTitleDisplay);

            // 4. Committing blank/whitespace title falls back to Untitled
            mainVm.BeginEditTitleCommand.Execute(null);
            mainVm.TitleDraft = "   ";
            mainVm.CommitTitleEditCommand.Execute(null);
            Assert.Equal(NoteDocument.DefaultTitle, mainVm.DocumentTitle);
            Assert.Equal(NoteDocument.DefaultTitle, mainVm.Document.Title);
        });
    }

    [Fact]
    public void DocumentTitle_GetSafeDocumentTitleForFileName_SanitizesIllegalCharacters()
    {
        RunInSta(() =>
        {
            var mainVm = new MainViewModel
            {
                DocumentTitle = @"Project/Proposal: Draft *2* <Final>?"
            };

            var safeName = mainVm.GetSafeDocumentTitleForFileName();
            Assert.DoesNotContain("/", safeName);
            Assert.DoesNotContain(":", safeName);
            Assert.DoesNotContain("*", safeName);
            Assert.DoesNotContain("<", safeName);
            Assert.DoesNotContain(">", safeName);
            Assert.DoesNotContain("?", safeName);
            Assert.Contains("Project", safeName);
            Assert.Contains("Proposal", safeName);
        });
    }

    [Fact]
    public void MainWindow_F2Key_TriggersBeginEditTitle()
    {
        RunInSta(() =>
        {
            var mainVm = new MainViewModel();
            var window = new MainWindow(mainVm);

            Assert.False(mainVm.IsEditingTitle);
            var handled = window.HandleNavigationKey(Key.F2, ModifierKeys.None);
            Assert.True(handled);
            Assert.True(mainVm.IsEditingTitle);
            Assert.Equal("Untitled", mainVm.TitleDraft);
        });
    }

    [Fact]
    public void MainWindow_InitialDisplay_IsMaximized()
    {
        RunInSta(() =>
        {
            var mainVm = new MainViewModel();
            var window = new MainWindow(mainVm);

            Assert.Equal(WindowState.Maximized, window.WindowState);
        });
    }

    [Fact]
    public void MainWindow_F11Key_TogglesWindowState()
    {
        RunInSta(() =>
        {
            var mainVm = new MainViewModel();
            var window = new MainWindow(mainVm);

            Assert.Equal(WindowState.Maximized, window.WindowState);

            // Press F11 -> restores to Normal
            var handled = window.HandleNavigationKey(Key.F11, ModifierKeys.None);
            Assert.True(handled);
            Assert.Equal(WindowState.Normal, window.WindowState);

            // Press F11 again -> Maximized
            handled = window.HandleNavigationKey(Key.F11, ModifierKeys.None);
            Assert.True(handled);
            Assert.Equal(WindowState.Maximized, window.WindowState);
        });
    }
}

