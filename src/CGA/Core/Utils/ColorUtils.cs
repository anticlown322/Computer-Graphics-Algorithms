using System.Numerics;

namespace CGA.Core.Utils;

public static class ColorUtils
{
    public static int ColorToInt(Vector3 color, int alpha = 255, double strength = 1)
    {
        int r = (int)Math.Round(strength * color.X * 255);
        int g = (int)Math.Round(strength * color.Y * 255);
        int b = (int)Math.Round(strength * color.Z * 255);
        r = Math.Clamp(r, 0, 255);
        g = Math.Clamp(g, 0, 255);
        b = Math.Clamp(b, 0, 255);
        int shadedColorBgra = (alpha << 24) | (r << 16) | (g << 8) | b;
        
        return shadedColorBgra;
    }
}