using System.Globalization;
using System.IO;
using System.Numerics;
using CGA.Core.Entities;

namespace CGA.Core.Parsers;

public static class ObjFileParser
{
    public static ObjectModel LoadFromFile(in string path)
    {
        var model = new ObjectModel();
        var lines = File.ReadAllLines(path);
        var currentMaterialName = String.Empty;

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("#"))
                continue;

            var lineStart = line.Substring(0, trimmedLine.IndexOf(' '));

            switch (lineStart)
            {
                case "mtllib":
                    ParseMtlFilename(trimmedLine, path, model);
                    break;

                case "usemtl":
                    currentMaterialName = ParseCurrentMaterialName(trimmedLine, model);
                    break;

                case "v":
                    ParseVertex(line, model);
                    break;

                case "vn":
                    ParseNormal(line, model);
                    break;

                case "vt":
                    ParseTexture(line, model);
                    break;

                case "f":
                    ParseFace(line, model, currentMaterialName);
                    break;
            }
        }

        if (model.Faces.Any(f => f.VertexNormal == Vector3.Zero))
        {
            model.CalcNormals(Matrix4x4.Identity);
        }

        model.GlobalVertices = new Vector4[model.LocalVertices.Count];
        model.ProjectionVertices = new Vector4[model.LocalVertices.Count];

        return model;
    }

    private static void ParseMtlFilename(in string line, in string basePath, ObjectModel model)
    {
        var fileLineParts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
    
        var directory = Path.GetDirectoryName(basePath);
        model.PathToMtlFile = Path.Combine(directory, fileLineParts[1]);
    }
    
    private static string ParseCurrentMaterialName(in string line, ObjectModel model)
    {
        var materialLineParts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        
        return materialLineParts[1];
    }
    
    private static void ParseNormal(in string line, ObjectModel model)
    {
        var normalParts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        var normal = new Vector3(
            float.Parse(normalParts[1], System.Globalization.CultureInfo.InvariantCulture),
            float.Parse(normalParts[2], System.Globalization.CultureInfo.InvariantCulture),
            float.Parse(normalParts[3], System.Globalization.CultureInfo.InvariantCulture)
        );

        model.Normals.Add(Vector3.Normalize(normal)); // Сохраняем нормализованную нормаль
    }

    private static void ParseVertex(in string line, ObjectModel model)
    {
        var vertexParts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        var vertex = new Vector4
        (
            //на моем пк по умолчанию формат 0,0 вместо 0.0[obj формат]
            //Поэтому использую System.Globalization.CultureInfo.InvariantCulture
            float.Parse(vertexParts[1], System.Globalization.CultureInfo.InvariantCulture),
            float.Parse(vertexParts[2], System.Globalization.CultureInfo.InvariantCulture),
            float.Parse(vertexParts[3], System.Globalization.CultureInfo.InvariantCulture),
            1
        );

        model.LocalVertices.Add(vertex);
    }

    private static void ParseFace(in string line, ObjectModel model, string materialName)
    {
        var faceParts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var faceVertices = new int[faceParts.Length - 1];
        var faceTextures = new int[faceParts.Length - 1];
        var faceNormals = new int[faceParts.Length - 1];

        var hasTextures = false;
        var hasNormals = false;

        for (int i = 1; i < faceParts.Length; i++)
        {
            var vertexData = faceParts[i].Split('/');

            // Индекс вершины (обязательно есть)
            faceVertices[i - 1] = int.Parse(vertexData[0]);

            // Индекс текстуры (может отсутствовать)
            if (vertexData.Length > 1 && !string.IsNullOrEmpty(vertexData[1]))
            {
                faceTextures[i - 1] = int.Parse(vertexData[1]);
                hasTextures = true;
            }
            
            // Индекс нормали (может отсутствовать)
            if (vertexData.Length > 2 && !string.IsNullOrEmpty(vertexData[2]))
            {
                faceNormals[i - 1] = int.Parse(vertexData[2]);
                hasNormals = true;
            }
        }

        var face = new Face(faceVertices, materialName);
        
        if (hasTextures)
        {
            face.TextureIndexes = faceTextures;
        }
        
        if (hasNormals)
        {
            face.NormalIndexes = faceNormals;
        }

        model.Faces.Add(face);
    }

    private static void ParseTexture(in string line, ObjectModel model)
    {
        var textureParts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        var u = float.Parse(textureParts[1], CultureInfo.InvariantCulture);
        var v = textureParts.Length >= 3 ? float.Parse(textureParts[2], CultureInfo.InvariantCulture) : 0;
        var w = textureParts.Length >= 4 ? float.Parse(textureParts[3], CultureInfo.InvariantCulture) : 0;
        
        model.TextureCoords.Add(new Vector3(u, v, w));
    }
}