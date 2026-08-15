using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

internal static class BugReportIcon
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);

    internal static Icon Create()
    {
        using var bitmap = new Bitmap(16, 16);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        using var legPen = new Pen(Color.FromArgb(45, 45, 45), 1.15F)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        graphics.DrawLine(legPen, 5.5F, 6F, 1.5F, 4.2F);
        graphics.DrawLine(legPen, 5F, 8F, 1F, 8F);
        graphics.DrawLine(legPen, 5.5F, 10F, 1.8F, 12F);
        graphics.DrawLine(legPen, 10.5F, 6F, 14.5F, 4.2F);
        graphics.DrawLine(legPen, 11F, 8F, 15F, 8F);
        graphics.DrawLine(legPen, 10.5F, 10F, 14.2F, 12F);
        graphics.DrawLine(legPen, 7F, 3F, 5.3F, 0.8F);
        graphics.DrawLine(legPen, 9F, 3F, 10.7F, 0.8F);

        using var wingBrush = new SolidBrush(Color.FromArgb(220, 235, 70, 70));
        graphics.FillEllipse(wingBrush, new RectangleF(3.8F, 4F, 5F, 7.5F));
        graphics.FillEllipse(wingBrush, new RectangleF(7.2F, 4F, 5F, 7.5F));

        using var bodyBrush = new SolidBrush(Color.FromArgb(185, 25, 35));
        using var outlinePen = new Pen(Color.FromArgb(60, 20, 20), 0.8F);
        graphics.FillEllipse(bodyBrush, new RectangleF(5F, 3.5F, 6F, 9F));
        graphics.DrawEllipse(outlinePen, new RectangleF(5F, 3.5F, 6F, 9F));
        graphics.FillEllipse(Brushes.Black, new RectangleF(6F, 1F, 4F, 4F));
        graphics.FillEllipse(Brushes.White, new RectangleF(6.9F, 2F, 0.8F, 0.8F));
        graphics.FillEllipse(Brushes.White, new RectangleF(8.3F, 2F, 0.8F, 0.8F));
        graphics.DrawLine(outlinePen, 8F, 4.2F, 8F, 12F);

        var handle = bitmap.GetHicon();
        try
        {
            using var icon = Icon.FromHandle(handle);
            return (Icon)icon.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }
}
