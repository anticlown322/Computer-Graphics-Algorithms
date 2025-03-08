using System.Windows;
using System.Windows.Controls;
using CGA.MVVM.ViewModels;

namespace CGA.MVVM.Views;

public partial class CanvasView : UserControl
{
    public CanvasView()
    {
        InitializeComponent();
    }
    
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is CanvasViewModel canvasView)
        {
            canvasView.SceneManager.CanvasHeight = (int)CanvasBorder.ActualHeight;
            canvasView.SceneManager.CanvasWidth = (int)CanvasBorder.ActualWidth;
        }
    }
}