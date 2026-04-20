using System.IO;

namespace PdfKit.Models
{
    public class PdfFileItem
    {
        public string FilePath     { get; set; }
        public string FileName     => Path.GetFileName(FilePath);
        public int    PageCount    { get; set; }
        public int    DisplayIndex { get; set; }
        public string PageInfo     => PageCount > 0 ? $"{PageCount} pages" : "-";
    }
}
