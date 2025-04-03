using System.Numerics;
using System.Windows;
using System.Windows.Media.Imaging;
using CGA.Core.Entities;
using CGA.Core.Utils;

namespace CGA.Core.Shadings;

public class FlatShading : IShading
{
    public unsafe void DrawShading(ObjectModel objectModel, WriteableBitmap bitmap, Vector3 color, Vector3 eyePos, float[,] zBuffer)
    {
        int width = bitmap.PixelWidth;
        int height = bitmap.PixelHeight;

        bitmap.Lock();

        int* buffer = (int*)bitmap.BackBuffer;

        Parallel.ForEach(objectModel.Faces, face =>
        {
            int count = face.VertexIndexes.Length;
            if (count < 3)
                return;

            // отбраковка
            int idx = face.VertexIndexes[0] - 1;
            Vector3 vertexPos = objectModel.GlobalVertices[idx].GetVector3XYZ();
            Vector3 viewDirection = eyePos - vertexPos;

            if (Vector3.Dot(face.VertexNormal, viewDirection) < 0)
                return;

            // затенение + освещение
            if (Vector3.Dot(face.VertexNormal, Vector3.Normalize(eyePos)) > 0)
            {
                face.VertexNormal = -face.VertexNormal;
            }

            Vector3 lightDirection = new Vector3(0, 0.5f, 1);
            double strength = MathF.Max(Vector3.Dot(face.VertexNormal, -lightDirection), 0);
            
            int shadedColorBgra = ColorUtils.ColorToInt(
                color: color, 
                strength: strength, 
                alpha: 255);

            // отрисовка
            for (int i = 1; i < count - 1; i++)
            {
                int idx1 = face.VertexIndexes[0] - 1;
                int idx2 = face.VertexIndexes[i] - 1;
                int idx3 = face.VertexIndexes[i + 1] - 1;

                Vector3 screenVertex1 = objectModel.ProjectionVertices[idx1].GetVector3XYZ();
                Vector3 screenVertex2 = objectModel.ProjectionVertices[idx2].GetVector3XYZ();
                Vector3 screenVertex3 = objectModel.ProjectionVertices[idx3].GetVector3XYZ();

                RasterWithScanningLine(
                    vertex1: screenVertex1,
                    vertex2: screenVertex2,
                    vertex3: screenVertex3,
                    height:  height,
                    width:   width,
                    buffer:  buffer,
                    color:   shadedColorBgra,
                    zBuffer: zBuffer);
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
        Vector3 vertex1, Vector3 vertex2, Vector3 vertex3,
        int height, int width,
        int* buffer, int color, float[,] zBuffer)
    {
        // отсортировать вершины по Y
        if (vertex1.Y > vertex3.Y)
        {
            (vertex1, vertex3) = (vertex3, vertex1);
        }

        if (vertex1.Y > vertex2.Y)
        {
            (vertex1, vertex2) = (vertex2, vertex1);
        }

        if (vertex2.Y > vertex3.Y)
        {
            (vertex2, vertex3) = (vertex3, vertex2);
        }

        // кэфы для упрощения
        Vector3 coefVer1 = (vertex3 - vertex1) / (vertex3.Y - vertex1.Y);
        Vector3 coefVer2 = (vertex2 - vertex1) / (vertex2.Y - vertex1.Y);
        Vector3 coefVer3 = (vertex3 - vertex2) / (vertex3.Y - vertex2.Y);

        // границы перебора
        int top = Math.Max(0, (int)Math.Ceiling(vertex1.Y));
        int bottom = Math.Min(height, (int)Math.Ceiling(vertex3.Y));

        // алгоритм отрисовки
        for (int y = top; y < bottom; y++)
        {
            Vector3 aPoint = vertex1 + (y - vertex1.Y) * coefVer1;
            Vector3 bPoint = y < vertex2.Y
                ? vertex1 + (y - vertex1.Y) * coefVer2
                : vertex2 + (y - vertex2.Y) * coefVer3;

            if (aPoint.X > bPoint.X)
            {
                (aPoint, bPoint) = (bPoint, aPoint);
            }

            int left = Math.Max(0, (int)Math.Ceiling(aPoint.X));
            int right = Math.Min(width, (int)Math.Ceiling(bPoint.X));

            for (int x = left; x < right; x++)
            {
                float t = (x - aPoint.X) / (bPoint.X - aPoint.X);
                float z = aPoint.Z + t * (bPoint.Z - aPoint.Z);

                int index = y * width + x;
                if (z < zBuffer[y, x])
                {
                    buffer[index] = color;
                    zBuffer[y, x] = z;
                }
            }
        }
    }
}