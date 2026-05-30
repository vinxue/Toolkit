namespace PdfForge.Models
{
    public class PageItem
    {
        public string SourceFile      { get; set; } = string.Empty;
        public int    SourcePageIndex { get; set; }
        public int    DisplayNumber   { get; set; }
        public string PageLabel       { get; set; } = string.Empty;
        public string SizeInfo        { get; set; } = string.Empty;
        public string SourceName      => System.IO.Path.GetFileName(SourceFile);
    }
}
