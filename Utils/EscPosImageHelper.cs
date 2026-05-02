using System;
using System.Collections.Generic;
using System.IO;
using Windows.Graphics.Imaging;
using Windows.Storage;
using System.Threading.Tasks;
using System.Runtime.InteropServices.WindowsRuntime;

namespace DChemist.Utils
{
    public static class EscPosImageHelper
    {
        public static async Task<byte[]> GetLogoEscPosBytesAsync()
        {
            try
            {
                // Load the logo file from the installation directory
                string logoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "store-logo.png");
                if (!File.Exists(logoPath)) return Array.Empty<byte>();

                using (var fileStream = File.OpenRead(logoPath))
                using (var stream = fileStream.AsRandomAccessStream())
                {
                    var decoder = await BitmapDecoder.CreateAsync(stream);
                    var softwareBitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

                    // For ESC/POS, we need to convert this to monochrome bits
                    // We'll target a width of 384 pixels (standard 80mm printer)
                    int width = (int)decoder.PixelWidth;
                    int height = (int)decoder.PixelHeight;

                    // Resize if too wide (160 pixels is a small, professional size for 80mm printers)
                    if (width > 160)
                    {
                        double scale = 160.0 / width;
                        width = 160;
                        height = (int)(height * scale);
                    }
                    byte[] pixels = new byte[width * height * 4];
                    var pixelData = await decoder.GetPixelDataAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, new BitmapTransform { ScaledWidth = (uint)width, ScaledHeight = (uint)height }, ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.DoNotColorManage);
                    pixels = pixelData.DetachPixelData();
                    int widthBytes = (width + 7) / 8;
                    var result = new List<byte>();
                    
                    // --- Center Image ---
                    result.Add(27); result.Add(97); result.Add(1); // ESC a 1
                    
                    result.Add(29); // GS
                    result.Add(118); // v
                    result.Add(48); // 0
                    result.Add(0); // m = 0
                    
                    result.Add((byte)(widthBytes % 256)); // xL
                    result.Add((byte)(widthBytes / 256)); // xH
                    result.Add((byte)(height % 256)); // yL
                    result.Add((byte)(height / 256)); // yH

                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < widthBytes; x++)
                        {
                            byte b = 0;
                            for (int bit = 0; bit < 8; bit++)
                            {
                                int px = x * 8 + bit;
                                if (px < width)
                                {
                                    int idx = (y * width + px) * 4;
                                    byte blue = pixels[idx];
                                    byte green = pixels[idx + 1];
                                    byte red = pixels[idx + 2];
                                    byte alpha = pixels[idx + 3];

                                    // Luma threshold (0.299R + 0.587G + 0.114B)
                                    double luma = (0.299 * red + 0.587 * green + 0.114 * blue);
                                    
                                    // If alpha is low, treat as white
                                    if (alpha < 128 || luma > 128)
                                    {
                                        // White (0 in ESC/POS bit image)
                                    }
                                    else
                                    {
                                        // Black (1 in ESC/POS bit image)
                                        b |= (byte)(128 >> bit);
                                    }
                                }
                            }
                            result.Add(b);
                        }
                    }

                    // Reset alignment to Left and add a small gap
                    result.Add(27); result.Add(97); result.Add(0); // ESC a 0
                    result.Add(10); // LF

                    return result.ToArray();
                }
            }
            catch
            {
                return Array.Empty<byte>();
            }
        }
    }
}
