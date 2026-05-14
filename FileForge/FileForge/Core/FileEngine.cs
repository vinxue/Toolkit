using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace FileForge.Core
{
    public enum FillMode
    {
        Zeros,
        SpecificByte,
        HexPattern,
        Random
    }

    public static class FileEngine
    {
        private const int BufferSize = 65536; // 64 KB

        // ── Append / Prepend ───────────────────────────────────────────────────

        /// <summary>
        /// Prepend and/or append fill data to a file.
        /// prependSize = 0 means no prepend; appendSize = 0 means no append.
        /// </summary>
        public static void AppendPrepend(string inputPath, string outputPath,
            long prependSize, long appendSize,
            FillMode fillMode, byte specificByte, byte[] hexPattern)
        {
            if (!File.Exists(inputPath)) throw new FileNotFoundException("Input file not found.", inputPath);
            if (prependSize < 0 || appendSize < 0) throw new ArgumentException("Fill size cannot be negative.");
            if (prependSize == 0 && appendSize == 0) throw new ArgumentException("At least one fill size must be > 0.");

            using (var output = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
            {
                if (prependSize > 0)
                    WriteFillData(output, fillMode, prependSize, specificByte, hexPattern);
                CopyFileToStream(inputPath, output);
                if (appendSize > 0)
                    WriteFillData(output, fillMode, appendSize, specificByte, hexPattern);
            }
        }

        private static void WriteFillData(Stream output, FillMode mode, long size,
            byte specificByte, byte[] hexPattern)
        {
            if (mode == FillMode.HexPattern && (hexPattern == null || hexPattern.Length == 0))
                throw new ArgumentException("Hex pattern cannot be empty.");

            long remaining = size;
            long absPos = 0;

            RNGCryptoServiceProvider rng = mode == FillMode.Random ? new RNGCryptoServiceProvider() : null;
            try
            {
                while (remaining > 0)
                {
                    int toWrite = (int)Math.Min(BufferSize, remaining);
                    byte[] chunk = new byte[toWrite];

                    switch (mode)
                    {
                        case FillMode.Zeros:
                            break; // already zeroed
                        case FillMode.SpecificByte:
                            for (int i = 0; i < toWrite; i++) chunk[i] = specificByte;
                            break;
                        case FillMode.HexPattern:
                            for (int i = 0; i < toWrite; i++)
                                chunk[i] = hexPattern[(absPos + i) % hexPattern.Length];
                            break;
                        case FillMode.Random:
                            rng.GetBytes(chunk);
                            break;
                    }

                    output.Write(chunk, 0, toWrite);
                    absPos += toWrite;
                    remaining -= toWrite;
                }
            }
            finally
            {
                rng?.Dispose();
            }
        }

        // ── Generate ───────────────────────────────────────────────────────────

        /// <summary>
        /// Create a new file of the specified size filled with the given fill content.
        /// </summary>
        public static void GenerateFile(string outputPath, long size,
            FillMode fillMode, byte specificByte, byte[] hexPattern)
        {
            if (size <= 0) throw new ArgumentException("File size must be greater than zero.");

            using (var output = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
                WriteFillData(output, fillMode, size, specificByte, hexPattern);
        }

        // ── Split ──────────────────────────────────────────────────────────────

        public static List<string> SplitBySize(string inputPath, string outputDir,
            string namePattern, long chunkSize)
        {
            if (chunkSize <= 0) throw new ArgumentException("Chunk size must be positive.");
            if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

            var results = new List<string>();
            long fileSize = new FileInfo(inputPath).Length;
            int partIndex = 0;
            byte[] buffer = new byte[BufferSize];

            using (var input = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                long remaining = fileSize;
                while (remaining > 0)
                {
                    string outPath = BuildPartName(outputDir, namePattern, partIndex,
                        Path.GetFileNameWithoutExtension(inputPath), Path.GetExtension(inputPath));
                    long partRemaining = Math.Min(chunkSize, remaining);

                    using (var output = new FileStream(outPath, FileMode.Create, FileAccess.Write))
                    {
                        while (partRemaining > 0)
                        {
                            int toRead = (int)Math.Min(buffer.Length, partRemaining);
                            int read = input.Read(buffer, 0, toRead);
                            if (read == 0) break;
                            output.Write(buffer, 0, read);
                            partRemaining -= read;
                        }
                    }

                    results.Add(outPath);
                    remaining -= Math.Min(chunkSize, remaining);
                    partIndex++;
                }
            }
            return results;
        }

        public static List<string> SplitByOffsets(string inputPath, string outputDir,
            string namePattern, long[] offsets)
        {
            if (offsets == null || offsets.Length == 0)
                throw new ArgumentException("At least one offset is required.");
            if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

            Array.Sort(offsets);
            long fileSize = new FileInfo(inputPath).Length;

            var cutPoints = new List<long> { 0 };
            foreach (long o in offsets)
                if (o > 0 && o < fileSize) cutPoints.Add(o);
            cutPoints.Add(fileSize);

            var results = new List<string>();

            using (var input = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                for (int i = 0; i < cutPoints.Count - 1; i++)
                {
                    long start = cutPoints[i];
                    long end = cutPoints[i + 1];
                    if (start >= end) continue;

                    string outPath = BuildPartName(outputDir, namePattern, i,
                        Path.GetFileNameWithoutExtension(inputPath), Path.GetExtension(inputPath));
                    input.Seek(start, SeekOrigin.Begin);
                    CopyStreamSection(input, outPath, end - start);
                    results.Add(outPath);
                }
            }
            return results;
        }

        // ── Line-based operations ──────────────────────────────────────────────

        /// <summary>
        /// Counts the number of lines in a text file. Supports LF, CRLF, and CR line endings.
        /// The last line is counted even when not terminated by a newline.
        /// </summary>
        public static long CountLines(string inputPath)
        {
            long count = 0;
            byte lastByte = 0;
            bool hasContent = false;
            byte[] buf = new byte[BufferSize];

            using (var fs = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                int read;
                while ((read = fs.Read(buf, 0, buf.Length)) > 0)
                {
                    hasContent = true;
                    for (int i = 0; i < read; i++)
                    {
                        if (buf[i] == (byte)'\n') count++;
                        lastByte = buf[i];
                    }
                }
            }
            // Count final line if it has no terminating newline
            if (hasContent && lastByte != (byte)'\n') count++;
            return count;
        }

        /// <summary>
        /// Extracts the first <paramref name="lineCount"/> lines to a single output file.
        /// Output path is derived from <paramref name="namePattern"/> with index 0.
        /// </summary>
        public static List<string> ExtractFirstLines(string inputPath, string outputDir,
            string namePattern, long lineCount)
        {
            if (lineCount <= 0) throw new ArgumentException("Line count must be positive.");
            if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

            string outPath = BuildPartName(outputDir, namePattern, 0,
                Path.GetFileNameWithoutExtension(inputPath), Path.GetExtension(inputPath));

            byte[] buf = new byte[BufferSize];
            using (var input = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var output = new FileStream(outPath, FileMode.Create, FileAccess.Write))
            {
                long linesFound = 0;
                bool done = false;
                while (!done)
                {
                    int read = input.Read(buf, 0, buf.Length);
                    if (read == 0) break;
                    int writeUpTo = read;
                    for (int i = 0; i < read; i++)
                    {
                        if (buf[i] == (byte)'\n')
                        {
                            linesFound++;
                            if (linesFound >= lineCount) { writeUpTo = i + 1; done = true; break; }
                        }
                    }
                    output.Write(buf, 0, writeUpTo);
                }
            }
            EnsureTrailingNewline(outPath);
            return new List<string> { outPath };
        }

        /// <summary>
        /// Extracts the last <paramref name="lineCount"/> lines to a single output file.
        /// Uses a backward scan for efficiency on large files.
        /// </summary>
        public static List<string> ExtractLastLines(string inputPath, string outputDir,
            string namePattern, long lineCount)
        {
            if (lineCount <= 0) throw new ArgumentException("Line count must be positive.");
            if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

            string outPath = BuildPartName(outputDir, namePattern, 0,
                Path.GetFileNameWithoutExtension(inputPath), Path.GetExtension(inputPath));

            long fileSize = new FileInfo(inputPath).Length;
            if (fileSize == 0) { File.WriteAllBytes(outPath, new byte[0]); return new List<string> { outPath }; }

            long startPos = FindLastNLinesStart(inputPath, fileSize, lineCount);
            using (var input = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                input.Seek(startPos, SeekOrigin.Begin);
                CopyStreamSection(input, outPath, fileSize - startPos);
            }
            EnsureTrailingNewline(outPath);
            return new List<string> { outPath };
        }

        /// <summary>
        /// Extracts lines <paramref name="fromLine"/> through <paramref name="toLine"/> (both 1-based, inclusive).
        /// </summary>
        public static List<string> ExtractLineRange(string inputPath, string outputDir,
            string namePattern, long fromLine, long toLine)
        {
            if (fromLine <= 0) throw new ArgumentException("From line must be ≥ 1.");
            if (toLine < fromLine) throw new ArgumentException("To line must be ≥ from line.");
            if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

            string outPath = BuildPartName(outputDir, namePattern, 0,
                Path.GetFileNameWithoutExtension(inputPath), Path.GetExtension(inputPath));

            byte[] buf = new byte[BufferSize];
            using (var input = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var output = new FileStream(outPath, FileMode.Create, FileAccess.Write))
            {
                long currentLine = 1;
                bool capturing = false;

                while (true)
                {
                    int read = input.Read(buf, 0, buf.Length);
                    if (read == 0) break;

                    int segStart = capturing ? 0 : -1;
                    for (int i = 0; i < read; i++)
                    {
                        if (!capturing && currentLine == fromLine)
                        {
                            capturing = true;
                            segStart = i;
                        }
                        if (buf[i] == (byte)'\n')
                        {
                            if (capturing && currentLine == toLine)
                            {
                                output.Write(buf, segStart, i - segStart + 1);
                                return new List<string> { outPath };
                            }
                            currentLine++;
                        }
                    }
                    if (capturing && segStart >= 0)
                        output.Write(buf, segStart, read - segStart);
                }
            }
            EnsureTrailingNewline(outPath);
            return new List<string> { outPath };
        }

        /// <summary>
        /// Splits a text file into multiple parts each containing at most
        /// <paramref name="linesPerPart"/> lines. The final part holds any remainder.
        /// </summary>
        public static List<string> SplitByLineCount(string inputPath, string outputDir,
            string namePattern, long linesPerPart)
        {
            if (linesPerPart <= 0) throw new ArgumentException("Lines per part must be positive.");
            if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

            var results = new List<string>();
            string baseName = Path.GetFileNameWithoutExtension(inputPath);
            string ext = Path.GetExtension(inputPath);
            byte[] buf = new byte[BufferSize];

            using (var input = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                int partIndex = 0;
                long lineCount = 0;
                string outPath = BuildPartName(outputDir, namePattern, partIndex, baseName, ext);
                results.Add(outPath);
                FileStream output = new FileStream(outPath, FileMode.Create, FileAccess.Write);
                try
                {
                    int read;
                    while ((read = input.Read(buf, 0, buf.Length)) > 0)
                    {
                        int segStart = 0;
                        for (int i = 0; i < read; i++)
                        {
                            if (buf[i] == (byte)'\n')
                            {
                                lineCount++;
                                if (lineCount >= linesPerPart)
                                {
                                    output.Write(buf, segStart, i - segStart + 1);
                                    output.Dispose();
                                    partIndex++;
                                    lineCount = 0;
                                    outPath = BuildPartName(outputDir, namePattern, partIndex, baseName, ext);
                                    results.Add(outPath);
                                    output = new FileStream(outPath, FileMode.Create, FileAccess.Write);
                                    segStart = i + 1;
                                }
                            }
                        }
                        if (segStart < read)
                            output.Write(buf, segStart, read - segStart);
                    }
                }
                finally { output.Dispose(); }
            }

            // Remove empty trailing part (created when file ends exactly on a line boundary)
            if (results.Count > 0)
            {
                string last = results[results.Count - 1];
                if (File.Exists(last) && new FileInfo(last).Length == 0)
                {
                    File.Delete(last);
                    results.RemoveAt(results.Count - 1);
                }
            }
            // Ensure the final part is a valid POSIX text file
            if (results.Count > 0)
                EnsureTrailingNewline(results[results.Count - 1]);
            return results;
        }

        // Appends a single LF if the file is non-empty and does not already end with one,
        // ensuring every extracted text output is a valid POSIX text file.
        private static void EnsureTrailingNewline(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite))
            {
                if (fs.Length == 0) return;
                fs.Seek(-1, SeekOrigin.End);
                if (fs.ReadByte() != (byte)'\n')
                    fs.WriteByte((byte)'\n');
            }
        }

        // Backward scan: returns byte offset of the first byte of the last N lines.
        private static long FindLastNLinesStart(string inputPath, long fileSize, long lineCount)
        {
            const int ChunkSize = 65536;
            byte[] chunk = new byte[ChunkSize];
            long newlinesNeeded = lineCount;
            long pos = fileSize;

            using (var fs = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                // If file ends with \n it is a line terminator, not a separator; skip it.
                if (fileSize > 0)
                {
                    fs.Seek(fileSize - 1, SeekOrigin.Begin);
                    if (fs.ReadByte() == (byte)'\n') pos = fileSize - 1;
                }
                while (pos > 0)
                {
                    long readStart = Math.Max(0, pos - ChunkSize);
                    int toRead = (int)(pos - readStart);
                    fs.Seek(readStart, SeekOrigin.Begin);
                    int read = fs.Read(chunk, 0, toRead);
                    for (int i = read - 1; i >= 0; i--)
                    {
                        if (chunk[i] == (byte)'\n')
                        {
                            if (--newlinesNeeded == 0)
                                return readStart + i + 1;
                        }
                    }
                    pos = readStart;
                }
            }
            return 0; // entire file contains fewer lines than requested
        }

        // ── Merge ──────────────────────────────────────────────────────────────

        public static void MergeFiles(IList<string> inputPaths, string outputPath, byte[] separator = null)
        {
            using (var output = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
            {
                for (int i = 0; i < inputPaths.Count; i++)
                {
                    if (i > 0 && separator != null && separator.Length > 0)
                        output.Write(separator, 0, separator.Length);
                    CopyFileToStream(inputPaths[i], output);
                }
            }
        }

        // ── Region operations ──────────────────────────────────────────────────

        public static void ExtractRegion(string inputPath, string outputPath, long offset, long size)
        {
            ValidateOffset(inputPath, offset);
            long fileSize = new FileInfo(inputPath).Length;
            long available = fileSize - offset;
            if (size <= 0 || size > available) size = available;

            using (var input = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                input.Seek(offset, SeekOrigin.Begin);
                CopyStreamSection(input, outputPath, size);
            }
        }

        public static void InsertData(string inputPath, string outputPath, long offset, byte[] data)
        {
            if (data == null || data.Length == 0) throw new ArgumentException("Data to insert cannot be empty.");
            ValidateOffset(inputPath, offset);

            using (var input = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var output = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
            {
                CopyStreamSection(input, output, offset);
                output.Write(data, 0, data.Length);
                input.Seek(offset, SeekOrigin.Begin);
                CopyRemainingStream(input, output);
            }
        }

        public static void DeleteRegion(string inputPath, string outputPath, long offset, long size)
        {
            ValidateOffset(inputPath, offset);
            long fileSize = new FileInfo(inputPath).Length;
            if (size <= 0) throw new ArgumentException("Delete size must be positive.");
            if (offset + size > fileSize) size = fileSize - offset;

            using (var input = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var output = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
            {
                CopyStreamSection(input, output, offset);
                input.Seek(offset + size, SeekOrigin.Begin);
                CopyRemainingStream(input, output);
            }
        }

        public static void OverwriteRegion(string inputPath, string outputPath, long offset, long size,
            FillMode fillMode, byte specificByte, byte[] hexPattern)
        {
            ValidateOffset(inputPath, offset);
            long fileSize = new FileInfo(inputPath).Length;
            if (offset + size > fileSize)
                throw new ArgumentException("Overwrite region exceeds file size.");

            File.Copy(inputPath, outputPath, true);
            using (var output = new FileStream(outputPath, FileMode.Open, FileAccess.Write))
            {
                output.Seek(offset, SeekOrigin.Begin);
                WriteFillData(output, fillMode, size, specificByte, hexPattern);
            }
        }

        public static void TruncateFile(string inputPath, string outputPath, long size)
        {
            long fileSize = new FileInfo(inputPath).Length;
            if (size <= 0 || size >= fileSize)
                throw new ArgumentException("Truncate size must be between 1 and the file size (exclusive).");

            using (var input = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                CopyStreamSection(input, outputPath, size);
        }

        // ── Alignment / Padding ────────────────────────────────────────────────

        public static long AlignFile(string inputPath, string outputPath, long alignment, byte fillByte)
        {
            if (alignment <= 0) throw new ArgumentException("Alignment must be positive.");
            long fileSize = new FileInfo(inputPath).Length;
            long rem = fileSize % alignment;
            long padSize = rem == 0 ? 0 : alignment - rem;

            using (var output = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
            {
                CopyFileToStream(inputPath, output);
                if (padSize > 0)
                    WriteFillData(output, fillByte == 0 ? FillMode.Zeros : FillMode.SpecificByte, padSize, fillByte, null);
            }
            return padSize;
        }

        // ── Search / Replace ───────────────────────────────────────────────────

        public static List<long> SearchPattern(string inputPath, byte?[] pattern, int maxMatches = 100000)
        {
            if (pattern == null || pattern.Length == 0)
                throw new ArgumentException("Pattern cannot be empty.");

            var matches = new List<long>();
            int patLen = pattern.Length;
            const int BlockSize = 2 * 1024 * 1024; // 2 MB
            byte[] buf = new byte[BlockSize + patLen];
            long blockStart = 0;
            int bufFill = 0;

            using (var fs = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                while (true)
                {
                    int read = fs.Read(buf, bufFill, BlockSize);
                    int totalBuf = bufFill + read;
                    if (totalBuf < patLen) break;

                    int searchEnd = totalBuf - patLen + 1;
                    for (int i = 0; i < searchEnd; i++)
                    {
                        if (PatternMatches(buf, i, pattern))
                        {
                            matches.Add(blockStart + i);
                            if (matches.Count >= maxMatches) return matches;
                        }
                    }

                    if (read == 0) break;

                    int keep = Math.Min(patLen - 1, totalBuf);
                    if (keep > 0)
                        Buffer.BlockCopy(buf, totalBuf - keep, buf, 0, keep);
                    blockStart += totalBuf - keep;
                    bufFill = keep;
                }
            }
            return matches;
        }

        public static long ReplaceAll(string inputPath, string outputPath,
            byte?[] searchPattern, byte[] replaceData)
        {
            var matches = SearchPattern(inputPath, searchPattern);
            if (matches.Count == 0)
            {
                File.Copy(inputPath, outputPath, true);
                return 0;
            }

            int patLen = searchPattern.Length;

            using (var input = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var output = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
            {
                long cursor = 0;
                foreach (long matchOffset in matches)
                {
                    if (matchOffset > cursor)
                    {
                        input.Seek(cursor, SeekOrigin.Begin);
                        CopyStreamSection(input, output, matchOffset - cursor);
                    }
                    output.Write(replaceData, 0, replaceData.Length);
                    cursor = matchOffset + patLen;
                }
                if (cursor < input.Length)
                {
                    input.Seek(cursor, SeekOrigin.Begin);
                    CopyRemainingStream(input, output);
                }
            }
            return matches.Count;
        }

        // ── Patch ─────────────────────────────────────────────────────────────

        public static void ApplyPatch(string inputPath, string outputPath, IList<PatchEntry> entries)
        {
            File.Copy(inputPath, outputPath, true);
            long fileSize = new FileInfo(outputPath).Length;

            using (var fs = new FileStream(outputPath, FileMode.Open, FileAccess.Write))
            {
                foreach (var entry in entries)
                {
                    if (entry.NewBytes == null || entry.NewBytes.Length == 0) continue;
                    if (entry.Offset < 0 || entry.Offset + entry.NewBytes.Length > fileSize)
                        throw new InvalidOperationException(
                            $"Patch at 0x{entry.Offset:X8} (size {entry.NewBytes.Length}) exceeds file size.");
                    fs.Seek(entry.Offset, SeekOrigin.Begin);
                    fs.Write(entry.NewBytes, 0, entry.NewBytes.Length);
                }
            }
        }

        // ── Diff ──────────────────────────────────────────────────────────────

        public static List<DiffEntry> DiffFiles(string pathA, string pathB, int maxDiffs = 50000)
        {
            var diffs = new List<DiffEntry>();
            byte[] bufA = new byte[BufferSize];
            byte[] bufB = new byte[BufferSize];
            long offset = 0;

            using (var streamA = new FileStream(pathA, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var streamB = new FileStream(pathB, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                while (true)
                {
                    int readA = streamA.Read(bufA, 0, BufferSize);
                    int readB = streamB.Read(bufB, 0, BufferSize);
                    int common = Math.Min(readA, readB);

                    for (int i = 0; i < common && diffs.Count < maxDiffs; i++)
                    {
                        if (bufA[i] != bufB[i])
                            diffs.Add(new DiffEntry { Offset = offset + i, ValueA = bufA[i], ValueB = bufB[i] });
                    }

                    // Handle size difference
                    for (int i = common; i < readA && diffs.Count < maxDiffs; i++)
                        diffs.Add(new DiffEntry { Offset = offset + i, ValueA = bufA[i], ValueB = 0 });
                    for (int i = common; i < readB && diffs.Count < maxDiffs; i++)
                        diffs.Add(new DiffEntry { Offset = offset + i, ValueA = 0, ValueB = bufB[i] });

                    offset += Math.Max(readA, readB);
                    if (readA == 0 && readB == 0) break;
                    if (diffs.Count >= maxDiffs) break;
                }
            }
            return diffs;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        public static bool TryParseOffset(string s, out long value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(s)) return false;
            s = s.Trim();
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return long.TryParse(s.Substring(2),
                    System.Globalization.NumberStyles.HexNumber, null, out value);
            return long.TryParse(s, out value);
        }

        public static byte?[] ParseHexPattern(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) throw new ArgumentException("Pattern is empty.");
            s = s.Trim().Replace(" ", "").Replace("\t", "").Replace("-", "");
            if (s.Length % 2 != 0) throw new ArgumentException("Pattern must have an even number of hex digits.");

            var result = new byte?[s.Length / 2];
            for (int i = 0; i < result.Length; i++)
            {
                string hex = s.Substring(i * 2, 2);
                if (hex == "??" || hex == "**")
                    result[i] = null;
                else
                    result[i] = Convert.ToByte(hex, 16);
            }
            return result;
        }

        public static byte[] ParseHexBytes(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return new byte[0];
            s = s.Trim().Replace(" ", "").Replace("\t", "").Replace("-", "");
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s.Substring(2);
            if (s.Length == 0) return new byte[0];
            if (s.Length % 2 != 0) throw new ArgumentException("Hex string must have an even number of digits.");

            byte[] result = new byte[s.Length / 2];
            for (int i = 0; i < result.Length; i++)
                result[i] = Convert.ToByte(s.Substring(i * 2, 2), 16);
            return result;
        }

        public static string BytesToHex(byte[] data)
        {
            if (data == null || data.Length == 0) return "";
            return BitConverter.ToString(data).Replace("-", " ");
        }

        public static string FormatSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024L * 1024) return $"{bytes / 1024.0:F2} KB";
            if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F2} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
        }

        public static long ParseSize(string value, string unit)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Invalid size value.");

            string trimmed = value.Trim();
            long n;
            if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                if (!long.TryParse(trimmed.Substring(2),
                        System.Globalization.NumberStyles.HexNumber, null, out n) || n <= 0)
                    throw new ArgumentException("Invalid size value.");
            }
            else
            {
                if (!long.TryParse(trimmed, out n) || n <= 0)
                    throw new ArgumentException("Invalid size value.");
            }
            try
            {
                switch (unit)
                {
                    case "KB": return checked(n * 1024L);
                    case "MB": return checked(n * 1024L * 1024);
                    case "GB": return checked(n * 1024L * 1024 * 1024);
                    default:   return n;
                }
            }
            catch (OverflowException)
            {
                throw new ArgumentException("Size value is too large.");
            }
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private static bool PatternMatches(byte[] buf, int start, byte?[] pattern)
        {
            for (int i = 0; i < pattern.Length; i++)
                if (pattern[i].HasValue && buf[start + i] != pattern[i].Value)
                    return false;
            return true;
        }

        private static string BuildPartName(string dir, string pattern, int index,
            string baseName, string ext)
        {
            string name = pattern
                .Replace("{n}", (index + 1).ToString("D4"))
                .Replace("{name}", baseName)
                .Replace("{ext}", ext.TrimStart('.'));
            return Path.Combine(dir, name);
        }

        private static void CopyFileToStream(string inputPath, Stream output)
        {
            using (var input = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                CopyRemainingStream(input, output);
        }

        private static void CopyStreamSection(Stream input, Stream output, long size)
        {
            byte[] buffer = new byte[BufferSize];
            long remaining = size;
            while (remaining > 0)
            {
                int toRead = (int)Math.Min(buffer.Length, remaining);
                int read = input.Read(buffer, 0, toRead);
                if (read == 0) break;
                output.Write(buffer, 0, read);
                remaining -= read;
            }
        }

        private static void CopyStreamSection(Stream input, string outputPath, long size)
        {
            using (var output = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
                CopyStreamSection(input, output, size);
        }

        private static void CopyRemainingStream(Stream input, Stream output)
        {
            byte[] buffer = new byte[BufferSize];
            int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                output.Write(buffer, 0, read);
        }

        private static void ValidateOffset(string inputPath, long offset)
        {
            if (offset < 0) throw new ArgumentException("Offset cannot be negative.");
            long fileSize = new FileInfo(inputPath).Length;
            // offset == fileSize is valid: it means "at end of file" (e.g. InsertData appends).
            if (offset > fileSize)
                throw new ArgumentException(
                    $"Offset 0x{offset:X} exceeds file size 0x{fileSize:X}.");
        }
    }
}
