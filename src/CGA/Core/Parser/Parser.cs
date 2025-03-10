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
                    
                case "f":
                    ParseFace(line, model);
                    break;
            }
        }

        model.GlobalVertices = new Vector4[model.LocalVertices.Count];

        return model;
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

        //массив для хранения индексов вершин грани
        var faceVertices = new int[faceParts.Length - 1]; // parts[0] - это f, поэтому вычитаем 1

        for (int i = 1; i < faceParts.Length; i++) // с 1, так как parts[0] - это f
        {
            // деление вершины на компоненты (vertex/texture/normal)
            var vertexData = faceParts[i].Split('/');
            faceVertices[i - 1] = int.Parse(vertexData[0]);
        }

        model.Faces.Add(faceVertices);
    }
}