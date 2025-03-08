using System.Numerics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CGA.Core;
using CGA.Core.Entities;
using CGA.Core.Parser;
using CGA.Core.Renderer;
using Microsoft.Win32;

namespace CGA.MVVM.ViewModels;

public class CanvasViewModel : ObservableObject
{
    private string _filePath = string.Empty;
    private WriteableBitmap? _writeableBitmap;
    private SceneManager _sceneManager = new();
    private Point _lastMousePos;

    #region Commands
    public RelayCommand LoadFileCommand { get; }
    public RelayCommand MouseWheelCommand { get; }
    public RelayCommand MouseMoveCommand { get; }
    public RelayCommand KeyPressCommand { get; }
    #endregion Commands
    
    #region Public properties for private fields
    public SceneManager SceneManager
    {
        get => _sceneManager;
        set
        {
            _sceneManager = value;
            OnPropertyChanged();
        }
    }
    
    public WriteableBitmap? WriteableBitmap
    {
        get => _writeableBitmap;
        set
        {
            _writeableBitmap = value;
            OnPropertyChanged();
        }
    }
    #endregion

    public CanvasViewModel()
    {
        LoadFileCommand = new RelayCommand(LoadFile);
        MouseWheelCommand = new RelayCommand(OnMouseWheel);
        MouseMoveCommand = new RelayCommand(OnMouseMove);
        KeyPressCommand = new RelayCommand(OnKeyPress);
    }

    void LoadFile(object parameter)
    {
        try
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == true)
            {
                _filePath = openFileDialog.FileName;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message);
        }
        
        WriteableBitmap ??= new WriteableBitmap(
            pixelWidth:  SceneManager.CanvasWidth, 
            pixelHeight: SceneManager.CanvasHeight, 
            dpiX:        96, 
            dpiY:        96, 
            pixelFormat: PixelFormats.Bgra32, 
            palette:     null);

        SceneManager.ObjectModel = Parser.LoadFromFile(_filePath);
        
        SceneManager.CameraModel.ChangeEyePosition();
        SceneManager.TransformObject();
        WireframeRenderer.RenderModel(
            objectModel: SceneManager.ObjectModel, 
            bitmap:      WriteableBitmap, 
            zNear:       SceneManager.CameraModel.ZNear, 
            zFar:        SceneManager.CameraModel.ZFar);
    }
    
    private void OnMouseWheel(object parameter)
    {
        if (SceneManager.ObjectModel is null)
            return;

        if (parameter is not MouseWheelEventArgs args) 
            return;
        
        SceneManager.CameraModel.Radius -= args.Delta / 1000.0f;
        
        if (SceneManager.CameraModel.Radius < SceneManager.CameraModel.ZNear)
            SceneManager.CameraModel.Radius = SceneManager.CameraModel.ZNear;
        
        if (SceneManager.CameraModel.Radius > SceneManager.CameraModel.ZFar)
            SceneManager.CameraModel.Radius = SceneManager.CameraModel.ZFar;
    }

    private void OnMouseMove(object parameter)
    {
        if (SceneManager.ObjectModel is null)
            return;

        if (parameter is not MouseEventArgs args) 
            return;
        
        var currentPos = args.GetPosition(null);
        var delta = currentPos - _lastMousePos;
        
        if (args.LeftButton == MouseButtonState.Pressed)
        {
            SceneManager.ObjectModel.Rotation = new Vector3(
                SceneManager.ObjectModel.Rotation.X + (float)delta.X * MathF.PI / 360.0f,
                SceneManager.ObjectModel.Rotation.Y,
                SceneManager.ObjectModel.Rotation.Z);
        }
        else if (args.RightButton == MouseButtonState.Pressed)
        {
            SceneManager.ObjectModel.Rotation = new Vector3(
                SceneManager.ObjectModel.Rotation.X,
                SceneManager.ObjectModel.Rotation.Y  + (float)delta.X * MathF.PI / 360.0f,
                SceneManager.ObjectModel.Rotation.Z);
        }
        
        _lastMousePos = currentPos;
        SceneManager.CameraModel.ChangeEyePosition();
        SceneManager.TransformObject();
        WireframeRenderer.RenderModel(
            objectModel: SceneManager.ObjectModel, 
            bitmap:      WriteableBitmap, 
            zNear:       SceneManager.CameraModel.ZNear, 
            zFar:        SceneManager.CameraModel.ZFar);
    }

    private void OnKeyPress(object parameter)
    {
        if (SceneManager.ObjectModel is null)
            return;

        if (parameter is not Key key) 
            return;

        const float delta = 0.05f;
        
        switch (key)
        {
            case Key.W: SceneManager.ObjectModel.Position += new Vector3(0, delta, 0); break;
            case Key.A: SceneManager.ObjectModel.Position += new Vector3(-delta, 0, 0); break;
            case Key.S: SceneManager.ObjectModel.Position += new Vector3(0, -delta, 0); break;
            case Key.D: SceneManager.ObjectModel.Position += new Vector3(delta, 0, 0); break;
        }

        SceneManager.CameraModel.ChangeEyePosition();
        SceneManager.TransformObject();
        WireframeRenderer.RenderModel(
            objectModel: SceneManager.ObjectModel, 
            bitmap:      WriteableBitmap, 
            zNear:       SceneManager.CameraModel.ZNear, 
            zFar:        SceneManager.CameraModel.ZFar);
    }

}