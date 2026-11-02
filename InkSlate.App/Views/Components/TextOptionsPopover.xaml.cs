using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using InkSlate.App.ViewModels;

namespace InkSlate.App.Views.Components;

public partial class TextOptionsPopover : UserControl
{
    public TextOptionsPopover()
    {
        InitializeComponent();
    }

    private void ColorSwatchButton_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is ColorSwatchOption swatch)
        {
            if (swatch.IsCustom && DataContext is MainViewModel vm)
            {
                vm.Toolbar.RemoveCustomColorCommand.Execute(swatch.Hex);
                e.Handled = true;
            }
        }
    }

    private void ColorSpectrumBox_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement spectrumElement)
        {
            spectrumElement.CaptureMouse();
            UpdateSpectrumSelection(spectrumElement, e.GetPosition(spectrumElement));
            e.Handled = true;
        }
    }

    private void ColorSpectrumBox_MouseMove(object sender, MouseEventArgs e)
    {
        if (sender is FrameworkElement spectrumElement && spectrumElement.IsMouseCaptured)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                UpdateSpectrumSelection(spectrumElement, e.GetPosition(spectrumElement));
                e.Handled = true;
            }
            else
            {
                spectrumElement.ReleaseMouseCapture();
            }
        }
    }

    private void ColorSpectrumBox_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement spectrumElement && spectrumElement.IsMouseCaptured)
        {
            spectrumElement.ReleaseMouseCapture();
            e.Handled = true;
        }
    }

    private void UpdateSpectrumSelection(FrameworkElement element, Point position)
    {
        var width = element.ActualWidth;
        var height = element.ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var saturation = Math.Clamp(position.X / width, 0.0, 1.0);
        var value = Math.Clamp(1.0 - (position.Y / height), 0.0, 1.0);
        if (DataContext is MainViewModel vm)
        {
            vm.Toolbar.SetCustomColorSaturationAndValue(saturation, value);
        }
    }
}

