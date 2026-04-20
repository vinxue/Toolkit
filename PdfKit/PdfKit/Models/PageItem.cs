using System.IO;

namespace PdfKit.Models
{
    public class PageItem
    {
        public string SourceFile      { get; set; }
        public int    SourcePageIndex { get; set; }  // 0-based index inside SourceFile
        public int    DisplayNumber   { get; set; }  // 1-based position in the working list
        public string PageLabel       { get; set; }  // e.g. "Page 3"
        public string SizeInfo        { get; set; }  // e.g. "595 x 842 pt"
        public string SourceName      => Path.GetFileName(SourceFile);
    }
}
