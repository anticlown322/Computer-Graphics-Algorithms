using System.Numerics;
using System.Windows;
using System.Windows.Media.Imaging;
using CGA.Core.Entities;
using static System.Windows.Forms.DataFormats;

namespace CGA.Core.Renderer;


    public enum ShadingType
{
    Flat,
    Phong    
}


public static class RasterRenderer
{
    private static float[,]? _zBuffer;
    public static ShadingType CurrentShading { get; set; } = ShadingType.Flat;

    public static void RenderModel(ObjectModel objectModel, WriteableBitmap bitmap, Vector3 color, Vector3 eyePos)
    {
        if (bitmap is null)
            throw new ArgumentNullException(nameof(bitmap));

        ClearBitmap(bitmap, new(0, 0, 0));
        ClearZBuffer(bitmap.PixelWidth, bitmap.PixelHeight);
        switch (CurrentShading)
        {
            case ShadingType.Flat:
                DrawFlatShading(objectModel, bitmap, color, eyePos);
                break;
            case ShadingType.Phong:
                DrawPhongShading(objectModel, bitmap, color, eyePos);
                break;
        }
    }

    private static unsafe void DrawPhongShading(ObjectModel objectModel, WriteableBitmap bitmap, Vector3 color, Vector3 eyePos)
    {
        int width = bitmap.PixelWidth;
        int height = bitmap.PixelHeight;

        bitmap.Lock();

        int* buffer = (int*)bitmap.BackBuffer;

        // Параметры освещения
        Vector3 lightPos = new Vector3(1, 1, 1); // Позиция источника света
        Vector3 lightColor = new Vector3(0, 0, 1); // Цвет света
        float ambientStrength = 0.2f;
        float specularStrength = 0.5f;
        int shininess = 32;

        Parallel.ForEach(objectModel.Faces, face =>
        {
            int count = face.vertexIndexes.Length;
            if (count < 3)
                return;

            // Отбраковка невидимых граней
            int idx = face.vertexIndexes[0] - 1;
            Vector4 vertex = objectModel.GlobalVertices[idx];
            Vector3 vertexPos = new Vector3(vertex.X, vertex.Y, vertex.Z);
            Vector3 viewDirection = eyePos - vertexPos;

            if (Vector3.Dot(face.vertexNormal, viewDirection) < 0)
                return;

            // Коррекция нормали, если нужно
            if (Vector3.Dot(face.vertexNormal, Vector3.Normalize(eyePos)) > 0)
            {
                face.vertexNormal = -face.vertexNormal;
            }

            // Отрисовка треугольника с интерполяцией нормалей
            for (int i = 1; i < count - 1; i++)
            {
                int idx1 = face.vertexIndexes[0] - 1;
                int idx2 = face.vertexIndexes[i] - 1;
                int idx3 = face.vertexIndexes[i + 1] - 1;

                Vector2 screenVertex1 = new Vector2(objectModel.ProjectionVertices[idx1].X, objectModel.ProjectionVertices[idx1].Y);
                Vector2 screenVertex2 = new Vector2(objectModel.ProjectionVertices[idx2].X, objectModel.ProjectionVertices[idx2].Y);
                Vector2 screenVertex3 = new Vector2(objectModel.ProjectionVertices[idx3].X, objectModel.ProjectionVertices[idx3].Y);

                // Получаем нормали вершин
                Vector3 normal1 = face.normalIndexes != null ?
                    objectModel.Normals[face.normalIndexes[0] - 1] :
                    face.vertexNormal;

                Vector3 normal2 = face.normalIndexes != null ?
                    objectModel.Normals[face.normalIndexes[i] - 1] :
                    face.vertexNormal;

                Vector3 normal3 = face.normalIndexes != null ?
                    objectModel.Normals[face.normalIndexes[i + 1] - 1] :
                    face.vertexNormal;

                RasterWithPhongShading(
                    vertex1: screenVertex1,
                    vertex2: screenVertex2,
                    vertex3: screenVertex3,
                    worldPos1: new Vector3(objectModel.GlobalVertices[idx1].X, objectModel.GlobalVertices[idx1].Y, objectModel.GlobalVertices[idx1].Z),
                    worldPos2: new Vector3(objectModel.GlobalVertices[idx2].X, objectModel.GlobalVertices[idx2].Y, objectModel.GlobalVertices[idx2].Z),
                    worldPos3: new Vector3(objectModel.GlobalVertices[idx3].X, objectModel.GlobalVertices[idx3].Y, objectModel.GlobalVertices[idx3].Z),
                    normal1: normal1,
                    normal2: normal2,
                    normal3: normal3,
                    z1: objectModel.ProjectionVertices[idx1].Z,
                    z2: objectModel.ProjectionVertices[idx2].Z,
                    z3: objectModel.ProjectionVertices[idx3].Z,
                    height: height,
                    width: width,
                    buffer: buffer,
                    eyePos: eyePos,
                    lightPos: lightPos,
                    lightColor: lightColor,
                    objectColor: color,
                    ambientStrength: ambientStrength,
                    specularStrength: specularStrength,
                    shininess: shininess);
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

    private static unsafe void DrawFlatShading(ObjectModel objectModel, WriteableBitmap bitmap, Vector3 color, Vector3 eyePos)
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

    private static unsafe void RasterWithPhongShading(
    Vector2 vertex1, Vector2 vertex2, Vector2 vertex3,
    Vector3 worldPos1, Vector3 worldPos2, Vector3 worldPos3,
    Vector3 normal1, Vector3 normal2, Vector3 normal3,
    float z1, float z2, float z3, int height, int width,
    int* buffer, Vector3 eyePos, Vector3 lightPos,
    Vector3 lightColor, Vector3 objectColor,
    float ambientStrength, float specularStrength, int shininess)
    {
       
        if (vertex1.Y > vertex3.Y)
        {
            (vertex1, vertex3) = (vertex3, vertex1);
            (worldPos1, worldPos3) = (worldPos3, worldPos1);
            (normal1, normal3) = (normal3, normal1);
            (z1, z3) = (z3, z1);
        }

        if (vertex1.Y > vertex2.Y)
        {
            (vertex1, vertex2) = (vertex2, vertex1);
            (worldPos1, worldPos2) = (worldPos2, worldPos1);
            (normal1, normal2) = (normal2, normal1);
            (z1, z2) = (z2, z1);
        }

        if (vertex2.Y > vertex3.Y)
        {
            (vertex2, vertex3) = (vertex3, vertex2);
            (worldPos2, worldPos3) = (worldPos3, worldPos2);
            (normal2, normal3) = (normal3, normal2);
            (z2, z3) = (z3, z2);
        }

        
        float invDeltaY13 = 1.0f / (vertex3.Y - vertex1.Y);
        float invDeltaY12 = 1.0f / (vertex2.Y - vertex1.Y);
        float invDeltaY23 = 1.0f / (vertex3.Y - vertex2.Y);

        Vector2 edge13 = (vertex3 - vertex1) * invDeltaY13;
        Vector2 edge12 = (vertex2 - vertex1) * invDeltaY12;
        Vector2 edge23 = (vertex3 - vertex2) * invDeltaY23;

        Vector3 world13 = (worldPos3 - worldPos1) * invDeltaY13;
        Vector3 world12 = (worldPos2 - worldPos1) * invDeltaY12;
        Vector3 world23 = (worldPos3 - worldPos2) * invDeltaY23;

        Vector3 normal13 = (normal3 - normal1) * invDeltaY13;
        Vector3 normal12 = (normal2 - normal1) * invDeltaY12;
        Vector3 normal23 = (normal3 - normal2) * invDeltaY23;

        float z13 = (z3 - z1) * invDeltaY13;
        float z12 = (z2 - z1) * invDeltaY12;
        float z23 = (z3 - z2) * invDeltaY23;

    
        int startY = Math.Max(0, (int)MathF.Ceiling(vertex1.Y));
        int endY = Math.Min(height, (int)MathF.Ceiling(vertex3.Y));

        for (int y = startY; y < endY; y++)
        {
            float dy = y - vertex1.Y;


            Vector2 aPoint, bPoint;
            Vector3 aWorld, bWorld;
            Vector3 aNormal, bNormal;
            float aZ, bZ;

            if (y < vertex2.Y)
            {
         
                aPoint = vertex1 + edge13 * dy;
                bPoint = vertex1 + edge12 * dy;
                aWorld = worldPos1 + world13 * dy;
                bWorld = worldPos1 + world12 * dy;
                aNormal = normal1 + normal13 * dy;
                bNormal = normal1 + normal12 * dy;
                aZ = z1 + z13 * dy;
                bZ = z1 + z12 * dy;
            }
            else
            {
           
                dy = y - vertex2.Y;
                aPoint = vertex1 + edge13 * (y - vertex1.Y);
                bPoint = vertex2 + edge23 * dy;
                aWorld = worldPos1 + world13 * (y - vertex1.Y);
                bWorld = worldPos2 + world23 * dy;
                aNormal = normal1 + normal13 * (y - vertex1.Y);
                bNormal = normal2 + normal23 * dy;
                aZ = z1 + z13 * (y - vertex1.Y);
                bZ = z2 + z23 * dy;
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

            if (startX >= endX) continue;


            float dx = endX - startX;
            float tStep = 1.0f / dx;
            float t = 0;

            for (int x = startX; x < endX; x++, t += tStep)
            {
             
                Vector3 pixelWorld = aWorld + (bWorld - aWorld) * t;
                Vector3 pixelNormal = Vector3.Normalize(aNormal + (bNormal - aNormal) * t);
                float pixelZ = aZ + (bZ - aZ) * t;

          
                if (pixelZ >= _zBuffer[y, x]) continue;


                Vector3 lightDir = Vector3.Normalize(lightPos - pixelWorld);
                Vector3 viewDir = Vector3.Normalize(eyePos - pixelWorld);
                Vector3 reflectDir = Vector3.Reflect(-lightDir, pixelNormal);

              
                Vector3 ambient = ambientStrength * lightColor;

            
                float diff = MathF.Max(Vector3.Dot(pixelNormal, lightDir), 0.0f);
                Vector3 diffuse = diff * lightColor;

              
                float spec = MathF.Pow(MathF.Max(Vector3.Dot(viewDir, reflectDir), 0.0f), shininess);
                Vector3 specular = specularStrength * spec * lightColor;

                
                Vector3 result = (ambient + diffuse + specular) * objectColor;
                result = Vector3.Clamp(result, Vector3.Zero, Vector3.One);

               
                int r = (int)(result.X * 255);
                int g = (int)(result.Y * 255);
                int b = (int)(result.Z * 255);
                int color = (255 << 24) | (r << 16) | (g << 8) | b;


                int index = y * width + x;
                buffer[index] = color;
                _zBuffer[y, x] = pixelZ;
            }
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