using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Media.TextFormatting;
using CGA.Core.Entities;
using CGA.Core.Shadings;
using CGA.Core.Utils;
using Vector = System.Numerics.Vector;

namespace CGA.Core.Renderers;

public static class TextureRenderer
{
    private static float[]? _zBuffer;

    public static void RenderModel(
        ObjectModel objectModel,
        WriteableBitmap bitmap,
        Vector3 eyePos,
        List<Entities.Material> materials,
        Dictionary<string, TextureMap> textureMaps)
    {
        _zBuffer = new float[bitmap.PixelHeight * bitmap.PixelWidth];
        ClearBitmap(bitmap, new Vector3(0, 0, 0));
        Draw(objectModel, bitmap, eyePos, materials, textureMaps);
    }

    private static unsafe void ClearBitmap(WriteableBitmap bitmap, Vector3 color)
    {
        var intColor = 255 << 24 | (int)(255 * color.X) << 16 | (int)(255 * color.Y) << 8 | (int)(255 * color.Z);

        bitmap.Lock();

        var pBackBuffer = (int*)bitmap.BackBuffer;
        for (var i = 0; i < bitmap.PixelWidth * bitmap.PixelHeight; i++)
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
        List<Entities.Material> materials,
        Dictionary<string, TextureMap> textureMaps)
    {
        var width = bitmap.PixelWidth;
        var height = bitmap.PixelHeight;

        bitmap.Lock();
        var buffer = (int*)bitmap.BackBuffer;

        var ambientCoeff = 0.12f;
        var ambientColor = Colors.Black;
        var specularCoeff = 0.5f;
        var specularColor = Colors.Black;
        var diffuseCoeff = 1.0f;
        var shininess = 32;

        var light1 = (eyePos + new Vector3(2, 2, 2), Color.FromScRgb(1.0f, 1.0f, 0.9f, 0.8f), 0.3f);
        // var light2 = (eyePos + new Vector3(-2, -1, -1), Color.FromScRgb(1.0f, 0.9f, 0.95f, 1.0f), 0.5f);
        // var light3 = (eyePos + new Vector3(0, 0, -3), Colors.White, 0.3f);

        var lights = new[] { light1 };

        Parallel.ForEach(objectModel.Faces, face =>
        {
            var count = face.VertexIndexes.Length;
            if (count < 2)
                return;

            var textures = GetTexturesForFace(materials, face.MaterialName, textureMaps);

            for (var i = 1; i < count - 1; i++)
            {
                var idx1 = face.VertexIndexes[0] - 1;
                var idx2 = face.VertexIndexes[i] - 1;
                var idx3 = face.VertexIndexes[i + 1] - 1;

                Vector3[] screenVertices =
                [
                    objectModel.ProjectionVertices[idx1].XYZ() ,
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
                    objectModel.TextureCoords[face.TextureIndexes[0] - 1] / objectModel.WValues[idx1],
                    objectModel.TextureCoords[face.TextureIndexes[i] - 1] / objectModel.WValues[idx2],
                    objectModel.TextureCoords[face.TextureIndexes[i + 1] - 1] / objectModel.WValues[idx3]
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
                    textures: textures,
                    lights: lights,
                    ambientCoeff: ambientCoeff,
                    ambientColor: ambientColor,
                    diffuseCoeff: diffuseCoeff,
                    specularCoeff: specularCoeff,
                    specularColor: specularColor,
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


    private static (TextureMap? diffuseTexture, TextureMap? normalTexture, TextureMap? specularTexture)
        GetTexturesForFace(List<Entities.Material> materials, string materialName,
            Dictionary<string, TextureMap> textureMaps)
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
        (TextureMap? diffuseTexture, TextureMap? normalTexture, TextureMap? specularTexture) textures,
        (Vector3 SourceOfLight, Color Color, float Intensity)[] lights,
        float ambientCoeff,
        Color ambientColor,
        float diffuseCoeff,
        float specularCoeff,
        Color specularColor,
        float shininess)
    {
        var v0 = screenVertices[0];
        var v1 = screenVertices[1];
        var v2 = screenVertices[2];

        var world0 = worldVertices[0];
        var world1 = worldVertices[1];
        var world2 = worldVertices[2];

        var n0 = normals[0];
        var n1 = normals[1];
        var n2 = normals[2];

        var uv0 = textureCoordinates[0];
        var uv1 = textureCoordinates[1];
        var uv2 = textureCoordinates[2];

        var modelWorldMatrix = Matrix4x4.Identity;
        int* bufferPtr = buffer;

        // boundings
        var xMin = (int)Math.Round(MathF.Min(v0.X, MathF.Min(v1.X, v2.X)));
        var yMin = (int)Math.Round(MathF.Min(v0.Y, MathF.Min(v1.Y, v2.Y)));
        var xMax = (int)Math.Round(MathF.Max(v0.X, MathF.Max(v1.X, v2.X)));
        var yMax = (int)Math.Round(MathF.Max(v0.Y, MathF.Max(v1.Y, v2.Y)));

        xMax = Math.Min(width - 1, xMax);
        yMax = Math.Min(height - 1, yMax);
        xMin = Math.Max(0, xMin);
        yMin = Math.Max(0, yMin);

        var denom = (v2.X - v0.X) * (v1.Y - v0.Y) - (v2.Y - v0.Y) * (v1.X - v0.X);
        if (Math.Abs(denom) < float.Epsilon)
            return;

        for (var y = yMin; y <= yMax; y++)
        {
            if (y < 0 || y >= height)
                return;

            for (var x = xMin; x <= xMax; x++)
            {
                if (x < 0 || x >= width)
                    continue;

                var pixel = new Vector2(x, y);

                var alpha = (pixel.X - v1.X) * (v2.Y - v1.Y) - (pixel.Y - v1.Y) * (v2.X - v1.X);
                var beta = (pixel.X - v2.X) * (v0.Y - v2.Y) - (pixel.Y - v2.Y) * (v0.X - v2.X);
                var gamma = (pixel.X - v0.X) * (v1.Y - v0.Y) - (pixel.Y - v0.Y) * (v1.X - v0.X);

                if (!(alpha >= 0) || !(beta >= 0) || !(gamma >= 0))
                    continue;

                alpha /= denom;
                beta /= denom;
                gamma /= denom;

                var depth = v0.Z * alpha + v1.Z * beta + v2.Z * gamma;
                depth = 1.0f / depth;
                
                //
                Vector3 uv = alpha * uv0 + beta * uv1 + gamma * uv2;
                uv /= uv.Z;

                //
                var u = Math.Clamp(uv.X, 0.0f, 1.0f);
                var v = 1.0f - uv.Y;
                
                v = Math.Clamp(v, 0.0f, 1.0f);

                var index = y * width + x;

                if (_zBuffer == null || !(depth > _zBuffer[index]))
                    continue;

                var diffuseSample = textures.diffuseTexture.GetColor(u, v);
                var normal = GetNormal(textures.normalTexture, u, v, n0, n1, n2, alpha, beta, gamma, modelWorldMatrix);
                (Color specularSample, float specularStrength) =
                    GetSpecular(textures.specularTexture, u, v, specularCoeff, specularColor);

                var position = world0 * alpha + world1 * beta + world2 * gamma;

                var color = ShadePhong(
                    normal, position, lights, ambientCoeff, ambientColor,
                    diffuseCoeff, diffuseSample,
                    specularStrength, specularSample,
                    shininess, eyePos);

                _zBuffer[index] = depth;
                bufferPtr[index] = color;
            }
        }
    }

    private static Vector3 GetNormal(
        TextureMap? normalMap,
        float u, float v,
        Vector3 n0, Vector3 n1, Vector3 n2,
        float alpha, float beta, float gamma,
        Matrix4x4 modelWorldMatrix)
    {
        if (normalMap == null)
        {
            var normal = n0 * alpha + n1 * beta + n2 * gamma;
            return normal;
        }

        var normalColor = normalMap.GetColor(u, v);
        var sampledNormal = new Vector3(
            normalColor.ScR * 2 - 1,
            normalColor.ScG * 2 - 1,
            normalColor.ScB * 2 - 1
        );

        sampledNormal = Vector3.TransformNormal(sampledNormal, modelWorldMatrix);
        return Vector3.Normalize(sampledNormal);
    }

    private static (Color specularColor, float specularCoeff) GetSpecular(
        TextureMap? specularMap,
        float u, float v,
        float baseSpecularCoeff,
        Color baseSpecularColor)
    {
        var specularStrength = 1.0f;
        var specularSample = baseSpecularColor;

        if (specularMap != null)
        {
            specularSample = specularMap.GetColor(u, v);
            specularStrength = (specularSample.ScR + specularSample.ScG + specularSample.ScB) / 3.0f;
        }

        var specularCoeff = baseSpecularCoeff * specularStrength;
        return (specularSample, specularCoeff);
    }

    private static int ShadePhong(
        Vector3 normal,
        Vector3 center,
        (Vector3 SourceOfLight, Color Color, float Intensity)[] lights,
        float ambientCoeff,
        Color ambientColor,
        float diffuseCoeff,
        Color diffuseSample,
        float specularCoeff,
        Color specularColor,
        float shininess,
        Vector3 eyePos)
    {
        normal = Vector3.Normalize(normal);
        var viewDir = Vector3.Normalize(eyePos - center);

        var rColor = ambientColor.ScR * ambientCoeff;
        var gColor = ambientColor.ScG * ambientCoeff;
        var bColor = ambientColor.ScB * ambientCoeff;

        foreach (var light in lights)
        {
            var lightDirection = Vector3.Normalize(light.SourceOfLight - center);
            var dot = Vector3.Dot(normal, lightDirection);

            if (dot <= 0)
                continue; 

            var intensity = dot * light.Intensity;

            rColor += intensity * light.Color.ScR * diffuseCoeff * diffuseSample.ScR;
            gColor += intensity * light.Color.ScG * diffuseCoeff * diffuseSample.ScG;
            bColor += intensity * light.Color.ScB * diffuseCoeff * diffuseSample.ScB;

            var specular = CalcSpecular(normal, lightDirection, viewDir, shininess);
            rColor += specularCoeff * specular * light.Color.ScR * specularColor.ScR;
            gColor += specularCoeff * specular * light.Color.ScG * specularColor.ScG;
            bColor += specularCoeff * specular * light.Color.ScB * specularColor.ScB;
        }

        // Gamma correction
        rColor = MathF.Pow(rColor, 1 / 2.2f);
        gColor = MathF.Pow(gColor, 1 / 2.2f);
        bColor = MathF.Pow(bColor, 1 / 2.2f);

        return
            (int)MathF.Min(255.0f, MathF.Round(bColor * 255)) |
            ((int)MathF.Min(255.0f, MathF.Round(gColor * 255)) << 8) |
            ((int)MathF.Min(255.0f, MathF.Round(rColor * 255)) << 16) |
            (ambientColor.A << 24);
    }

    private static float CalcSpecular(Vector3 normal, Vector3 lightDirection, Vector3 viewDir, float shininess)
    {
        var reflectedLight = Vector3.Reflect(-lightDirection, normal);
        var specFactor = MathF.Max(Vector3.Dot(reflectedLight, viewDir), 0.0f);
        return MathF.Pow(specFactor, shininess);
    }
}