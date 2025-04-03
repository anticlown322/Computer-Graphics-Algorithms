using System.Numerics;
using System.Windows;
using System.Windows.Media.Imaging;
using CGA.Core.Entities;
using CGA.Core.Shadings;
using static System.Windows.Forms.DataFormats;

namespace CGA.Core.Renderer;

public static class RasterRenderer
{
    private static float[,]? _zBuffer;
    private static IShading? _shading;
    private static ShadingType CurrentShading { get; set; }

    public static void RenderModel(ObjectModel objectModel, WriteableBitmap bitmap, Vector3 color, Vector3 eyePos, ShadingType shading)
    {
        if (bitmap is null)
            throw new ArgumentNullException(nameof(bitmap));
        
        switch (shading)
        {
            case ShadingType.Flat:
                _shading = new FlatShading();
                break;
            case ShadingType.Phong:
                _shading = new PhongShading();
                break;
        }
        
        ClearBitmap(bitmap, new(0, 0, 0));
        ClearZBuffer(bitmap.PixelWidth, bitmap.PixelHeight);
        _shading.DrawShading(objectModel, bitmap, color, eyePos, _zBuffer);
    }
    
    private static unsafe void ClearBitmap(WriteableBitmap bitmap, Vector3 color)
    {
        int intColor = 255 << 24 | (int)(255 * color.X) << 16 | (int)(255 * color.Y) << 8 | (int)(255 * color.Z);

        bitmap.Lock();

        int* pBackBuffer = (int*)bitmap.BackBuffer;
        for (int i = 0; i < bitmap.PixelWidth * bitmap.PixelHeight; i++)
        {
            pBackBuffer[i] = intColor;
        }

        try
        {
            bitmap.AddDirtyRect(new Int32Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight));
        }
        finally
        {
            bitmap.Unlock();
        }
    }

    private static void ClearZBuffer(int width, int height)
    {
        _zBuffer ??= new float[height, width];

        for (int i = 0; i < height; i++)
        {
            for (int j = 0; j < width; j++)
            {
                _zBuffer[i, j] = 1f;
            }
        }
    }
}