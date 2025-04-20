using System.Numerics;
using System.Windows;
using System.Windows.Media.Imaging;
using CGA.Core.Entities;
using CGA.Core.Shadings;
using CGA.Core.Utils;

namespace CGA.Core.Renderers;

public static class TextureRenderer
{
    private static float[]? _zBuffer;

    public static void RenderModel(
        ObjectModel objectModel,
        WriteableBitmap bitmap,
        Vector3 eyePos,
        List<Material> materials,
        Dictionary<string, TextureMap> textureMaps)
    {
        _zBuffer = new float[bitmap.PixelHeight * bitmap.PixelWidth];
        ClearBitmap(bitmap, new Vector3(0, 0, 0));
        Draw(objectModel, bitmap, eyePos, materials, textureMaps);
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

    private static unsafe void Draw(
        ObjectModel objectModel,
        WriteableBitmap bitmap,
        Vector3 eyePos,
        List<Material> materials,
        Dictionary<string, TextureMap> textureMaps)
    {
        var width = bitmap.PixelWidth;
        var height = bitmap.PixelHeight;

        bitmap.Lock();
        var buffer = (int*)bitmap.BackBuffer;

        Parallel.ForEach(objectModel.Faces, face =>
        {
            var count = face.VertexIndexes.Length;
            if (count < 2)
                return;

            if(IsBackFace(face, objectModel, eyePos))
                return;

            var textures = GetTexturesForFace(materials, face.MaterialName, textureMaps);
            
            for (var i = 1; i < count - 1; i++)
            {
                var idx1 = face.VertexIndexes[0] - 1;
                var idx2 = face.VertexIndexes[i] - 1;
                var idx3 = face.VertexIndexes[i + 1] - 1;
                
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
                
                Vector3[] textureCoords =
                [
                    objectModel.TextureCoords[face.TextureIndexes[0] - 1],
                    objectModel.TextureCoords[face.TextureIndexes[i] - 1],
                    objectModel.TextureCoords[face.TextureIndexes[i + 1] - 1]
                ];
                
                RasterTriangleWithTexture(
                    screenVertices: screenVertices,
                    worldVertices: worldVertices,
                    normals: normals,
                    textureCoordinates: textureCoords,
                    height: height,
                    width: width,
                    buffer: buffer,
                    eyePos: eyePos,
                    textures: textures);
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
    
    private static bool IsBackFace(Face face, ObjectModel objectModel, Vector3 eyePos)
    {
        var idx = face.VertexIndexes[0] - 1;
        var vertex = objectModel.GlobalVertices[idx];
        var vertexPos = vertex.XYZ();
        var viewDirection = eyePos - vertexPos;

        return Vector3.Dot(face.VertexNormal, viewDirection) < 0;
    }

    private static (TextureMap? diffuseTexture, TextureMap? normalTexture, TextureMap? specularTexture) 
        GetTexturesForFace(List<Material> materials, string materialName, Dictionary<string, TextureMap> textureMaps)
    {
        var material = materials.FirstOrDefault(a => a.Name == materialName);
        TextureMap? diffuseTexture = null;
        TextureMap? normalTexture = null;
        TextureMap? specularTexture = null;

        if (material != null && textureMaps.Count > 0)
        {
            textureMaps.TryGetValue(material.DiffuseMap, out diffuseTexture);
            textureMaps.TryGetValue(material.NormalMap, out normalTexture);
            textureMaps.TryGetValue(material.SpecularMap, out specularTexture);
        }
        
        return (diffuseTexture, normalTexture, specularTexture);
    }
    
    private static unsafe void RasterTriangleWithTexture(        
        Vector3[] screenVertices,
        Vector3[] worldVertices,
        Vector3[] normals,
        Vector3[] textureCoordinates,
        int height, 
        int width,
        int* buffer, 
        Vector3 eyePos, 
        (TextureMap? diffuseTexture, TextureMap? normalTexture, TextureMap? specularTexture) textures)
    {
        
    }
}