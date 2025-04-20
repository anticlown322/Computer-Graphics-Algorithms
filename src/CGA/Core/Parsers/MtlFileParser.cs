using System.Globalization;
using System.IO;
using System.Numerics;
using CGA.Core.Entities;

namespace CGA.Core.Parsers;

public static class MtlFileParser
{
    public static Dictionary<string, Material> LoadFromFile(string filePath)
    {
        var pathToTextures = Path.GetDirectoryName(filePath) + Path.DirectorySeparatorChar;
        var namesAndMaterials = new Dictionary<string, Material>();
        Material? currentMaterial = null;

        foreach (var line in File.ReadLines(filePath))
        {
            var trimmedLine = line.Trim();

            if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("#"))
                continue;

            var tokens = trimmedLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            switch (tokens[0])
            {
                case "newmtl":
                    currentMaterial = ParseMaterialName(namesAndMaterials, tokens);
                    break;
                
                case "Ka":
                case "Kd":
                case "Ks":
                    ParseMaterialColor(currentMaterial, tokens);
                    break;
                
                case "Ns":
                    currentMaterial!.Shininess = float.Parse(tokens[1], CultureInfo.InvariantCulture);
                    break;
                
                case "d":
                    currentMaterial!.Transparency = 1f - float.Parse(tokens[1], CultureInfo.InvariantCulture);
                    break;
                
                case "Tr":
                    currentMaterial!.Transparency = float.Parse(tokens[1], CultureInfo.InvariantCulture);
                    break;
                
                case "Ni":
                    currentMaterial!.OpticalDensity = float.Parse(tokens[1], CultureInfo.InvariantCulture);
                    break;
                
                case "illum":
                    currentMaterial!.IlluminationModel = int.Parse(tokens[1], CultureInfo.InvariantCulture);
                    break;

                case "map_Kd":
                    currentMaterial.DiffuseMap = pathToTextures + tokens[1];
                    break;
                
                case "norm":
                case "map_Norm":
                    currentMaterial.NormalMap = pathToTextures + tokens[1];
                    break;
                
                case "map_bump":
                    ParseBumpMap(currentMaterial, tokens, pathToTextures);
                    break;
                
                case "map_mrao": //metallic + roughness + ambient occlusion
                    currentMaterial.MraoMap = pathToTextures + tokens[1];
                    break;
                
                case "map_ao":
                    currentMaterial.AmbientOcclusionMap = pathToTextures + tokens[1];
                    break;
                
                case "map_metallic":
                case "map_refl":
                    currentMaterial.ReflectionMap = pathToTextures + tokens[1];
                    break;
                
                case "map_roughness":
                case "map_ns":
                    currentMaterial.RoughnessMap = pathToTextures + tokens[1];
                    break;
                
                case "map_ke":
                    currentMaterial.EmissiveMap = pathToTextures + tokens[1];
                    break;
                
                case "map_specular":
                    currentMaterial.SpecularMap = pathToTextures + tokens[1];
                    break;
            }
        }

        return namesAndMaterials;
    }

    private static Material ParseMaterialName(
        Dictionary<string, Material> namesAndMaterials,
        in string[] tokens)
    {
        var material = new Material { Name = tokens[1] };
        namesAndMaterials[tokens[1]] = material;
        
        return material;
    }

    private static void ParseMaterialColor(Material material, in string[] tokens)
    {
        var color = new Vector3(
            float.Parse(tokens[1], CultureInfo.InvariantCulture),
            float.Parse(tokens[2], CultureInfo.InvariantCulture),
            float.Parse(tokens[3], CultureInfo.InvariantCulture));

        if (tokens[0] == "Ka")
            material.AmbientColor = color;
        if (tokens[0] == "Kd")
            material.DiffuseColor = color;
        if (tokens[0] == "Ks")
            material.SpecularColor = color;
    }

    private static void ParseBumpMap(
        Material material, 
        in string[] tokens,
        in string pathToTextures)
    {
        // check if there is a scaling param
        if (tokens.Length >= 4 && tokens[1].ToLowerInvariant() == "-bm")
        {
            material.BumpScale = float.Parse(tokens[2], CultureInfo.InvariantCulture);
            material.BumpMap = pathToTextures + Path.GetFileName(string.Join("", tokens[3..]));
        }
        else if (tokens.Length >= 2)
        {
            material.BumpMap = pathToTextures + Path.GetFileName(string.Join("", tokens[1..]));
        }
    }
}