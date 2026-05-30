namespace PdfForge.Models
{
    public class PdfFileItem
    {
        public string FilePath     { get; set; } = string.Empty;
        public string FileName     => System.IO.Path.GetFileName(FilePath);
        public int    PageCount    { get; set; }
        public int    DisplayIndex { get; set; }
        public string PageInfo     => PageCount > 0 ? $"{PageCount} pages" : "-";
    }
}
