namespace PdfKit.Models
{
    public class PdfMetadata
    {
        public string Title        { get; set; }
        public string Author       { get; set; }
        public string Subject      { get; set; }
        public string Keywords     { get; set; }
        public string Creator      { get; set; }
        // Read-only display fields
        public string Producer     { get; set; }
        public string CreationDate { get; set; }
    }
}
