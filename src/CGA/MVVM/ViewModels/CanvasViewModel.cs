using System.IO;
using System.Numerics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CGA.Core.Entities;
using CGA.Core.Parsers;
using CGA.Core.Renderers;
using CGA.Core.Shadings;
using Microsoft.Win32;
using Material = CGA.Core.Entities.Material;

namespace CGA.MVVM.ViewModels;

public class CanvasViewModel : ObservableObject
{
    private string _filePath = string.Empty;
    private WriteableBitmap? _writeableBitmap;
    private SceneManager _sceneManager;
    private Point _mousePosition;
    private RendererType _selectedRenderer;
    private ShadingType _selectedShading;

    private Dictionary<string, Material> _materials;
    private Dictionary<string, TextureMap> _textureMaps = new();

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

    public RendererType SelectedRenderer
    {
        get => _selectedRenderer;
        set
        {
            _selectedRenderer = value;
            OnPropertyChanged();
        }
    }

    public ShadingType SelectedShading
    {
        get => _selectedShading;
        set
        {
            _selectedShading = value;
            OnPropertyChanged();
        }
    }

    #endregion

    #region Commands

    public RelayCommand LoadFileCommand { get; }
    public RelayCommand MouseWheelCommand { get; }
    public RelayCommand MouseMoveCommand { get; }
    public RelayCommand KeyPressCommand { get; }

    #endregion Commands

    public CanvasViewModel()
    {
        LoadFileCommand = new RelayCommand(LoadFile);
        MouseWheelCommand = new RelayCommand(OnMouseWheel);
        MouseMoveCommand = new RelayCommand(OnMouseMove);
        KeyPressCommand = new RelayCommand(OnKeyPress);

        SceneManager = new SceneManager();
    }

    internal void OnViewLoaded()
    {
        WriteableBitmap = new WriteableBitmap(
            pixelWidth: SceneManager.CanvasWidth,
            pixelHeight: SceneManager.CanvasHeight,
            dpiX: 96,
            dpiY: 96,
            pixelFormat: PixelFormats.Bgra32,
            palette: null);
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

        UpdateCanvas();
    }

    private void OnMouseMove(object parameter)
    {
        if (SceneManager.ObjectModel is null)
            return;

        if (parameter is not MouseEventArgs args)
            return;

        var currentMousePosition = args.GetPosition(null);
        var delta = currentMousePosition - _mousePosition;

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
                SceneManager.ObjectModel.Rotation.Y + (float)delta.X * MathF.PI / 360.0f,
                SceneManager.ObjectModel.Rotation.Z);
        }

        _mousePosition = currentMousePosition;

        UpdateCanvas();
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

        UpdateCanvas();
    }

    private void LoadFile(object parameter)
    {
        try
        {
            var openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == true)
            {
                _filePath = openFileDialog.FileName;
            }
            else
            {
                // файл не выбран
                return;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка при выборе файла: {ex.Message}", "Ошибка", MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        try
        {
            SceneManager.ObjectModel = ObjFileParser.LoadFromFile(_filePath);
            _materials = MtlFileParser.LoadFromFile(SceneManager.ObjectModel.PathToMtlFile);
            LoadTextureMaps();
            UpdateCanvas();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка при загрузке файла: {ex.Message}", "Ошибка", MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void LoadTextureMaps()
    {
        foreach (var material in _materials)
        {
            var materialValue = material.Value;

            try
            {
                if (!_textureMaps.ContainsKey(materialValue.DiffuseMap) &&
                    !string.IsNullOrEmpty(materialValue.DiffuseMap))
                {
                    _textureMaps.TryAdd(materialValue.DiffuseMap, new TextureMap(materialValue.DiffuseMap));
                }

                if (!_textureMaps.ContainsKey(materialValue.NormalMap) &&
                    !string.IsNullOrEmpty(materialValue.NormalMap))
                {
                    _textureMaps.TryAdd(materialValue.NormalMap, new TextureMap(materialValue.NormalMap));
                }

                if (!_textureMaps.ContainsKey(materialValue.SpecularMap) &&
                    !string.IsNullOrEmpty(materialValue.SpecularMap))
                {
                    _textureMaps.TryAdd(materialValue.SpecularMap, new TextureMap(materialValue.SpecularMap));
                }
            }
            catch (FileNotFoundException ex)
            {
                MessageBox.Show($"Ошибка при загрузке текстур: {ex.Message}", "Ошибка", MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }

    private void UpdateCanvas()
    {
        SceneManager.CameraModel.ChangeEyePosition();
        SceneManager.TransformObject();

        switch (SelectedRenderer)
        {
            case RendererType.Wireframe:
            {
                WireframeRenderer.RenderModel(
                    objectModel: SceneManager.ObjectModel,
                    bitmap: WriteableBitmap,
                    zNear: SceneManager.CameraModel.ZNear,
                    zFar: SceneManager.CameraModel.ZFar,
                    color: new Vector3(1, 1, 1));
                break;
            }

            case RendererType.Rasterized:
            {
                RasterRenderer.RenderModel(
                    objectModel: SceneManager.ObjectModel,
                    bitmap: WriteableBitmap,
                    color: new Vector3(1, 1, 1),
                    eyePos: SceneManager.CameraModel.EyePosition,
                    shading: SelectedShading);
                break;
            }

            case RendererType.Textured:
            {
                TextureRenderer.RenderModel(
                    objectModel: SceneManager.ObjectModel,
                    bitmap: WriteableBitmap,
                    eyePos: SceneManager.CameraModel.EyePosition,
                    materials: _materials.Select(kvp => kvp.Value).ToList(),
                    textureMaps: _textureMaps);
                break;
            }


            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}