using System.Numerics;
using System.Windows;
using System.Windows.Media.Imaging;
using CGA.Core.Entities;
using static System.Windows.Forms.DataFormats;

namespace CGA.Core.Renderer;

public static class RasterRenderer
{
    private static float[,]? _zBuffer;
    
    public static void RenderModel(ObjectModel objectModel, WriteableBitmap bitmap, Vector3 color, Vector3 eyePos)
    {
        if (bitmap is null)
            throw new ArgumentNullException(nameof(bitmap));

        ClearBitmap(bitmap, new(0, 0, 0));
        ClearZBuffer(bitmap.PixelWidth, bitmap.PixelHeight);
        Draw(objectModel, bitmap, color, eyePos);
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

    private static unsafe void Draw(ObjectModel objectModel, WriteableBitmap bitmap, Vector3 color, Vector3 eyePos)
    {
        int width = bitmap.PixelWidth;
        int height = bitmap.PixelHeight;

        bitmap.Lock();
        
        int* buffer = (int*)bitmap.BackBuffer;

        Parallel.ForEach(objectModel.Faces, face =>
        {
            int count = face.vertexIndexes.Length;
            if (count < 3)
                return;

            // отбраковка
            int idx               = face.vertexIndexes[0] - 1;
            Vector4 vertex        = objectModel.GlobalVertices[idx];
            Vector3 vertexPos     = new Vector3(vertex.X, vertex.Y, vertex.Z);
            Vector3 viewDirection = eyePos - vertexPos;
                
            if (Vector3.Dot(face.vertexNormal, viewDirection ) < 0)
                return;

            // освещение
            if (Vector3.Dot(face.vertexNormal, Vector3.Normalize(eyePos)) > 0)
            {
                face.vertexNormal = -face.vertexNormal;
            }

            Vector3 lightDirection = new Vector3(0, 0.5f, 1);
            Vector3 baseColor = new Vector3(1, 0, 0);
            double strength = MathF.Max(Vector3.Dot(face.vertexNormal, -lightDirection), 0);

            int r = (int)Math.Round(strength * baseColor.X * 255);
            int g = (int)Math.Round(strength * baseColor.Y * 255);
            int b = (int)Math.Round(strength * baseColor.Z * 255); 
            r = Math.Clamp(r, 0, 255);
            g = Math.Clamp(g, 0, 255);
            b = Math.Clamp(b, 0, 255);
            int a = 255; 
            int shadedColorBgra = (a << 24) | (r << 16) | (g << 8) | b;

            // отрисовка
            for (int i = 1; i < count - 1; i++)
            {
                int idx1 = face.vertexIndexes[0] - 1;
                int idx2 = face.vertexIndexes[i] - 1;
                int idx3 = face.vertexIndexes[i + 1] - 1;
                
                Vector2 screenVertex1 = new Vector2(objectModel.ProjectionVertices[idx1].X, objectModel.ProjectionVertices[idx1].Y);
                Vector2 screenVertex2 = new Vector2(objectModel.ProjectionVertices[idx2].X, objectModel.ProjectionVertices[idx2].Y);
                Vector2 screenVertex3 = new Vector2(objectModel.ProjectionVertices[idx3].X, objectModel.ProjectionVertices[idx3].Y);

                RasterWithScanningLine(
                    vertex1:   screenVertex1,
                    vertex2:   screenVertex2,
                    vertex3:   screenVertex3,
                    z1:        objectModel.ProjectionVertices[idx1].Z,
                    z2:        objectModel.ProjectionVertices[idx2].Z,
                    z3:        objectModel.ProjectionVertices[idx3].Z,
                    height:    height,
                    width:     width,
                    buffer:    buffer,
                    shadedColorBgra);
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
        float z1, float z2, float z3, int height, int width, 
        int* buffer, int shadedColorBgra)
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
                float z = zA + t * (zB - zA); 
                
                int index = y * width + x;
                if (z < _zBuffer[y, x])
                {
                    buffer[index] = shadedColorBgra;
                    _zBuffer[y, x] = z; 
                }
            }
        }
    }
}