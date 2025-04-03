using System.IO;
using System.Numerics;
using CGA.Core.Entities;

namespace CGA.Core.Parser;

public static class Parser
{
    public static ObjectModel LoadFromFile(in string path)
    {
        var model = new ObjectModel();
        var lines = File.ReadAllLines(path);

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("#"))
                continue;

            string lineStart = line.Substring(0, trimmedLine.IndexOf(' '));

            switch (lineStart)
            {
                case "v":
                    ParseVertex(line, model);
                    break;

                case "vn":
                    ParseNormal(line, model);
                    break;

                case "f":
                    ParseFace(line, model);
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

    private static void ParseNormal(in string line, in ObjectModel model)
    {
        var normalParts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        var normal = new Vector3(
            float.Parse(normalParts[1], System.Globalization.CultureInfo.InvariantCulture),
            float.Parse(normalParts[2], System.Globalization.CultureInfo.InvariantCulture),
            float.Parse(normalParts[3], System.Globalization.CultureInfo.InvariantCulture)
            
        );

        model.Normals.Add(Vector3.Normalize(normal)); // Сохраняем нормализованную нормаль
    }

    private static void ParseVertex(in string line, in ObjectModel model)
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
    
private static void ParseFace(in string line, in ObjectModel model)
{
    var faceParts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
    var faceVertices = new int[faceParts.Length - 1];
    var faceNormals = new int[faceParts.Length - 1]; // Для хранения индексов нормалей
    
    bool hasNormals = false;

    for (int i = 1; i < faceParts.Length; i++)
    {
        var vertexData = faceParts[i].Split('/');
        
        // Индекс вершины (обязательно есть)
        faceVertices[i - 1] = int.Parse(vertexData[0]);
        
        // Индекс нормали (может отсутствовать)
        if (vertexData.Length > 2 && !string.IsNullOrEmpty(vertexData[2]))
        {
            faceNormals[i - 1] = int.Parse(vertexData[2]);
            hasNormals = true;
        }
    }

    var face = new Face(faceVertices);
    
    // Если в грани указаны нормали, сохраняем их индексы
    if (hasNormals)
    {
        face.NormalIndexes = faceNormals;
    }
    
    model.Faces.Add(face);
}
}