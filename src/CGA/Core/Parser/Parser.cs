using System.IO;
using System.Numerics;
using CGA.Core.Entities;

namespace CGA.Core.Parser;

public static class Parser
{
    public static ObjectModel LoadFromFile(string path)
    {
        var model = new ObjectModel();
        var lines = File.ReadAllLines(path);

        foreach (var line in lines)
        {
            if (line.StartsWith("v "))
            {
                var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                
                var vertex = new Vector4
                (
                    //на моем пк по умолчанию формат 0,0 вместо 0.0[obj формат]
                    //Поэтому использую System.Globalization.CultureInfo.InvariantCulture
                    float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture),
                    float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture),
                    float.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture),
                    1
                ); 
                
                model.LocalVertices.Add(vertex);
            }
            else if (line.StartsWith("f "))
            {
                var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                
                var face = new int[]
                {
                    int.Parse(parts[1].Split('/')[0]),
                    int.Parse(parts[2].Split('/')[0]),
                    int.Parse(parts[3].Split('/')[0])
                };
                
                model.Faces.Add(face);
            }
        }
        
        model.GlobalVertices = new Vector4[model.LocalVertices.Count];

        return model;
    }
}