using System.Drawing;
using System.Drawing.Drawing2D;

namespace DeskTolls.Services;

internal static class TrayIconFactory
{
    internal static Icon Create(bool active)
    {
        using var bitmap = new Bitmap(64, 64);
        using var graphics = Graphics.FromImage(bitmap);

        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        var background = active
            ? Color.FromArgb(217, 70, 52)
            : Color.FromArgb(15, 118, 110);
        using var brush = new SolidBrush(background);
        graphics.FillRoundedRectangle(brush, new Rectangle(3, 3, 58, 58), 12);

        using var font = new Font("Segoe UI", 31, FontStyle.Bold, GraphicsUnit.Pixel);
        using var textBrush = new SolidBrush(Color.White);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
        };
        graphics.DrawString("D", font, textBrush, new RectangleF(2, 1, 60, 60), format);

        var iconHandle = bitmap.GetHicon();
        try
        {
            using var temporaryIcon = Icon.FromHandle(iconHandle);
            return (Icon)temporaryIcon.Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(iconHandle);
        }
    }

    private static void FillRoundedRectangle(
        this Graphics graphics,
        Brush brush,
        Rectangle bounds,
        int radius)
    {
        var diameter = radius * 2;
        using var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        graphics.FillPath(brush, path);
    }
}
