using System.Windows;
using CGA.Core;

namespace CGA.MVVM.ViewModels;

public class MainViewModel : ObservableObject
{
    public CanvasViewModel CanvasVm { get; set; }

    public RelayCommand MinimizeWindowCommand { get; set; }
    public RelayCommand CloseWindowCommand { get; set; }
    public RelayCommand StartLoadingFileCommand { get; set; }
    public RelayCommand KeyPressCommand { get; set; }
    public RelayCommand CanvasViewCommand { get; set; }

    private object _currentView;
    public object CurrentView
    {
        get => _currentView;
        set
        {
            _currentView = value;
            OnPropertyChanged();
        }
    }

    public MainViewModel()
    {
        CanvasVm = new CanvasViewModel();

        CurrentView = CanvasVm; 
        
        MinimizeWindowCommand = new RelayCommand(MinimizeWindow);
        CloseWindowCommand = new RelayCommand(CloseWindow);
        StartLoadingFileCommand = new RelayCommand(StartLoadingFile);
        CanvasViewCommand = new RelayCommand(obj => CurrentView = CanvasVm );
        KeyPressCommand = new RelayCommand(OnKeyPress);
    }
    
    void MinimizeWindow(object parameter)
    {
        if (parameter is Window window)
        {
            window.WindowState = WindowState.Minimized;
        }
    }

    void CloseWindow(object parameter)
    {
        if (parameter is Window window)
        {
            window.Close();
        }
    }
    
    void StartLoadingFile(object parameter)
    {
        CurrentView = CanvasVm; 
        CanvasVm.LoadFileCommand.Execute(parameter);
    }

    private void OnKeyPress(object parameter)
    {
        if (CurrentView is CanvasViewModel canvas)
        {
            canvas.KeyPressCommand.Execute(parameter);
        }
    }
}