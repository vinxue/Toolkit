using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FileForge.Views
{
    /// <summary>
    /// Shared UI utilities used across all views.
    /// Centralises status-display colours and the modern Vista+ folder picker.
    /// </summary>
    internal static class ViewHelper
    {
        // ── Frozen brushes (match App.xaml resource colours) ─────────────────
        private static readonly Brush ErrorBrush;
        private static readonly Brush SuccessBrush;
        private static readonly Brush InfoBrush;

        static ViewHelper()
        {
            ErrorBrush   = new SolidColorBrush(Color.FromRgb(192,  57,  43)); // #C0392B
            SuccessBrush = new SolidColorBrush(Color.FromRgb( 30, 132,  73)); // #1E8449
            InfoBrush    = new SolidColorBrush(Color.FromRgb(127, 140, 141)); // #7F8C8D
            ErrorBrush.Freeze(); SuccessBrush.Freeze(); InfoBrush.Freeze();
        }

        public static void ShowError  (TextBlock tb, string msg) => Set(tb, "\u26a0 " + msg, ErrorBrush);
        public static void ShowSuccess(TextBlock tb, string msg) => Set(tb, "\u2713 " + msg, SuccessBrush);
        public static void ShowInfo   (TextBlock tb, string msg) => Set(tb, msg,              InfoBrush);
        public static void ShowTabStatus(TextBlock tb, string msg, bool ok) =>
            Set(tb, msg, ok ? SuccessBrush : ErrorBrush);

        private static void Set(TextBlock tb, string msg, Brush brush)
        {
            tb.Text       = msg;
            tb.Foreground = brush;
        }

        // ── Modern Vista+ folder picker ───────────────────────────────────────
        /// <summary>
        /// Opens a modern Windows folder-picker dialog (IFileOpenDialog, Vista+).
        /// Falls back to the legacy WinForms dialog if COM fails.
        /// </summary>
        public static string BrowseForFolder(Window owner, string title, string initialPath = null)
        {
            try   { return VistaFolderPicker(owner, title, initialPath); }
            catch
            {
                // Fallback: legacy WinForms tree-view dialog
                using (var dlg = new System.Windows.Forms.FolderBrowserDialog())
                {
                    dlg.Description = title ?? "Select Folder";
                    dlg.ShowNewFolderButton = true;
                    if (!string.IsNullOrEmpty(initialPath) && Directory.Exists(initialPath))
                        dlg.SelectedPath = initialPath;
                    return dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK
                        ? dlg.SelectedPath : null;
                }
            }
        }

        private static string VistaFolderPicker(Window owner, string title, string initialPath)
        {
            IFileDialog dialog = (IFileDialog)Activator.CreateInstance(
                Type.GetTypeFromCLSID(new Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7")));
            try
            {
                dialog.GetOptions(out uint opts);
                // FOS_PICKFOLDERS | FOS_FORCEFILESYSTEM
                dialog.SetOptions(opts | 0x00000020u | 0x00004000u);
                if (title != null) dialog.SetTitle(title);

                if (!string.IsNullOrEmpty(initialPath) && Directory.Exists(initialPath))
                {
                    Guid iid = new Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE");
                    if (SHCreateItemFromParsingName(initialPath, IntPtr.Zero, ref iid,
                            out IShellItem folder) == 0 && folder != null)
                        dialog.SetFolder(folder);
                }

                IntPtr hwnd = owner != null
                    ? new System.Windows.Interop.WindowInteropHelper(owner).Handle
                    : IntPtr.Zero;

                if (dialog.Show(hwnd) != 0) return null;

                dialog.GetResult(out IShellItem result);
                result.GetDisplayName(0x80058000u /* SIGDN_FILESYSPATH */, out string path);
                return path;
            }
            finally { Marshal.ReleaseComObject(dialog); }
        }

        // ── COM interop definitions ───────────────────────────────────────────

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHCreateItemFromParsingName(
            [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
            IntPtr pbc,
            ref Guid riid,
            out IShellItem ppv);

        [ComImport, Guid("42F85136-DB7E-439C-85F1-E4075D135FC8"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IFileDialog
        {
            [PreserveSig] int Show(IntPtr hwnd);
            void SetFileTypes(uint cFileTypes, IntPtr rgFilterSpec);
            void SetFileTypeIndex(uint iFileType);
            void GetFileTypeIndex(out uint piFileType);
            void Advise(IntPtr pfde, out uint pdwCookie);
            void Unadvise(uint dwCookie);
            void SetOptions(uint fos);
            void GetOptions(out uint pfos);
            void SetDefaultFolder(IShellItem psi);
            void SetFolder(IShellItem psi);
            void GetFolder(out IShellItem ppsi);
            void GetCurrentSelection(out IShellItem ppsi);
            void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
            void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
            void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
            void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
            void GetResult(out IShellItem ppsi);
            void AddPlace(IShellItem psi, uint fdap);
            void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
            void Close([MarshalAs(UnmanagedType.Error)] int hr);
            void SetClientGuid(ref Guid guid);
            void ClearClientData();
            void SetFilter(IntPtr pFilter);
        }

        [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem
        {
            void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
            void GetParent(out IShellItem ppsi);
            void GetDisplayName(uint sigdnName,
                [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
            void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
            void Compare(IShellItem psi, uint hint, out int piOrder);
        }
    }
}
