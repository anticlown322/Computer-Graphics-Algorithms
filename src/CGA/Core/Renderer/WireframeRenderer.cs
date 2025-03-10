using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using CGA.Core.Entities;

namespace CGA.Core.Renderer;

public static class WireframeRenderer
{
    public static void RenderModel(ObjectModel objectModel, WriteableBitmap bitmap, float zNear, float zFar, Vector3 color)
    {
        if (bitmap is null)
            throw new ArgumentNullException(nameof(bitmap));

        ClearBitmap(bitmap, new(0, 0, 0));
        Draw(objectModel, bitmap, zNear, zFar, color);
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

    private static void Draw(ObjectModel objectModel, WriteableBitmap bitmap, float zNear, float zFar, Vector3 color)
    {
        bitmap.Lock();

        foreach (var face in objectModel.Faces)
        {
            int count = face.Length;
            if (count < 2)
                continue;

            for (int i = 0; i < count; i++)
            {
                int index1 = face[i] - 1;
                int index2 = face[(i + 1) % count] - 1;

                if (!(index1 >= 0 && index1 < objectModel.GlobalVertices.Length &&
                      index2 >= 0 && index2 < objectModel.GlobalVertices.Length))
                    continue;

                int x0 = (int)Math.Round(objectModel.GlobalVertices[index1].X);
                int y0 = (int)Math.Round(objectModel.GlobalVertices[index1].Y);
                float z0 = objectModel.GlobalVertices[index1].Z;

                int x1 = (int)Math.Round(objectModel.GlobalVertices[index2].X);
                int y1 = (int)Math.Round(objectModel.GlobalVertices[index2].Y);
                float z1 = objectModel.GlobalVertices[index2].Z;

                //is both vertices outside screen
                if ((x0 >= bitmap.PixelWidth && x1 >= bitmap.PixelWidth)
                    || (x0 < 0 && x1 < 0)
                    || (y0 >= bitmap.PixelHeight && y1 >= bitmap.PixelHeight)
                    || (y0 < 0 && y1 < 0))
                    continue;

                //is outside camera vision
                if (z0 < zNear
                    || z1 < zNear
                    || z0 > zFar
                    || z1 > zFar)
                    continue;

                DrawBresenhamLine(bitmap, new(x0, y0), new(x1, y1), color, bitmap.PixelWidth, bitmap.PixelHeight);
            }
        }

        try
        {
            bitmap.AddDirtyRect(new(0, 0, bitmap.PixelWidth, bitmap.PixelHeight));
        }
        finally
        {
            bitmap.Unlock();
        }
    }

    private static unsafe void DrawBresenhamLine(
        WriteableBitmap bitmap, 
        Vector2 a, Vector2 b, Vector3 color, 
        int width, int height)
    {
        int x1 = (int)Math.Round(a.X, MidpointRounding.AwayFromZero);
        int y1 = (int)Math.Round(a.Y, MidpointRounding.AwayFromZero);
        int x2 = (int)Math.Round(b.X, MidpointRounding.AwayFromZero);
        int y2 = (int)Math.Round(b.Y, MidpointRounding.AwayFromZero);

        int dx = x2 - x1;
        int dy = y2 - y1;

        int w = int.Abs(dx);
        int h = int.Abs(dy);
        int l = int.Max(w, h);

        int m00 = int.Sign(dx);
        int m01 = 0;
        int m10 = 0;
        int m11 = int.Sign(dy);
        if (w < h)
        {
            (m00, m01) = (m01, m00);
            (m10, m11) = (m11, m10);
        }

        int y = 0;
        int e = 0;
        int eDec = 2 * l;
        int eInc = 2 * int.Min(w, h);

        for (int x = 0; x <= l; x++)
        {
            int xt = x1 + m00 * x + m01 * y;
            int yt = y1 + m10 * x + m11 * y;

            //if current point is inside screen
            if (xt >= 0 && xt < width && yt >= 0 && yt < height)
            {
                //Set bitmap pixel
                IntPtr address = bitmap.BackBuffer + yt * bitmap.BackBufferStride + xt * bitmap.Format.BitsPerPixel / 8;
                *(int*)address = 255 << 24 | (int)(255 * color.X) << 16 | (int)(255 * color.Y) << 8 | (int)(255 * color.Z);
            }

            if ((e += eInc) > 1)
            {
                e -= eDec;
                y++;
            }
        }
    }
}