using System.Numerics;
using System.Windows;
using System.Windows.Media.Imaging;
using CGA.Core.Entities;
using CGA.Core.Utils;

namespace CGA.Core.Shadings;

public class PhongShading : IShading
{
    // параметры освещения
    private static Vector3 _lightPos = new(1, 1, 1); // позиция источника света
    private static Vector3 _lightColor = new(0, 0, 1); // цвет света
    private static float   _ambientStrength = 0.2f;
    private static float   _specularStrength = 0.5f;
    private static int     _shininess = 32;
    
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
            Vector4 vertex = objectModel.GlobalVertices[idx];
            Vector3 vertexPos = vertex.XYZ();
            Vector3 viewDirection = eyePos - vertexPos;

            if (Vector3.Dot(face.VertexNormal, viewDirection) < 0)
                return;

            // коррекция нормали, если нужно
            if (Vector3.Dot(face.VertexNormal, Vector3.Normalize(eyePos)) > 0)
            {
                face.VertexNormal = -face.VertexNormal;
            }

            // отрисовка треугольника с интерполяцией нормалей
            for (int i = 1; i < count - 1; i++)
            {
                int idx1 = face.VertexIndexes[0] - 1;
                int idx2 = face.VertexIndexes[i] - 1;
                int idx3 = face.VertexIndexes[i + 1] - 1;

                Vector3[] screenVertices =
                [
                    objectModel.ProjectionVertices[idx1].XYZ(),
                    objectModel.ProjectionVertices[idx2].XYZ(),
                    objectModel.ProjectionVertices[idx3].XYZ()
                ];
                
                Vector3[] worldVertices =
                [
                    objectModel.GlobalVertices[idx1].XYZ(),
                    objectModel.GlobalVertices[idx2].XYZ(),
                    objectModel.GlobalVertices[idx3].XYZ()
                ];
                
                Vector3[] normals =
                [
                    face.NormalIndexes != null ? objectModel.Normals[face.NormalIndexes[0] - 1] : face.VertexNormal,
                    face.NormalIndexes != null ? objectModel.Normals[face.NormalIndexes[i] - 1] : face.VertexNormal,
                    face.NormalIndexes != null ? objectModel.Normals[face.NormalIndexes[i + 1] - 1] : face.VertexNormal
                ];
                
                RasterWithPhongShading(
                    screenVertices: screenVertices,
                    worldVertices: worldVertices,
                    normals: normals,
                    height: height,
                    width: width,
                    buffer: buffer,
                    eyePos: eyePos,
                    objectColor: color,
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

    private static unsafe void RasterWithPhongShading(
        Vector3[] screenVertices,
        Vector3[] worldVertices,
        Vector3[] normals,
        int height, int width,
        int* buffer, Vector3 eyePos, Vector3 objectColor, float[,] zBuffer)
    {
        SortVerticesByY(screenVertices, worldVertices, normals);
        
        float invDeltaY13 = 1.0f / (screenVertices[2].Y - screenVertices[0].Y);
        float invDeltaY12 = 1.0f / (screenVertices[1].Y - screenVertices[0].Y);
        float invDeltaY23 = 1.0f / (screenVertices[2].Y - screenVertices[1].Y);

        Vector2 edge13 = ((screenVertices[2] - screenVertices[0]) * invDeltaY13).XY();
        Vector2 edge12 = ((screenVertices[1] - screenVertices[0]) * invDeltaY12).XY();
        Vector2 edge23 = ((screenVertices[2] - screenVertices[1]) * invDeltaY23).XY();

        Vector3 world13 = (worldVertices[2] - worldVertices[0]) * invDeltaY13;
        Vector3 world12 = (worldVertices[1] - worldVertices[0]) * invDeltaY12;
        Vector3 world23 = (worldVertices[2] - worldVertices[1]) * invDeltaY23;

        Vector3 normal13 = (normals[2] - normals[0]) * invDeltaY13;
        Vector3 normal12 = (normals[1] - normals[0]) * invDeltaY12;
        Vector3 normal23 = (normals[2] - normals[1]) * invDeltaY23;

        float z13 = (screenVertices[2].Z - screenVertices[0].Z) * invDeltaY13;
        float z12 = (screenVertices[1].Z - screenVertices[0].Z) * invDeltaY12;
        float z23 = (screenVertices[2].Z - screenVertices[1].Z) * invDeltaY23;
        
        int startY = Math.Max(0, (int)MathF.Ceiling(screenVertices[0].Y));
        int endY = Math.Min(height, (int)MathF.Ceiling(screenVertices[2].Y));

        for (int y = startY; y < endY; y++)
        {
            float dy = y - screenVertices[0].Y;
            
            Vector2 aPoint, bPoint;
            Vector3 aWorld, bWorld;
            Vector3 aNormal, bNormal;
            float aZ, bZ;

            if (y < screenVertices[1].Y)
            {
                aPoint = screenVertices[0].XY() + edge13 * dy;
                bPoint = screenVertices[0].XY() + edge12 * dy;
                aWorld = worldVertices[0] + world13 * dy;
                bWorld = worldVertices[0] + world12 * dy;
                aNormal = normals[0] + normal13 * dy;
                bNormal = normals[0] + normal12 * dy;
                aZ = screenVertices[0].Z + z13 * dy;
                bZ = screenVertices[0].Z + z12 * dy;
            }
            else
            {
                dy = y - screenVertices[1].Y;
                aPoint = screenVertices[0].XY() + edge13 * (y - screenVertices[0].Y);
                bPoint = screenVertices[1].XY() + edge23 * dy;
                aWorld = worldVertices[0] + world13 * (y - screenVertices[0].Y);
                bWorld = worldVertices[1] + world23 * dy;
                aNormal = normals[0] + normal13 * (y - screenVertices[0].Y);
                bNormal = normals[1] + normal23 * dy;
                aZ = screenVertices[0].Z + z13 * (y - screenVertices[0].Y);
                bZ = screenVertices[1].Z + z23 * dy;
            }

            if (aPoint.X > bPoint.X)
            {
                (aPoint, bPoint) = (bPoint, aPoint);
                (aWorld, bWorld) = (bWorld, aWorld);
                (aNormal, bNormal) = (bNormal, aNormal);
                (aZ, bZ) = (bZ, aZ);
            }
            
            int startX = Math.Max(0, (int)MathF.Ceiling(aPoint.X));
            int endX = Math.Min(width, (int)MathF.Ceiling(bPoint.X));

            if (startX >= endX) 
                continue;
            
            float dx = endX - startX;
            float tStep = 1.0f / dx;
            float t = 0;

            for (int x = startX; x < endX; x++, t += tStep)
            {
                Vector3 pixelWorld = aWorld + (bWorld - aWorld) * t;
                Vector3 pixelNormal = Vector3.Normalize(aNormal + (bNormal - aNormal) * t);
                float pixelZ = aZ + (bZ - aZ) * t;

                if (pixelZ >= zBuffer[y, x]) 
                    continue;

                Vector3 lightDir = Vector3.Normalize(_lightPos - pixelWorld);
                Vector3 viewDir = Vector3.Normalize(eyePos - pixelWorld);
                Vector3 reflectDir = Vector3.Reflect(-lightDir, pixelNormal);
                Vector3 ambient = _ambientStrength * _lightColor;
                
                float diff = MathF.Max(Vector3.Dot(pixelNormal, lightDir), 0.0f);
                Vector3 diffuse = diff * _lightColor;
                
                float spec = MathF.Pow(MathF.Max(Vector3.Dot(viewDir, reflectDir), 0.0f), _shininess);
                Vector3 specular = _specularStrength * spec * _lightColor;
                
                Vector3 result = (ambient + diffuse + specular) * objectColor;
                result = Vector3.Clamp(result, Vector3.Zero, Vector3.One);

                int color = ColorUtils.ColorToInt(result);
                int index = y * width + x;
                
                buffer[index] = color;
                zBuffer[y, x] = pixelZ;
            }
        }
    }

    private static void SortVerticesByY(Vector3[] screenVertices, Vector3[] worldVertices, Vector3[] normals)
    {
        if (screenVertices[0].Y > screenVertices[2].Y)
        {
            (screenVertices[0], screenVertices[2]) = (screenVertices[2], screenVertices[0]);
            (worldVertices[0], worldVertices[2]) = (worldVertices[2], worldVertices[0]);
            (normals[0], normals[2]) = (normals[2], normals[0]);
        }

        if (screenVertices[0].Y > screenVertices[1].Y)
        {
            (screenVertices[0], screenVertices[1]) = (screenVertices[1], screenVertices[0]);
            (worldVertices[0], worldVertices[1]) = (worldVertices[1], worldVertices[0]);
            (normals[0], normals[1]) = (normals[1], normals[0]);
        }

        if (screenVertices[1].Y > screenVertices[2].Y)
        {
            (screenVertices[1], screenVertices[2]) = (screenVertices[2], screenVertices[1]);
            (worldVertices[1], worldVertices[2]) = (worldVertices[2], worldVertices[1]);
            (normals[1], normals[2]) = (normals[2], normals[1]);
        }
    }
}