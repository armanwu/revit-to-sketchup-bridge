# Revit to SketchUp Bridge

[![Revit Version](https://img.shields.io/badge/Revit-2021%20--%202027%2B-blue.svg)](https://www.autodesk.com/products/revit/overview)
[![SketchUp Compatible](https://img.shields.io/badge/SketchUp-2017%20--%202026%2B-red.svg)](https://www.sketchup.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows-lightgray.svg)]()

> Seamlessly export 3D geometry from Autodesk Revit to Trimble SketchUp using an efficient, lightweight JSON-based workflow.

---

## 🌟 Key Features

* 🚀 **One-Click Installation**: Automated installer script detects your installed Revit version, builds the C# add-in, and deploys it automatically—no Visual Studio required.
* 🎯 **Smart Geometry Export**: Exports selected elements or all visible geometry in the active 3D view.
* 🏷️ **Clean Tag Organization**: Imported elements in SketchUp are automatically organized into Tags based on their Revit categories (e.g., `Revit - Walls`, `Revit - Doors`, `Revit - Windows`).
* 🎨 **Material & Color Preservation**: Reads per-face colors and materials directly from Revit families, types, and subcategories.
* ⭕ **Smooth Curved Surfaces**: Reconstructs Revit cylindrical surfaces (curved walls, columns, pipes) as smooth, segmented geometry with hidden coplanar seams in SketchUp.
* 🔄 **Broad Revit Compatibility**: Works across Revit 2021, 2022, 2023, 2024, 2025, 2026, and 2027+.

---

## 🔄 Workflow Overview

```text
  ┌─────────────────┐       Export        ┌──────────────────┐       Import        ┌──────────────────┐
  │   Revit Model   │  ────────────────>  │  .json Data File │  ────────────────>  │  SketchUp Model  │
  └─────────────────┘                     └──────────────────┘                     └──────────────────┘
```

---

## 💻 Requirements & Compatibility

| Software | Supported Versions | Notes |
|---|---|---|
| **Autodesk Revit** | 2021, 2022, 2023, 2024, 2025, 2026, 2027+ | All 64-bit editions |
| **Trimble SketchUp** | 2017 or newer | Pro, Studio, or Desktop |
| **Operating System** | Windows 10 / 11 (64-bit) | Requires MSBuild / .NET Build Tools (Free) |

---

## 📦 Installation Guide

### 1. Install the Revit Add-in

1. Download or clone this repository.
2. Double-click **`install.bat`**.
   * The installer automatically detects your installed Revit version(s), compiles the add-in, and copies the binary files to your Revit add-ins directory.
3. Launch or restart Revit.
4. A new **SketchUp Bridge** panel will appear under the **Add-Ins** tab.

> ℹ️ **Note on MSBuild:** Because this is a compiled C# Revit add-in, your computer needs **MSBuild** (included with *Build Tools for Visual Studio* or .NET SDK). If MSBuild is missing, `install.bat` will notify you with a download link.

### 2. Install the SketchUp Extension

1. Open SketchUp.
2. Go to **Window > Extension Manager**.
3. Click **Install Extension** and select **`RevitJsonImporter.rbz`** inside the `SketchUpExtension/` folder.
4. A new menu item will appear under **Extensions > Import from Revit (JSON)**.

---

## 🚀 How to Use

| In Revit | In SketchUp |
|---|---|
| 1. Open your 3D view and select specific elements (or leave unselected to export all visible 3D geometry). | 1. Open SketchUp and navigate to **Extensions > Import from Revit (JSON)**. |
| 2. Click **Export RVT to JSON** on the **SketchUp Bridge** panel (*Add-Ins* tab). | 2. Choose the `.json` file exported from Revit. |
| 3. Choose a destination folder and save the `.json` file. | 3. The geometry appears neatly grouped by Revit category with preserved material colors. |

---

## 📁 Repository Structure

```text
revit-to-sketchup-bridge/
├── install.bat                      # One-click installer launcher
├── uninstall.bat                    # One-click uninstaller launcher
├── install-revit-addin.ps1          # PowerShell build & deployment script
├── uninstall-revit-addin.ps1        # PowerShell removal script
├── RevitAddin/                      # C# Revit Add-in source code
│   ├── icon/                        # Add-in ribbon icons (16x16, 32x32)
│   ├── App.cs                       # UI ribbon initialization
│   ├── ExportCommand.cs             # Geometry extraction & JSON exporter
│   └── RevitToSketchUpExporter.csproj
└── SketchUpExtension/               # Ruby SketchUp extension package
    ├── RevitJsonImporter.rbz        # Pre-packaged SketchUp extension
    ├── revit_importer_extension.rb  # Extension loader
    └── revit_importer/              # Importer logic & surface builder
```

---

## ⚠️ Known Limitations

* **Static Geometry**: Exported models are transferred as static polygonal faces rather than parametric Revit families.
* **Large Models**: For complex projects with tens of thousands of elements, exporting selected elements or section-boxed views is recommended for optimal performance.
* **Colors & Materials**: Materials are resolved per-face. Elements without assigned materials receive a neutral gray fallback color (`RGB 200, 200, 200`).

---

## 📄 License & Copyright

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

This project is open-source software distributed under the terms of the **[MIT License](LICENSE)**.

```text
Copyright (c) 2026 Arman Arisman
```

You are free to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of this software for both commercial and non-commercial purposes. See the [`LICENSE`](LICENSE) file for complete details.
