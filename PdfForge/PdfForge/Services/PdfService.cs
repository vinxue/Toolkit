using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Kernel.Font;
using iText.Kernel.Colors;
using iText.Kernel.Geom;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using iText.Kernel.Crypto;
using PdfForge.Models;

namespace PdfForge.Services
{
    public class PageDetail
    {
        public double WidthPt  { get; set; }
        public double HeightPt { get; set; }
        public int    Rotate   { get; set; }
    }

    public class PdfService
    {
        /// <summary>
        /// Creates a PdfReader that bypasses owner password restrictions.
        /// iText's SetUnethicalReading(true) allows processing PDFs with owner password
        /// without providing the password - equivalent to PdfSharp's default behavior.
        /// </summary>
        private static PdfReader CreateReader(string filePath, string? password = null)
        {
            var readerProps = new ReaderProperties();
            if (!string.IsNullOrEmpty(password))
                readerProps.SetPassword(System.Text.Encoding.UTF8.GetBytes(password));

            var reader = new PdfReader(filePath, readerProps);
            reader.SetUnethicalReading(true);
            return reader;
        }

        public int GetPageCount(string filePath)
        {
            using var reader = CreateReader(filePath);
            using var doc = new PdfDocument(reader);
            return doc.GetNumberOfPages();
        }

        public int GetPageCount(string filePath, string password)
        {
            using var reader = CreateReader(filePath, password);
            using var doc = new PdfDocument(reader);
            return doc.GetNumberOfPages();
        }

        public void ExtractPages(string sourcePath, IEnumerable<int> pageNumbers, string outputPath)
        {
            var pages = pageNumbers.ToList();
            using var reader = CreateReader(sourcePath);
            using var srcDoc = new PdfDocument(reader);

            using var writer = new PdfWriter(outputPath);
            using var dstDoc = new PdfDocument(writer);

            foreach (int pageNum in pages)
            {
                if (pageNum < 1 || pageNum > srcDoc.GetNumberOfPages())
                    throw new ArgumentOutOfRangeException(
                        nameof(pageNumbers), $"Page {pageNum} is out of range (document has {srcDoc.GetNumberOfPages()} pages).");
                srcDoc.CopyPagesTo(pageNum, pageNum, dstDoc);
            }
        }

        public void MergePdfs(IEnumerable<string> sourcePaths, string outputPath)
        {
            using var writer = new PdfWriter(outputPath);
            using var mergedDoc = new PdfDocument(writer);

            foreach (string path in sourcePaths)
            {
                using var reader = CreateReader(path);
                using var srcDoc = new PdfDocument(reader);
                srcDoc.CopyPagesTo(1, srcDoc.GetNumberOfPages(), mergedDoc);
            }
        }

        public void RotatePages(string sourcePath, IEnumerable<int>? pageNumbers, int degrees, string outputPath)
        {
            using var reader = CreateReader(sourcePath);
            using var srcDoc = new PdfDocument(reader);

            using var writer = new PdfWriter(outputPath);
            using var dstDoc = new PdfDocument(writer);

            var targetPages = pageNumbers != null ? new HashSet<int>(pageNumbers) : null;
            int total = srcDoc.GetNumberOfPages();

            srcDoc.CopyPagesTo(1, total, dstDoc);

            for (int i = 1; i <= total; i++)
            {
                bool shouldRotate = targetPages == null || targetPages.Contains(i);
                if (shouldRotate)
                {
                    var page = dstDoc.GetPage(i);
                    int currentRotation = page.GetRotation();
                    int newRotation = NormalizeRotation(currentRotation + degrees);
                    page.SetRotation(newRotation);
                }
            }
        }

        private static int NormalizeRotation(int degrees)
        {
            degrees %= 360;
            if (degrees < 0) degrees += 360;
            return degrees;
        }

        public static List<int> ParsePageRange(string rangeStr, int totalPages)
        {
            if (string.IsNullOrWhiteSpace(rangeStr))
                throw new ArgumentException("Please enter page numbers.");

            var result = new HashSet<int>();
            foreach (var part in rangeStr.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var token = part.Trim();
                if (token.Contains('-'))
                {
                    var bounds = token.Split('-', 2);
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

        public List<PageDetail> GetPageDetails(string filePath)
        {
            var result = new List<PageDetail>();
            using var reader = CreateReader(filePath);
            using var doc = new PdfDocument(reader);

            for (int i = 1; i <= doc.GetNumberOfPages(); i++)
            {
                var page = doc.GetPage(i);
                var size = page.GetPageSize();
                result.Add(new PageDetail
                {
                    WidthPt  = size.GetWidth(),
                    HeightPt = size.GetHeight(),
                    Rotate   = page.GetRotation()
                });
            }
            return result;
        }

        public void BuildDocument(IList<KeyValuePair<string, int>> sourcePages, string outputPath)
        {
            var opened = new Dictionary<string, PdfDocument>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var writer = new PdfWriter(outputPath);
                using var output = new PdfDocument(writer);

                foreach (var entry in sourcePages)
                {
                    if (!opened.TryGetValue(entry.Key, out var srcDoc))
                    {
                        var reader = CreateReader(entry.Key);
                        srcDoc = new PdfDocument(reader);
                        opened[entry.Key] = srcDoc;
                    }
                    int pageNum = entry.Value + 1; // Convert 0-based to 1-based
                    srcDoc.CopyPagesTo(pageNum, pageNum, output);
                }
            }
            finally
            {
                foreach (var doc in opened.Values)
                    doc.Close();
            }
        }

        // ── Watermark ───────────────────────────────────────────────

        /// <summary>
        /// Gets a list of available font families for watermark use.
        /// Returns fonts that support CJK characters.
        /// </summary>
        public static List<string> GetAvailableFonts()
        {
            var fonts = new List<string>
            {
                // CJK fonts
                "Microsoft YaHei",  // 微软雅黑
                "SimHei",           // 黑体
                "SimSun",           // 宋体
                "KaiTi",            // 楷体
                "FangSong",         // 仿宋
                "DengXian",         // 等线
                // Common Latin fonts
                "Segoe UI",
                "Segoe UI Semibold",
                "Segoe UI Light",
                "Arial",
                "Arial Black",
                "Times New Roman",
                "Courier New",
                "Verdana",
                "Tahoma",
                "Georgia",
                "Trebuchet MS",
                "Calibri",
                "Cambria",
                "Consolas",
                "Impact",
            };

            // Filter to only fonts installed on this system, then sort A–Z
            var available = new List<string>();
            foreach (var fontName in fonts)
            {
                string? fontPath = FindSystemFont(fontName);
                if (fontPath != null)
                    available.Add(fontName);
            }

            if (available.Count == 0)
                available.Add("Helvetica"); // iText built-in fallback
            else
                available.Sort(StringComparer.OrdinalIgnoreCase);

            return available;
        }

        private static string? FindSystemFont(string fontName)
        {
            string fontsDir = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);

            // Values: "filename.ttf" for TTF/OTF, or "filename.ttc,N" for TrueType Collections
            // (the index N selects which sub-font inside the .ttc to use).
            var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // CJK
                ["SimHei"]            = "simhei.ttf",
                ["SimSun"]            = "simsun.ttc,0",
                ["KaiTi"]             = "simkai.ttf",
                ["FangSong"]          = "simfang.ttf",
                ["Microsoft YaHei"]   = "msyh.ttc,0",
                ["DengXian"]          = "Deng.ttf",
                // Latin
                ["Segoe UI"]          = "segoeui.ttf",
                ["Segoe UI Semibold"]  = "seguisb.ttf",
                ["Segoe UI Light"]     = "segoeuil.ttf",
                ["Arial"]             = "arial.ttf",
                ["Arial Black"]       = "ariblk.ttf",
                ["Times New Roman"]   = "times.ttf",
                ["Courier New"]       = "cour.ttf",
                ["Verdana"]           = "verdana.ttf",
                ["Tahoma"]            = "tahoma.ttf",
                ["Georgia"]           = "georgia.ttf",
                ["Trebuchet MS"]      = "trebuc.ttf",
                ["Calibri"]           = "calibri.ttf",
                ["Cambria"]           = "cambria.ttc,0",
                ["Consolas"]          = "consola.ttf",
                ["Impact"]            = "impact.ttf",
            };

            if (!mappings.TryGetValue(fontName, out var entry))
                return null;

            // For TTC entries (e.g. "msyh.ttc,0") only the file part is used for existence check
            string fileName = entry.Split(',')[0];
            string physicalPath = System.IO.Path.Combine(fontsDir, fileName);
            if (!File.Exists(physicalPath))
                return null;

            // Return the full entry path including TTC index if present
            return System.IO.Path.Combine(fontsDir, entry);
        }

        private static PdfFont CreateFont(string fontFamily, bool bold)
        {
            string? fontPath = FindSystemFont(fontFamily);
            if (fontPath != null)
            {
                try
                {
                    // FORCE_EMBEDDED ensures all used glyphs (including CJK) are
                    // embedded in the PDF so the file renders correctly on any viewer.
                    return PdfFontFactory.CreateFont(fontPath,
                        iText.IO.Font.PdfEncodings.IDENTITY_H,
                        PdfFontFactory.EmbeddingStrategy.FORCE_EMBEDDED);
                }
                catch { }
            }

            // Fallback: iText built-in Helvetica (Latin only, no CJK)
            return PdfFontFactory.CreateFont(iText.IO.Font.Constants.StandardFonts.HELVETICA);
        }

        public void AddTextWatermark(string sourcePath, WatermarkOptions opts, string outputPath)
        {
            using var reader = CreateReader(sourcePath);
            using var writer = new PdfWriter(outputPath);
            using var pdfDoc = new PdfDocument(reader, writer);

            var font = CreateFont(opts.FontFamily, opts.FontBold);
            int totalPages = pdfDoc.GetNumberOfPages();

            float alpha = (float)Math.Max(0, Math.Min(1, opts.Opacity));
            var baseColor = new DeviceRgb(opts.ColorR, opts.ColorG, opts.ColorB);
            float fontSize = (float)opts.FontSize;

            // Rotation in radians — PDF uses counter-clockwise positive
            float angleRad = (float)(opts.Rotation * Math.PI / 180.0);

            for (int i = 1; i <= totalPages; i++)
            {
                if (opts.PageNumbers != null && !opts.PageNumbers.Contains(i))
                    continue;

                var page = pdfDoc.GetPage(i);
                var pageSize = page.GetPageSize();
                float pw = pageSize.GetWidth();
                float ph = pageSize.GetHeight();

                GetAnchorPoint(opts.Position, pw, ph, out float anchorX, out float anchorY);

                // Canvas.ShowTextAligned(Paragraph, x, y, pageNumber, align, vAlign, angle)
                // routes through iText's layout engine, which uses the font's internal
                // Unicode→GID map to encode ALL characters including CJK correctly.
                // The pageNumber argument is required by this 7-parameter overload.
                var para = new Paragraph(opts.Text)
                    .SetFont(font)
                    .SetFontSize(fontSize)
                    .SetFontColor(baseColor, alpha)   // SetFontColor(Color, float opacity)
                    .SetMargin(0)
                    .SetPadding(0);

                var pdfCanvas = new PdfCanvas(page);
                using var layoutCanvas = new iText.Layout.Canvas(pdfCanvas, pageSize);

                layoutCanvas.ShowTextAligned(para, anchorX, anchorY, i,
                    TextAlignment.CENTER, VerticalAlignment.MIDDLE, angleRad);
            }
        }

        /// <summary>
        /// Returns the target anchor point on the page where the watermark center should be placed.
        /// PDF coordinate system: (0,0) = bottom-left, Y increases upward.
        /// </summary>
        private static void GetAnchorPoint(WatermarkPosition pos, float pw, float ph,
            out float x, out float y)
        {
            const float margin = 72f; // 1 inch margin from edges

            switch (pos)
            {
                case WatermarkPosition.TopLeft:      x = margin;        y = ph - margin; break;
                case WatermarkPosition.TopCenter:    x = pw / 2f;      y = ph - margin; break;
                case WatermarkPosition.TopRight:     x = pw - margin;   y = ph - margin; break;
                case WatermarkPosition.MiddleLeft:   x = margin;        y = ph / 2f;    break;
                case WatermarkPosition.MiddleRight:  x = pw - margin;   y = ph / 2f;    break;
                case WatermarkPosition.BottomLeft:   x = margin;        y = margin;      break;
                case WatermarkPosition.BottomCenter: x = pw / 2f;      y = margin;      break;
                case WatermarkPosition.BottomRight:  x = pw - margin;   y = margin;      break;
                default:                             x = pw / 2f;      y = ph / 2f;    break; // Center
            }
        }

        // ── Security ────────────────────────────────────────────────

        public void EncryptPdf(
            string sourcePath, string? sourcePassword,
            string? userPassword, string? ownerPassword,
            bool permitPrint, bool permitCopy, bool permitModify,
            string outputPath)
        {
            using var reader = CreateReader(sourcePath, sourcePassword);
            
            int permissions = 0;
            if (permitPrint)  permissions |= EncryptionConstants.ALLOW_PRINTING;
            if (permitCopy)   permissions |= EncryptionConstants.ALLOW_COPY;
            if (permitModify) permissions |= EncryptionConstants.ALLOW_MODIFY_CONTENTS | EncryptionConstants.ALLOW_MODIFY_ANNOTATIONS;

            byte[]? userPwdBytes = string.IsNullOrEmpty(userPassword) ? null : System.Text.Encoding.UTF8.GetBytes(userPassword);
            byte[]? ownerPwdBytes = string.IsNullOrEmpty(ownerPassword) ? null : System.Text.Encoding.UTF8.GetBytes(ownerPassword);

            var writerProps = new WriterProperties()
                .SetStandardEncryption(
                    userPwdBytes ?? Array.Empty<byte>(),
                    ownerPwdBytes ?? Array.Empty<byte>(),
                    permissions,
                    EncryptionConstants.ENCRYPTION_AES_128);

            using var writer = new PdfWriter(outputPath, writerProps);
            using var srcDoc = new PdfDocument(reader);
            using var dstDoc = new PdfDocument(writer);

            srcDoc.CopyPagesTo(1, srcDoc.GetNumberOfPages(), dstDoc);
        }

        public void RemovePassword(string sourcePath, string? password, string outputPath)
        {
            using var reader = CreateReader(sourcePath, password);
            using var writer = new PdfWriter(outputPath);
            using var srcDoc = new PdfDocument(reader);
            using var dstDoc = new PdfDocument(writer);

            srcDoc.CopyPagesTo(1, srcDoc.GetNumberOfPages(), dstDoc);
        }

        // ── Split ───────────────────────────────────────────────

        public List<string> SplitPdfByCount(string sourcePath, int pagesPerFile, string outputFolder, string prefix)
        {
            var outputPaths = new List<string>();
            using var reader = CreateReader(sourcePath);
            using var srcDoc = new PdfDocument(reader);

            int total = srcDoc.GetNumberOfPages();
            int fileIndex = 1;

            for (int start = 1; start <= total; start += pagesPerFile)
            {
                int end = Math.Min(start + pagesPerFile - 1, total);
                string fileName = prefix + "_" + fileIndex.ToString("D3") + ".pdf";
                string filePath = System.IO.Path.Combine(outputFolder, fileName);

                using var outWriter = new PdfWriter(filePath);
                using var outDoc = new PdfDocument(outWriter);
                srcDoc.CopyPagesTo(start, end, outDoc);

                outputPaths.Add(filePath);
                fileIndex++;
            }

            return outputPaths;
        }

        public List<string> SplitPdfByRanges(string sourcePath, string rangesInput, string outputFolder, string prefix)
        {
            var outputPaths = new List<string>();
            using var reader = CreateReader(sourcePath);
            using var srcDoc = new PdfDocument(reader);

            int total = srcDoc.GetNumberOfPages();
            var segments = rangesInput.Split(';', StringSplitOptions.RemoveEmptyEntries);
            int fileIndex = 1;

            foreach (var seg in segments)
            {
                var pages = ParsePageRange(seg.Trim(), total);
                string fileName = prefix + "_part" + fileIndex + ".pdf";
                string filePath = System.IO.Path.Combine(outputFolder, fileName);

                using var outWriter = new PdfWriter(filePath);
                using var outDoc = new PdfDocument(outWriter);

                foreach (int p in pages)
                    srcDoc.CopyPagesTo(p, p, outDoc);

                outputPaths.Add(filePath);
                fileIndex++;
            }

            return outputPaths;
        }

        // ── Metadata ─────────────────────────────────────────────

        public PdfMetadata GetPdfMetadata(string filePath)
        {
            using var reader = CreateReader(filePath);
            using var doc = new PdfDocument(reader);
            var info = doc.GetDocumentInfo();

            string creationDate = "";
            try
            {
                var dateStr = info.GetMoreInfo("CreationDate");
                if (!string.IsNullOrEmpty(dateStr))
                    creationDate = dateStr;
            }
            catch { }

            return new PdfMetadata
            {
                Title        = info.GetTitle() ?? "",
                Author       = info.GetAuthor() ?? "",
                Subject      = info.GetSubject() ?? "",
                Keywords     = info.GetKeywords() ?? "",
                Creator      = info.GetCreator() ?? "",
                Producer     = info.GetProducer() ?? "",
                CreationDate = creationDate
            };
        }

        public void SetPdfMetadata(string sourcePath, PdfMetadata meta, string outputPath)
        {
            using var reader = CreateReader(sourcePath);
            using var writer = new PdfWriter(outputPath);
            using var srcDoc = new PdfDocument(reader);
            using var dstDoc = new PdfDocument(writer);

            srcDoc.CopyPagesTo(1, srcDoc.GetNumberOfPages(), dstDoc);

            var info = dstDoc.GetDocumentInfo();
            info.SetTitle(meta.Title ?? "");
            info.SetAuthor(meta.Author ?? "");
            info.SetSubject(meta.Subject ?? "");
            info.SetKeywords(meta.Keywords ?? "");
            info.SetCreator(meta.Creator ?? "");
        }
    }
}
