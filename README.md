# Revit → SketchUp Bridge

Export Revit geometry to SketchUp through a simple JSON-based workflow.

## 1️ Install in Revit

1. Double-click **`install.bat`**.

   * The installer automatically detects your Revit version.
   * It builds the add-in and installs it into the correct Revit add-ins folder.
   * You do not need to open Visual Studio.
2. Open or restart Revit.

Done — a new **SketchUp Bridge** ribbon tab will appear automatically.

> **One-time requirement:** Since this is a compiled Revit add-in, your computer needs **MSBuild** for the automatic build step. If MSBuild is not available, `install.bat` will let you know and point you to **Build Tools for Visual Studio**. The full Visual Studio IDE is not required. After installing the build tools, simply run `install.bat` again.

## 2️ Install in SketchUp

1. Open SketchUp → **Window > Extension Manager**.
2. Click **Install Extension**.
3. Select **`RevitJsonImporter.rbz`** from the `SketchUpImporter/` folder.

Done — a new menu item will appear at:

**Extensions > Import Revit Export (JSON)...**

---

## Everyday Usage

| In Revit                                                                                      | In SketchUp                                                                         |
| --------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------- |
| Select elements, or leave nothing selected to export everything visible in the active 3D view | Click **Extensions > Import Revit Export (JSON)...**                                |
| Click **Export to SketchUp (JSON)** on the **SketchUp Bridge** tab                            | Select the `.json` file exported from Revit                                         |
| Save the `.json` file                                                                         | The model appears in SketchUp, grouped by category and level, with its Revit colors |

---

## Uninstalling

### Revit

Double-click **`uninstall.bat`**, then restart Revit.

The **SketchUp Bridge** ribbon tab will be removed.

### SketchUp

1. Open **Window > Extension Manager**.
2. Find **Revit JSON Importer**.
3. Click the uninstall icon next to the extension.

---

## Folder Contents

```text
install.bat
uninstall.bat
install-revit-addin.ps1
uninstall-revit-addin.ps1

RevitAddin/
  ... Revit add-in source code (C#)

SketchUpImporter/
  RevitJsonImporter.rbz
  revit_importer_extension.rb
  revit_importer/
    revit_importer.rb
```

* **`install.bat`** — installs the Revit add-in.
* **`uninstall.bat`** — removes the Revit add-in.
* **`install-revit-addin.ps1`** — installation logic called by `install.bat`.
* **`uninstall-revit-addin.ps1`** — uninstall logic called by `uninstall.bat`.
* **`RevitAddin/`** — Revit add-in source code.
* **`RevitJsonImporter.rbz`** — SketchUp extension package.
* **`revit_importer_extension.rb`** and **`revit_importer.rb`** — SketchUp importer source code.

---

## Known Limitations

* Geometry imported into SketchUp is **static rather than parametric**. Most geometry is transferred as triangulated faces, while supported cylindrical surfaces are reconstructed as smooth segmented geometry. The workflow is intended primarily for visualization and coordination, not for editing geometry in SketchUp and pushing changes back into Revit.
* For very large models, exporting a selection rather than the entire project is recommended so the JSON file remains manageable and the SketchUp import stays responsive.
* Colors are read per face from Revit materials, including materials inherited from Type, Family, or subcategory. If a face has no material information, a default gray color is used.

---

## AI Assistance Disclosure

This project was developed with assistance from AI tools, including **Claude by Anthropic** and **ChatGPT by OpenAI**. These tools were used to support coding, debugging, code review, and documentation.

All AI-generated suggestions were reviewed, tested, and adapted by the project author. The author remains responsible for the final implementation, functionality, and maintenance of this project.

This project is not affiliated with or endorsed by Anthropic or OpenAI.

---

## License

This project is licensed under the **MIT License**.

Copyright © 2026 Arman Arisman
