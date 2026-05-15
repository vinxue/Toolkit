using System.ComponentModel;
using System.IO;
using FileForge.Core;

namespace FileForge.Views
{
    public enum SegmentKind { File, Buffer }

    /// <summary>
    /// UI-level model for one segment in the merge composition sequence.
    /// Implements INotifyPropertyChanged so the ListView DataTemplate updates live.
    /// </summary>
    public class MergeSegment : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Notify(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // ── Kind ──────────────────────────────────────────────────────────────

        private SegmentKind _kind;
        public SegmentKind Kind
        {
            get => _kind;
            set { _kind = value; Notify(nameof(Kind)); Notify(nameof(TypeLabel)); Notify(nameof(Summary)); }
        }

        // ── File fields ───────────────────────────────────────────────────────

        private string _filePath = "";
        public string FilePath
        {
            get => _filePath;
            set { _filePath = value ?? ""; Notify(nameof(FilePath)); Notify(nameof(Summary)); }
        }

        // ── Buffer fields ──────────────────────────────────────────────────────

        private string _fillSizeText = "512";
        public string FillSizeText
        {
            get => _fillSizeText;
            set { _fillSizeText = value ?? "512"; Notify(nameof(FillSizeText)); Notify(nameof(Summary)); }
        }

        private string _fillSizeUnit = "Bytes";
        public string FillSizeUnit
        {
            get => _fillSizeUnit;
            set { _fillSizeUnit = value ?? "Bytes"; Notify(nameof(FillSizeUnit)); Notify(nameof(Summary)); }
        }

        private int _fillModeIndex;
        public int FillModeIndex
        {
            get => _fillModeIndex;
            set { _fillModeIndex = value; Notify(nameof(FillModeIndex)); Notify(nameof(Summary)); }
        }

        private string _specificByte = "FF";
        public string SpecificByte
        {
            get => _specificByte;
            set { _specificByte = value ?? "FF"; Notify(nameof(SpecificByte)); Notify(nameof(Summary)); }
        }

        private string _hexPattern = "DE AD BE EF";
        public string HexPattern
        {
            get => _hexPattern;
            set { _hexPattern = value ?? ""; Notify(nameof(HexPattern)); Notify(nameof(Summary)); }
        }

        // ── Computed display properties ───────────────────────────────────────

        public string TypeLabel => Kind == SegmentKind.File ? "FILE" : "BUFFER";

        public string Summary
        {
            get
            {
                if (Kind == SegmentKind.File)
                {
                    if (string.IsNullOrWhiteSpace(_filePath)) return "(no file selected)";
                    string name = Path.GetFileName(_filePath);
                    try
                    {
                        if (File.Exists(_filePath))
                            return name + "  (" + FileEngine.FormatSize(new FileInfo(_filePath).Length) + ")";
                    }
                    catch { }
                    return name;
                }
                else
                {
                    string fillPart;
                    switch (_fillModeIndex)
                    {
                        case 1:  fillPart = "Byte 0x" + _specificByte.Trim().ToUpperInvariant(); break;
                        case 2:  fillPart = "Hex Pattern"; break;
                        case 3:  fillPart = "Random"; break;
                        default: fillPart = "Zeros"; break;
                    }
                    return _fillSizeText + " " + _fillSizeUnit + "  \u2014 " + fillPart;
                }
            }
        }

        /// <summary>Parses FillSizeText + FillSizeUnit into bytes. Returns 0 if invalid.</summary>
        public long FillSizeBytes
        {
            get
            {
                try { return FileEngine.ParseSize(_fillSizeText, _fillSizeUnit); }
                catch { return 0; }
            }
        }
    }
}
