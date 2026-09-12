using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace IconGenerator
{
    class Program
    {
        static void Main()
        {
            var bitmap = new Bitmap(256, 256);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.Transparent);
                
                // Background circle
                using var brush = new SolidBrush(Color.FromArgb(0, 200, 255));
                g.FillEllipse(brush, 20, 20, 216, 216);
                
                // Inner glow
                using var glowBrush = new SolidBrush(Color.FromArgb(100, 0, 255, 255));
                g.FillEllipse(glowBrush, 40, 40, 176, 176);
                
                // Text
                using var font = new Font("Segoe UI", 100, FontStyle.Bold);
                using var textBrush = new SolidBrush(Color.FromArgb(20, 20, 40));
                var text = "A7";
                var textSize = g.MeasureString(text, font);
                var x = (256 - textSize.Width) / 2;
                var y = (256 - textSize.Height) / 2 - 10;
                g.DrawString(text, font, textBrush, x + 2, y + 2);
                
                using var textBrush2 = new SolidBrush(Color.White);
                g.DrawString(text, font, textBrush2, x, y);
            }

            // Save as ICO with multiple sizes
            using var fs = new FileStream("ashtron.ico", FileMode.Create);
            var encoder = new IconEncoder();
            encoder.Save(bitmap, fs);
        }
    }

    public class IconEncoder
    {
        public void Save(Bitmap bitmap, Stream stream)
        {
            using var ms = new MemoryStream();
            bitmap.Save(ms, ImageFormat.Png);
            
            // Write ICO header
            using var writer = new BinaryWriter(stream);
            writer.Write((short)0);           // Reserved
            writer.Write((short)1);           // Type: ICO
            writer.Write((short)1);           // Number of images
            
            // Image directory entry
            writer.Write((byte)256);          // Width
            writer.Write((byte)256);          // Height
            writer.Write((byte)0);            // Colors
            writer.Write((byte)0);            // Reserved
            writer.Write((short)0);           // Color planes
            writer.Write((short)32);          // Bits per pixel
            writer.Write((int)ms.Length);     // Size of image data
            writer.Write((int)22);            // Offset of image data
            
            // Write PNG data
            ms.Position = 0;
            ms.CopyTo(stream);
        }
    }
}