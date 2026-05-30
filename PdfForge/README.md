# PdfForge

A desktop PDF toolkit built with WPF (.NET 8) and [iText 9](https://itextpdf.com/). Provides the most common PDF operations in a clean, modern interface — no cloud upload required, everything runs locally.

![Platform](https://img.shields.io/badge/platform-Windows-blue) ![Framework](https://img.shields.io/badge/.NET-8-purple) ![License](https://img.shields.io/badge/license-AGPL--3.0-red)

---

## Screenshots

Application screenshot:

![Main window](Assets/Screenshot.png)

## Features

### Organize
| Feature | Description |
|---|---|
| **Extract Pages** | Pull a subset of pages from a PDF into a new file using flexible page ranges (e.g. `1-3, 5, 8-10`) |
| **Split** | Divide a PDF into multiple files — either by a fixed page count per file, or by explicit custom ranges |
| **Merge** | Combine multiple PDF files into one; drag-and-drop to reorder, move up/down, or remove files before merging |
| **Organize Pages** | Load a PDF's pages into an editable list, then reorder, delete, or insert pages from another PDF at any position |
| **Rotate Pages** | Rotate all pages or a specific range by 90°, 180°, or 270° |

### Enhance
| Feature | Description |
|---|---|
| **Watermark** | Stamp a customizable text watermark onto pages — control font, size, opacity, color (12 presets), position (3×3 grid), rotation angle, and target page range; full CJK (Chinese/Japanese/Korean) character support |

### Document
| Feature | Description |
|---|---|
| **Metadata** | Read and edit PDF document properties: Title, Author, Subject, Keywords, Creator |
| **Security** | **Encrypt** a PDF with user + owner passwords and fine-grained permissions (print, copy, modify); **Decrypt** an existing password-protected PDF; can process owner-password-protected PDFs without the owner password |

---

## Multilingual Support

The UI supports three languages and can be switched at runtime without restarting the application:

- English
- 简体中文 (Simplified Chinese)
- 繁體中文 (Traditional Chinese)

Language is auto-detected from the system locale on startup.

---

## Requirements

- Windows 10 / 11
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)

---

## Getting Started

1. Clone or download the repository.
2. Open `PdfForge.sln` in Visual Studio 2022 or later.
3. Restore NuGet packages (`itext 9.6.0`, `itext.bouncy-castle-adapter 9.6.0`).
4. Build and run (`Debug` or `Release`).

---

## License

This project is licensed under the **AGPL-3.0 License** because it uses iText 9, which is dual-licensed under AGPL-3.0 for open-source use and a commercial license for proprietary use.

Third-party components bundled with this project retain their original licenses. This project uses iText 9 (AGPL-3.0 / Commercial) — see https://itextpdf.com/how-buy for details.

---
