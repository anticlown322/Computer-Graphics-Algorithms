using System.Numerics;
using System.Windows;
using System.Windows.Media.Imaging;
using CGA.Core.Entities;

namespace CGA.Core.Renderer;

public static class RasterRenderer
{
    private static float[,]? _zBuffer;
    
    public static void RenderModel(ObjectModel objectModel, WriteableBitmap bitmap, Vector3 color)
    {
        if (bitmap is null)
            throw new ArgumentNullException(nameof(bitmap));

        ClearBitmap(bitmap, new(0, 0, 0));
        ClearZBuffer(bitmap.PixelWidth, bitmap.PixelHeight);
        Draw(objectModel, bitmap, color);
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

    private static unsafe void Draw(ObjectModel objectModel, WriteableBitmap bitmap, Vector3 color)
    {
        int width = bitmap.PixelWidth;
        int height = bitmap.PixelHeight;
        int colorBgra = 255 << 24 | (int)(255 * color.X) << 16 | (int)(255 * color.Y) << 8 | (int)(255 * color.Z);

        bitmap.Lock();
        
        int* buffer = (int*)bitmap.BackBuffer;

        Parallel.ForEach(objectModel.Faces, face =>
        {
            int count = face.Length;
            if (count < 3)
                return;

            for (int i = 1; i < count - 1; i++)
            {
                int idx1 = face[0] - 1;
                int idx2 = face[i] - 1;
                int idx3 = face[i + 1] - 1;
                
                Vector2 screenVertex1 = new Vector2(objectModel.GlobalVertices[idx1].X, objectModel.GlobalVertices[idx1].Y);
                Vector2 screenVertex2 = new Vector2(objectModel.GlobalVertices[idx2].X, objectModel.GlobalVertices[idx2].Y);
                Vector2 screenVertex3 = new Vector2(objectModel.GlobalVertices[idx3].X, objectModel.GlobalVertices[idx3].Y);

                RasterWithScanningLine(
                    vertex1:   screenVertex1,
                    vertex2:   screenVertex2,
                    vertex3:   screenVertex3,
                    z1:        objectModel.GlobalVertices[idx1].Z,
                    z2:        objectModel.GlobalVertices[idx2].Z,
                    z3:        objectModel.GlobalVertices[idx3].Z,
                    colorBgra: colorBgra,
                    height:    height,
                    width:     width,
                    buffer:    buffer);
            }
        });
            
        try
        {
            bitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
        }
        finally
        {
            bitmap.Unlock();
        }
    }

    private static unsafe void RasterWithScanningLine(
        Vector2 vertex1, Vector2 vertex2, Vector2 vertex3, 
        float z1, float z2, float z3,
        int colorBgra, int height, int width, 
        int* buffer)
    {
        // sotring vertices
        if (vertex1.Y > vertex3.Y)
        {
            (vertex1, vertex3) = (vertex3, vertex1);
            (z1, z3) = (z3, z1);
        }
        
        if (vertex1.Y > vertex2.Y)
        {
            (vertex1, vertex2) = (vertex2, vertex1);
            (z1, z2) = (z2, z1);
        }
        
        if (vertex2.Y > vertex3.Y)
        {
            (vertex2, vertex3) = (vertex3, vertex2);
            (z2, z3) = (z3, z2);
        }
        
        // preparation
        Vector2 coeffVer1 = (vertex3 - vertex1) / (vertex3.Y - vertex1.Y);
        Vector2 coeffVer2 = (vertex2 - vertex1) / (vertex2.Y - vertex1.Y);
        Vector2 coeffVer3 = (vertex3 - vertex2) / (vertex3.Y - vertex2.Y);
        
        float coeffZ1 = (z3 - z1) / (vertex3.Y - vertex1.Y);
        float coeffZ2 = (z2 - z1) / (vertex2.Y - vertex1.Y);
        float coeffZ3 = (z3 - z2) / (vertex3.Y - vertex2.Y);
        
        int top = Math.Max(0, (int)Math.Ceiling(vertex1.Y));
        int bottom = Math.Min(height, (int)Math.Ceiling(vertex3.Y));

        // drawing
        for (int y = top; y < bottom; y++)
        {
            Vector2 aPoint = vertex1 + (y - vertex1.Y) * coeffVer1;
            Vector2 bPoint = y < vertex2.Y ? 
                vertex1 + (y - vertex1.Y) * coeffVer2 :
                vertex2 + (y - vertex2.Y) * coeffVer3;

            float zA = z1 + (y - vertex1.Y) * coeffZ1;
            float zB = y < vertex2.Y ? 
                z1 + (y - vertex1.Y) * coeffZ2 :
                z2 + (y - vertex2.Y) * coeffZ3;

            if (aPoint.X > bPoint.X)
            {
                (aPoint, bPoint) = (bPoint, aPoint);
                (zA, zB) = (zB, zA);
            }
            
            int left = Math.Max(0, (int)Math.Ceiling(aPoint.X));
            int right = Math.Min(width, (int)Math.Ceiling(bPoint.X));
            
            for (int x = left; x < right; x++)
            {
                float t = (x - aPoint.X) / (bPoint.X - aPoint.X);
                float z = zA + t * (zB - zA); // depth interpolation
                
                int index = y * width + x;
                if (z < _zBuffer[y, x])
                {
                    buffer[index] = colorBgra;
                    _zBuffer[y, x] = z; 
                }
            }
        }
    }
}