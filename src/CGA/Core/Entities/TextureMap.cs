using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CGA.Core.Entities;

public class TextureMap
{
    private readonly byte[] _pixels;
    private readonly int _width;
    private readonly int _height;
    private readonly int _stride;

    public TextureMap(string path)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = new Uri(path, UriKind.RelativeOrAbsolute);
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();
        image.Freeze();
        
        _width = image.PixelWidth;
        _height = image.PixelHeight;
        _stride = _width * 4;
        _pixels = new byte[_height * _stride];
        
        image.CopyPixels(_pixels, _stride, 0);
    }
    
    public Color GetColor(float u, float v)
    {
        var x = (int)(u * (_width - 1));
        var y = (int)(v * (_height - 1));

        // cyclic traversal, if x > width or y > height 
        x %= _width;
        y %= _height;
        var offset = y * _stride + x * 4;
        
        return Color.FromArgb(
            _pixels[offset + 3],
            _pixels[offset + 2], 
            _pixels[offset + 1], 
            _pixels[offset]      
        );
    }
}