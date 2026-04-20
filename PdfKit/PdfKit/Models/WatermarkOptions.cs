using System.Collections.Generic;

namespace PdfKit.Models
{
    public enum WatermarkPosition
    {
        TopLeft,    TopCenter,    TopRight,
        MiddleLeft, Center,       MiddleRight,
        BottomLeft, BottomCenter, BottomRight
    }

    public class WatermarkOptions
    {
        public string           Text        { get; set; }
        public double           FontSize    { get; set; } = 60;
        public bool             FontBold    { get; set; } = false;
        public byte             ColorR      { get; set; } = 160;
        public byte             ColorG      { get; set; } = 160;
        public byte             ColorB      { get; set; } = 160;
        public double           Opacity     { get; set; } = 0.30;   // 0.0 – 1.0
        public WatermarkPosition Position   { get; set; } = WatermarkPosition.Center;
        public double           Rotation    { get; set; } = -45;
        public List<int>        PageNumbers { get; set; }            // null = all pages (1-based)
    }
}
