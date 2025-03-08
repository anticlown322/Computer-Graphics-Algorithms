using System.ComponentModel;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CGA.Core.Entities;
using CGA.Core.Parser;
using CGA.Core.Renderer;

namespace CGA.MVVM.Models;

public class CanvasModel : INotifyPropertyChanged
{
    #region INotifyPropertyChanged implementation

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    #endregion INotifyPropertyChanged implementation
}