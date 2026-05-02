using System;

namespace FileForge.Core
{
    public class PatchEntry
    {
        public long Offset { get; set; }
        public byte[] NewBytes { get; set; }
        public string Description { get; set; }

        public string OffsetDisplay => $"0x{Offset:X8}";
        public string SizeDisplay => NewBytes != null ? $"{NewBytes.Length} bytes" : "0 bytes";
        public string BytesDisplay => NewBytes != null && NewBytes.Length > 0
            ? BitConverter.ToString(NewBytes).Replace("-", " ")
            : "";

        public override string ToString() =>
            $"{OffsetDisplay}  {BytesDisplay}  {Description}";
    }
}
