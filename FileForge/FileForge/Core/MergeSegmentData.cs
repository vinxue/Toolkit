namespace FileForge.Core
{
    /// <summary>
    /// Describes a single segment in a composed merge operation:
    /// either a source file or a generated fill buffer.
    /// </summary>
    public class MergeSegmentData
    {
        /// <summary>true = file segment; false = fill-buffer segment.</summary>
        public bool IsFile { get; set; }

        // ── File segment ──────────────────────────────────────────────────────

        public string FilePath { get; set; }

        // ── Buffer segment ────────────────────────────────────────────────────

        public long     FillSize     { get; set; }
        public FillMode FillMode     { get; set; }
        public byte     SpecificByte { get; set; }
        public byte[]   HexPattern   { get; set; }
    }
}
