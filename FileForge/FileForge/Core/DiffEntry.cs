namespace FileForge.Core
{
    public class DiffEntry
    {
        public long Offset  { get; set; }
        public byte ValueA  { get; set; }
        public byte ValueB  { get; set; }

        public string OffsetHex  => $"0x{Offset:X8}";
        public string ValueAHex  => $"{ValueA:X2}";
        public string ValueBHex  => $"{ValueB:X2}";
    }
}
