using System.ComponentModel;
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
        List<Entities.Material> materials,
        Dictionary<string, TextureMap> textureMaps)
    {
        var width = bitmap.PixelWidth;
        var height = bitmap.PixelHeight;

        bitmap.Lock();
        var buffer = (int*)bitmap.BackBuffer;

        float ambientCoeff = 0.12f;
        Color ambientColor = Colors.White;
        float diffuseCoeff = 1.0f;
        Color diffuseColor = Colors.White;
        float specularCoeff = 0.5f;
        Color specularColor = Colors.White;
        float shininess = 32;
        Color backgroundColor = Colors.Purple;

        var lights = new[]
        {

        new
        {
        SourceOfLight = eyePos + new Vector3(2, 2, 2),
        Color = Color.FromScRgb(1.0f, 1.0f, 0.9f, 0.8f), 
        Intensity = 0.3f 
        },

        new
        {
        SourceOfLight = eyePos + new Vector3(-2, -1, -1),
        Color = Color.FromScRgb(1.0f, 0.9f, 0.95f, 1.0f),
        Intensity = 0.5f
        },

        new
        {
        SourceOfLight = eyePos + new Vector3(0, 0, -3),
        Color = Colors.White,
        Intensity = 0.3f
        }
};

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
                    textures: textures,
                    lights: lights,
                    ambientCoeff: ambientCoeff,
                    ambientColor: ambientColor,
                    diffuseCoeff: diffuseCoeff,
                    diffuseColor: diffuseColor,
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
        GetTexturesForFace(List<Entities.Material> materials, string materialName, Dictionary<string, TextureMap> textureMaps)
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
        dynamic[] lights,
        float ambientCoeff,
        Color ambientColor,
        float diffuseCoeff,
        Color diffuseColor,
        float specularCoeff,
        Color specularColor,
        float shininess)
    {
        Vector4 v0 = new Vector4(screenVertices[0], 1);
        Vector4 v1 = new Vector4(screenVertices[1], 1);
        Vector4 v2 = new Vector4(screenVertices[2], 1);

        Vector4 n0 = new Vector4(normals[0], 0);
        Vector4 n1 = new Vector4(normals[1], 0);
        Vector4 n2 = new Vector4(normals[2], 0);

        Vector3 uv0 = textureCoordinates[0];
        Vector3 uv1 = textureCoordinates[1];
        Vector3 uv2 = textureCoordinates[2];

        Vector4 world0 = new Vector4(worldVertices[0], 1);
        Vector4 world1 = new Vector4(worldVertices[1], 1);
        Vector4 world2 = new Vector4(worldVertices[2], 1);

        var modelWorldMatrix = Matrix4x4.Identity;

        uv0 *= v0.W;
        uv1 *= v1.W;
        uv2 *= v2.W;

        int* bufferPtr = buffer;

        var xMin = (int)Math.Round(MathF.Min(v0.X, MathF.Min(v1.X, v2.X)));
        var yMin = (int)Math.Round(MathF.Min(v0.Y, MathF.Min(v1.Y, v2.Y)));
        var xMax = (int)Math.Round(MathF.Max(v0.X, MathF.Max(v1.X, v2.X)));
        var yMax = (int)Math.Round(MathF.Max(v0.Y, MathF.Max(v1.Y, v2.Y)));

        xMax = Math.Min(width - 1, xMax);
        yMax = Math.Min(height - 1, yMax);
        xMin = Math.Max(0, xMin);
        yMin = Math.Max(0, yMin);

        float denom = (v2.X - v0.X) * (v1.Y - v0.Y) - (v2.Y - v0.Y) * (v1.X - v0.X);
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

                Vector4 pixel = new Vector4(x, y, 0, 1);

                float alpha = (pixel.X - v1.X) * (v2.Y - v1.Y) - (pixel.Y - v1.Y) * (v2.X - v1.X);
                float beta = (pixel.X - v2.X) * (v0.Y - v2.Y) - (pixel.Y - v2.Y) * (v0.X - v2.X);
                float gamma = (pixel.X - v0.X) * (v1.Y - v0.Y) - (pixel.Y - v0.Y) * (v1.X - v0.X);

                if (alpha >= 0 && beta >= 0 && gamma >= 0)
                {
                    var w0_Old = v0.W;
                    var w1_Old = v1.W;
                    var w2_Old = v2.W;

                    alpha /= denom;
                    beta /= denom;
                    gamma /= denom;

                    float depth = v0.Z * alpha + v1.Z * beta + v2.Z * gamma;
                    float depthW = v0.W * alpha + v1.W * beta + v2.W * gamma;

                    var index = y * width + x;

                    depth = 1.0f / depth;

                    if (_zBuffer != null && depth > _zBuffer[index])
                    {
                        float div = alpha / w0_Old + beta / w1_Old + gamma / w2_Old;

                        float u = ((alpha * uv0.X) + (beta * uv1.X) + (gamma * uv2.X)) / depthW;
                        float v = ((alpha * uv0.Y) + (beta * uv1.Y) + (gamma * uv2.Y)) / depthW;

                        v = 1.0f - v;
                        u = Math.Clamp(u, 0.0f, 1.0f);
                        v = Math.Clamp(v, 0.0f, 1.0f);

                        Color diffuseSample = SampleDiffuseColor(textures.diffuseTexture, u, v);
                        Vector3 normal = SampleNormal(textures.normalTexture, u, v, n0, n1, n2, alpha, beta, gamma, modelWorldMatrix);
                        (Color specularSample, float specularStrength) = SampleSpecular(textures.specularTexture, u, v, specularCoeff, specularColor);

                        Vector4 position4 = (world0 * alpha + world1 * beta + world2 * gamma);
                        Vector3 position = new Vector3(position4.X, position4.Y, position4.Z);

                        int phongColor = ApplyPhongShading(
                            normal, position, lights, ambientCoeff, ambientColor,
                            diffuseCoeff, diffuseColor, diffuseSample,
                            specularStrength, specularSample,
                            shininess, eyePos);

                        _zBuffer[index] = depth;
                        bufferPtr[index] = phongColor;
                    }
                }
            }
        }
    }

    private static Color SampleDiffuseColor(TextureMap? diffuseTexture, float u, float v)
    {
        return diffuseTexture?.GetColor(u, v) ?? Colors.Fuchsia;
    }

    private static Vector3 SampleNormal(
        TextureMap? normalMap,
        float u, float v,
        Vector4 n0, Vector4 n1, Vector4 n2,
        float alpha, float beta, float gamma,
        Matrix4x4 modelWorldMatrix)
    {
        if (normalMap == null)
        {
            Vector4 normal4 = (n0 * alpha + n1 * beta + n2 * gamma);
            return new Vector3(normal4.X, normal4.Y, normal4.Z);
        }

        Color normalColor = normalMap.GetColor(u, v);
        Vector3 sampledNormal = new Vector3(
            normalColor.ScR * 2 - 1,
            normalColor.ScG * 2 - 1,
            normalColor.ScB * 2 - 1
        );

        sampledNormal = Vector3.TransformNormal(sampledNormal, modelWorldMatrix);
        return Vector3.Normalize(sampledNormal);
    }

    private static (Color specularColor, float specularCoeff) SampleSpecular(
        TextureMap? specularMap,
        float u, float v,
        float baseSpecularCoeff,
        Color baseSpecularColor)
    {
        float specularStrength = 1.0f;
        Color specularSample = baseSpecularColor;

        if (specularMap != null)
        {
            specularSample = specularMap.GetColor(u, v);
            specularStrength = (specularSample.ScR + specularSample.ScG + specularSample.ScB) / 3.0f;
        }

        float specularCoeff = baseSpecularCoeff * specularStrength;
        return (specularSample, specularCoeff);
    }

    private static int ApplyPhongShading(
    Vector3 normal,
    Vector3 center,
    dynamic[] lights,
    float ambientCoeff,
    Color ambientColor,
    float diffuseCoeff,
    Color diffuseColor,
    Color diffuseSample,
    float specularCoeff,
    Color specularColor,
    float shininess,
    Vector3 eyePos)
    {
        normal = Vector3.Normalize(normal);
        Vector3 viewDir = Vector3.Normalize(eyePos - center);

        float rColor = ambientColor.ScR * ambientCoeff;
        float gColor = ambientColor.ScG * ambientCoeff;
        float bColor = ambientColor.ScB * ambientCoeff;

        foreach (var light in lights)
        {
            Vector3 lightDirection = Vector3.Normalize(light.SourceOfLight - center);
            float dot = Vector3.Dot(normal, lightDirection);

            // Двустороннее освещение - учитываем обе стороны
            float intensity = MathF.Abs(dot) * light.Intensity;

            rColor += intensity * light.Color.ScR * diffuseCoeff * diffuseSample.ScR;
            gColor += intensity * light.Color.ScG * diffuseCoeff * diffuseSample.ScG;
            bColor += intensity * light.Color.ScB * diffuseCoeff * diffuseSample.ScB;

            // Блики только для лицевой стороны
            if (dot > 0)
            {
                float specular = CalcSpecular(normal, lightDirection, viewDir, shininess);
                rColor += (specularCoeff * specular) * light.Color.ScR * specularColor.ScR;
                gColor += (specularCoeff * specular) * light.Color.ScG * specularColor.ScG;
                bColor += (specularCoeff * specular) * light.Color.ScB * specularColor.ScB;
            }
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
        Vector3 reflectedLight = Vector3.Reflect(-lightDirection, normal);
        float specFactor = MathF.Max(Vector3.Dot(reflectedLight, viewDir), 0.0f);
        return MathF.Pow(specFactor, shininess);
    }
}