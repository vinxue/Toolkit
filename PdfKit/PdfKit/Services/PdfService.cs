using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfSharp.Pdf.Security;
using PdfKit.Models;

namespace PdfKit.Services
{
    public class PdfService
    {
        /// <summary>
        /// Returns the total page count of a PDF file.
        /// </summary>
        public int GetPageCount(string filePath)
        {
            using (var doc = PdfReader.Open(filePath, PdfDocumentOpenMode.Import))
                return doc.PageCount;
        }

        /// <summary>
        /// Extracts the specified pages from sourcePath and saves to outputPath.
        /// pageNumbers is 1-based.
        /// </summary>
        public void ExtractPages(string sourcePath, IEnumerable<int> pageNumbers, string outputPath)
        {
            var pages = pageNumbers.ToList();
            using (var input = PdfReader.Open(sourcePath, PdfDocumentOpenMode.Import))
            {
                var output = new PdfDocument();
                foreach (int pageNum in pages)
                {
                    if (pageNum < 1 || pageNum > input.PageCount)
                        throw new ArgumentOutOfRangeException(
                            nameof(pageNumbers), $"Page {pageNum} is out of range (document has {input.PageCount} pages).");
                    output.AddPage(input.Pages[pageNum - 1]);
                }
                output.Save(outputPath);
            }
        }

        /// <summary>
        /// Merges all PDFs in sourcePaths (in order) into a single outputPath.
        /// </summary>
        public void MergePdfs(IEnumerable<string> sourcePaths, string outputPath)
        {
            var output = new PdfDocument();
            foreach (string path in sourcePaths)
            {
                using (var input = PdfReader.Open(path, PdfDocumentOpenMode.Import))
                {
                    for (int i = 0; i < input.PageCount; i++)
                        output.AddPage(input.Pages[i]);
                }
            }
            output.Save(outputPath);
        }

        /// <summary>
        /// Rotates the specified pages in sourcePath by degrees (90, 180, or 270) and saves to outputPath.
        /// pageNumbers is 1-based. Pass null to rotate all pages.
        /// </summary>
        public void RotatePages(string sourcePath, IEnumerable<int> pageNumbers, int degrees, string outputPath)
        {
            using (var input = PdfReader.Open(sourcePath, PdfDocumentOpenMode.Import))
            {
                var output = new PdfDocument();
                var targetPages = pageNumbers != null ? new HashSet<int>(pageNumbers) : null;

                for (int i = 0; i < input.PageCount; i++)
                {
                    var page = output.AddPage(input.Pages[i]);
                    bool shouldRotate = targetPages == null || targetPages.Contains(i + 1);
                    if (shouldRotate)
                        page.Rotate = NormalizeRotation(page.Rotate + degrees);
                }
                output.Save(outputPath);
            }
        }

        private static int NormalizeRotation(int degrees)
        {
            degrees %= 360;
            if (degrees < 0) degrees += 360;
            return degrees;
        }

        /// <summary>
        /// Parses a page range string like "1, 3, 5-8, 10" into a sorted list of 1-based page numbers.
        /// </summary>
        public static List<int> ParsePageRange(string rangeStr, int totalPages)
        {
            if (string.IsNullOrWhiteSpace(rangeStr))
                throw new ArgumentException("Please enter page numbers.");

            var result = new HashSet<int>();
            foreach (var part in rangeStr.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var token = part.Trim();
                if (token.Contains("-"))
                {
                    var bounds = token.Split(new[] { '-' }, 2);
                    if (!int.TryParse(bounds[0].Trim(), out int start) ||
                        !int.TryParse(bounds[1].Trim(), out int end))
                        throw new FormatException("Invalid range format: '" + token + "'.");
                    if (start < 1 || end > totalPages || start > end)
                        throw new ArgumentOutOfRangeException("Range " + token + " is out of valid bounds (1-" + totalPages + ").");
                    for (int i = start; i <= end; i++)
                        result.Add(i);
                }
                else
                {
                    if (!int.TryParse(token, out int page))
                        throw new FormatException("Invalid page number: '" + token + "'.");
                    if (page < 1 || page > totalPages)
                        throw new ArgumentOutOfRangeException("Page " + page + " is out of valid bounds (1-" + totalPages + ").");
                    result.Add(page);
                }
            }

            var list = result.ToList();
            list.Sort();
            return list;
        }

        // ── Organize ─────────────────────────────────────────────────────

        /// <summary>
        /// Returns size and rotation metadata for every page in the PDF.
        /// </summary>
        public List<PageDetail> GetPageDetails(string filePath)
        {
            var result = new List<PageDetail>();
            using (var doc = PdfReader.Open(filePath, PdfDocumentOpenMode.Import))
            {
                for (int i = 0; i < doc.PageCount; i++)
                {
                    var page = doc.Pages[i];
                    result.Add(new PageDetail
                    {
                        WidthPt  = page.Width.Point,
                        HeightPt = page.Height.Point,
                        Rotate   = page.Rotate
                    });
                }
            }
            return result;
        }

        /// <summary>
        /// Builds a new PDF from an ordered list of (sourceFile, 0-based pageIndex) pairs.
        /// Supports mixing pages from multiple source files (delete, reorder, insert).
        /// </summary>
        public void BuildDocument(IList<KeyValuePair<string, int>> sourcePages, string outputPath)
        {
            var opened = new Dictionary<string, PdfDocument>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var output = new PdfDocument();
                foreach (var entry in sourcePages)
                {
                    if (!opened.TryGetValue(entry.Key, out var srcDoc))
                    {
                        srcDoc = PdfReader.Open(entry.Key, PdfDocumentOpenMode.Import);
                        opened[entry.Key] = srcDoc;
                    }
                    output.AddPage(srcDoc.Pages[entry.Value]);
                }
                output.Save(outputPath);
            }
            finally
            {
                foreach (var doc in opened.Values)
                    doc.Dispose();
            }
        }

        // ── Watermark ───────────────────────────────────────────────

        public void AddTextWatermark(string sourcePath, WatermarkOptions opts, string outputPath)
        {
            using (var doc = PdfReader.Open(sourcePath, PdfDocumentOpenMode.Modify))
            {
                for (int i = 0; i < doc.PageCount; i++)
                {
                    if (opts.PageNumbers != null && !opts.PageNumbers.Contains(i + 1))
                        continue;

                    var page = doc.Pages[i];
                    using (var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append))
                        DrawWatermark(gfx, page, opts);
                }
                doc.Save(outputPath);
            }
        }

        private static void DrawWatermark(XGraphics gfx, PdfPage page, WatermarkOptions opts)
        {
            var style = opts.FontBold ? XFontStyle.Bold : XFontStyle.Regular;
            var font  = new XFont("Arial", opts.FontSize, style);

            int alpha = Math.Max(0, Math.Min(255, (int)Math.Round(opts.Opacity * 255)));
            int argb  = (alpha << 24) | (opts.ColorR << 16) | (opts.ColorG << 8) | opts.ColorB;
            var brush = new XSolidBrush(XColor.FromArgb(argb));

            double pw = page.Width.Point;
            double ph = page.Height.Point;
            XSize  sz = gfx.MeasureString(opts.Text, font);

            GetWatermarkOrigin(opts.Position, pw, ph, sz, out double cx, out double cy);

            var state = gfx.Save();
            try
            {
                gfx.TranslateTransform(cx, cy);
                if (opts.Rotation != 0)
                    gfx.RotateTransform(opts.Rotation);
                gfx.DrawString(opts.Text, font, brush, 0, 0, XStringFormats.Center);
            }
            finally
            {
                gfx.Restore(state);
            }
        }

        private static void GetWatermarkOrigin(
            WatermarkPosition pos, double pw, double ph, XSize sz,
            out double x, out double y)
        {
            const double M = 36;
            double hw = sz.Width  / 2;
            double hh = sz.Height / 2;

            switch (pos)
            {
                case WatermarkPosition.TopLeft:      x = M + hw;      y = M + hh;      break;
                case WatermarkPosition.TopCenter:    x = pw / 2;      y = M + hh;      break;
                case WatermarkPosition.TopRight:     x = pw - M - hw; y = M + hh;      break;
                case WatermarkPosition.MiddleLeft:   x = M + hw;      y = ph / 2;      break;
                case WatermarkPosition.MiddleRight:  x = pw - M - hw; y = ph / 2;      break;
                case WatermarkPosition.BottomLeft:   x = M + hw;      y = ph - M - hh; break;
                case WatermarkPosition.BottomCenter: x = pw / 2;      y = ph - M - hh; break;
                case WatermarkPosition.BottomRight:  x = pw - M - hw; y = ph - M - hh; break;
                default:                             x = pw / 2;      y = ph / 2;      break; // Center
            }
        }

        // ── Security ────────────────────────────────────────────────

        public int GetPageCount(string filePath, string password)
        {
            using (var doc = PdfReader.Open(filePath, password, PdfDocumentOpenMode.Import))
                return doc.PageCount;
        }

        public void EncryptPdf(
            string sourcePath, string sourcePassword,
            string userPassword, string ownerPassword,
            bool permitPrint, bool permitCopy, bool permitModify,
            string outputPath)
        {
            var srcDoc = string.IsNullOrEmpty(sourcePassword)
                ? PdfReader.Open(sourcePath, PdfDocumentOpenMode.Import)
                : PdfReader.Open(sourcePath, sourcePassword, PdfDocumentOpenMode.Import);

            var newDoc = new PdfDocument();
            for (int i = 0; i < srcDoc.PageCount; i++)
                newDoc.AddPage(srcDoc.Pages[i]);

            newDoc.SecuritySettings.DocumentSecurityLevel    = PdfDocumentSecurityLevel.Encrypted128Bit;
            if (!string.IsNullOrEmpty(userPassword))  newDoc.SecuritySettings.UserPassword  = userPassword;
            if (!string.IsNullOrEmpty(ownerPassword)) newDoc.SecuritySettings.OwnerPassword = ownerPassword;
            newDoc.SecuritySettings.PermitPrint            = permitPrint;
            newDoc.SecuritySettings.PermitFullQualityPrint = permitPrint;
            newDoc.SecuritySettings.PermitExtractContent   = permitCopy;
            newDoc.SecuritySettings.PermitModifyDocument   = permitModify;
            newDoc.SecuritySettings.PermitAnnotations      = permitModify;

            newDoc.Save(outputPath);
            srcDoc.Dispose();
        }

        public void RemovePassword(string sourcePath, string password, string outputPath)
        {
            var srcDoc = string.IsNullOrEmpty(password)
                ? PdfReader.Open(sourcePath, PdfDocumentOpenMode.Import)
                : PdfReader.Open(sourcePath, password, PdfDocumentOpenMode.Import);

            var newDoc = new PdfDocument();
            for (int i = 0; i < srcDoc.PageCount; i++)
                newDoc.AddPage(srcDoc.Pages[i]);

            // Save without security settings -> unencrypted output
            newDoc.Save(outputPath);
            srcDoc.Dispose();
        }

        // ── Split ───────────────────────────────────────────────

        public List<string> SplitPdfByCount(string sourcePath, int pagesPerFile, string outputFolder, string prefix)
        {
            var outputPaths = new List<string>();
            using (var input = PdfReader.Open(sourcePath, PdfDocumentOpenMode.Import))
            {
                int total = input.PageCount;
                int fileIndex = 1;
                for (int start = 0; start < total; start += pagesPerFile)
                {
                    var outDoc = new PdfDocument();
                    int end = Math.Min(start + pagesPerFile, total);
                    for (int i = start; i < end; i++)
                        outDoc.AddPage(input.Pages[i]);
                    string fileName = prefix + "_" + fileIndex.ToString("D3") + ".pdf";
                    string filePath = Path.Combine(outputFolder, fileName);
                    outDoc.Save(filePath);
                    outputPaths.Add(filePath);
                    fileIndex++;
                }
            }
            return outputPaths;
        }

        public List<string> SplitPdfByRanges(string sourcePath, string rangesInput, string outputFolder, string prefix)
        {
            var outputPaths = new List<string>();
            using (var input = PdfReader.Open(sourcePath, PdfDocumentOpenMode.Import))
            {
                int total = input.PageCount;
                var segments = rangesInput.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                int fileIndex = 1;
                foreach (var seg in segments)
                {
                    var pages = ParsePageRange(seg.Trim(), total);
                    var outDoc = new PdfDocument();
                    foreach (int p in pages)
                        outDoc.AddPage(input.Pages[p - 1]);
                    string fileName = prefix + "_part" + fileIndex + ".pdf";
                    string filePath = Path.Combine(outputFolder, fileName);
                    outDoc.Save(filePath);
                    outputPaths.Add(filePath);
                    fileIndex++;
                }
            }
            return outputPaths;
        }

        // ── Metadata ─────────────────────────────────────────────

        public PdfMetadata GetPdfMetadata(string filePath)
        {
            using (var doc = PdfReader.Open(filePath, PdfDocumentOpenMode.Import))
            {
                string creationDate = "";
                try
                {
                    var dt = doc.Info.CreationDate;
                    if (dt.Year > 1900)
                        creationDate = dt.ToString("yyyy-MM-dd  HH:mm:ss");
                }
                catch { }

                return new PdfMetadata
                {
                    Title        = doc.Info.Title    ?? "",
                    Author       = doc.Info.Author   ?? "",
                    Subject      = doc.Info.Subject  ?? "",
                    Keywords     = doc.Info.Keywords ?? "",
                    Creator      = doc.Info.Creator  ?? "",
                    Producer     = doc.Info.Producer ?? "",
                    CreationDate = creationDate
                };
            }
        }

        public void SetPdfMetadata(string sourcePath, PdfMetadata meta, string outputPath)
        {
            using (var input = PdfReader.Open(sourcePath, PdfDocumentOpenMode.Import))
            {
                var output = new PdfDocument();
                for (int i = 0; i < input.PageCount; i++)
                    output.AddPage(input.Pages[i]);

                output.Info.Title    = meta.Title    ?? "";
                output.Info.Author   = meta.Author   ?? "";
                output.Info.Subject  = meta.Subject  ?? "";
                output.Info.Keywords = meta.Keywords ?? "";
                output.Info.Creator  = meta.Creator  ?? "";

                output.Save(outputPath);
            }
        }
    }

    public class PageDetail
    {
        public double WidthPt  { get; set; }
        public double HeightPt { get; set; }
        public int    Rotate   { get; set; }
    }
}
