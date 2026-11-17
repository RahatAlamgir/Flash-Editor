<p align="center"><img src="src/FlashEditor.App/Assets/FlashEditor.png" width="96" alt="Flash Editor icon"></p>

# Flash Editor

A Photoshop-style image editor for Windows with Canva-style editable objects and built-in AI tools.
Written in **C# / .NET 10 / WPF**. AI runs on your PC (no internet, no account), on the graphics card when there is one.

---

## Highlights

| | |
|---|---|
| **AI Remove Background** | One click keeps the person / product and removes the rest, even busy backgrounds. A **Refine brush** paints back or takes away anything the AI got wrong. |
| **Recolor** | Change the color of a dress or T-shirt with a color graph. Folds, shadows and highlights are kept, so fabric still looks real. |
| **Editable objects** | Shapes, text and placed pictures stay editable (like Canva): click to select, drag the handles to resize or rotate, change colors and fonts any time. |
| **AI Upscale / Clean Up** | Makes small or blurry pictures sharper, or 2x to 4x larger with real-looking detail (Real-ESRGAN). |
| **Selections** | Rectangle, ellipse, lasso, polygon lasso, pen (curves), magic wand, quick-select brush, AI "Select Subject". |
| **Export** | Pick format, quality and size, crop to 1:1 / 4:5 / 16:9..., and see the real file size before saving. |

---

## Screenshots

### Change a T-shirt color, keeping the fabric's shading
Select the shirt (Magic Wand or Quick Select), then **Adjust > Recolor**. Tip: remove the background first, so the selection cannot spill into the background.

![Recolor](docs/screenshots/02-recolor-tshirt.png)

### Editable shapes and text
Speech bubble, text and a rotated star, each on its own layer. The **Properties** panel changes fill, border, corners, points, rotation, font and size, with a number box next to every slider.

![Editable objects](docs/screenshots/03-shapes.png)



## Features

### Files and pictures
* Open **PNG, JPG, WEBP, ICO, BMP, GIF, TIFF, JPEG XR, DDS** and, when Windows has the free codec from the Microsoft Store, **HEIC, AVIF, JPEG XL and camera RAW** (DNG, CR2, NEF, ARW...). Drag files onto the window; hold **Ctrl** while dropping to add them as layers.
* **Save** (Ctrl+S) keeps your work as a **project** (a `.flash` file): every layer, shape, text, picture, group, shadow and 3D look stays editable, so you can close the program and carry on later. Open it again with **Open** (Ctrl+O) or by dragging it onto the window.
* **Export** (Ctrl+Alt+S) makes a normal picture: **PNG, JPG, WEBP, ICO** (16 to 256 px), **BMP**, **TIFF** or **GIF** (256 colors, with smart color reduction), with format, quality, output size and crop.
* **Several pictures at once**, one tab each, with their own layers, undo history and zoom. Double-click a tab to rename it.

### AI tools (offline, GPU accelerated)
* **Remove Background** (button in the Layers panel, Ctrl+Alt+B). Sliders for edge sharpness, shrink / grow and feather, with live preview. The result is an **editable picture object**: move, resize, rotate it, give it a **shadow**, and click through its removed (transparent) parts to what lies behind. It remembers the original picture, so it can always be refined.
* **Refine brush (R)**: *Restore* paints back what was removed by mistake, *Remove* takes away leftovers. Alt = the other mode. It paints on the picture itself, so it keeps working after the cut-out was moved, resized, rotated or mirrored, and the shadow follows the new edge. Works on any placed picture too.
* **Select Subject**: selects the main person or object in one click.
* **AI Upscale / Clean Up**: same size but sharper, or 2x / 3x / 4x larger. Shows progress and time left, with Cancel.
* Runs on any DirectX 12 graphics card (NVIDIA, AMD, Intel) through DirectML, otherwise on the processor. Example: cleaning up a 1536 x 1024 picture takes about 25 s on an Intel Arc A750 instead of about 6 min on the processor.

### Editable objects (Canva-style)
* **Shapes (U)**: 36 shapes: rectangle (rounded corners), ellipse, triangle, right triangle, star, polygon, pentagon, hexagon, octagon, sparkle, burst, diamond, trapezoid, parallelogram, heart, arrow, double arrow, chevron, tag, home, cross, check, X mark, lightning bolt, cloud, teardrop, shield, speech bubble, oval bubble, line, and **ring, half ring, half circle, quarter circle, pie, crescent, frame** (with a hole-size or angle setting). Fill and border, each with its own color. Pick a shape from the grid in the tool options; the kind is chosen when you draw it.
* **Reshape corners**: select any object (shape, picture, text, group) and click **Reshape corners** in the Properties panel, then drag its round corner points, inward or outward: a square becomes a trapezium, a star point can be pulled out, a picture or text gets a perspective. Shapes with straight sides move their own corners; other objects move the four corners of their box. Shift = coarser steps, Esc finishes, **Reset** brings back the normal form. One undo step per drag.
* **Text (T)**: any installed font, size, bold, italic, alignment, color. Double-click to edit.
* Placed and pasted pictures are resizable without losing quality.
* Click to select, drag handles to resize, round handle to rotate (Shift = 15° steps). **Shift+click** or **Ctrl+drag a box** selects several; **Ctrl+G** groups, **Ctrl+Shift+G** ungroups, **Ctrl+D** duplicates.
* **Lock** (Ctrl+L or the padlock) prevents moving and editing. A locked object cannot be picked by clicking on the canvas (clicks go through it); select it in the Layers panel to unlock it. Bring to front / back, flip, crop one object, convert to pixels.

### Painting and selecting
* Brush, eraser, paint bucket, eyedropper, foreground / background colors with an HSV color picker. **Brush types** (pick in the tool options, each with a preview): round, square, calligraphy, pencil, chalk, spray, splatter.
* **Retouching tools**: **Clone Stamp (S)** (Alt+click the place to copy from, then paint; *Aligned* keeps the distance between strokes), **Spot Healing (J)** (paint over a blemish, it is healed from a clean place the program finds), **Healing Brush (J)** (like Clone Stamp, but the copied detail takes on the light and color of its new place), **Patch (J)** (draw around a blemish, drag the outline onto a clean area), **Dodge / Burn (O)** (lighten or darken shadows, midtones or highlights; Alt = the other one) and **Blur / Sharpen (Q)** (Alt = the other one). J switches between Spot Healing, Healing Brush and Patch. They paint on pixels (a placed picture is turned into pixels first; **Make editable** brings it back). One undo step per stroke; a selection limits the effect.
* **Shadow size**: the **Size** slider of a shadow makes it smaller (below 100 %) or bigger (above 100 %) than the object, around the middle of the object.* **3D look**: tick **3D look** in the Properties panel to give any shape, text, picture or group a solid side (a block or 3D-letter look) and lit / shaded edges on its face. Pick a preset (Block, Long, Pop, Floor, Left) or set **Depth**, **Angle**, **Edges** (bevel strength) and the side color (automatic = the object's own color, darker). The side follows the real outline, works together with shadows and keeps its direction when the object is turned.
* **Quick merge**: select two or more layers (Ctrl+click in the Layers panel, Shift+click or Ctrl+drag on the canvas) and press **Merge** in the Layers panel, **Ctrl+E**, Layer > Merge Selected Layers, or right-click > Merge N Layers. They become one layer where the top one was, with each layer's opacity kept; hidden and locked layers in the selection are left alone. One undo step. (With one layer selected Ctrl+E still merges it down. Use **Group**, Ctrl+G, to keep objects editable.)
* **Tool groups**: similar tools share one button in the toolbox (like Photoshop). A small triangle in the button corner means more tools: click the triangle (or right-click the button) to open the list and pick one; the button then shows it. Groups: rectangle / ellipse select, lasso / polygon lasso, quick select / magic wand, brush / eraser / refine, spot healing / healing brush / patch.* **Rulers, grid, guides and snapping**: toggle buttons above the canvas (also in the View menu). **Rulers** (Ctrl+R) show pixel units; drag from a ruler onto the picture to make a **guide line**, drag a guide with Select / Move to move it, drag it back onto the ruler to remove it. **Grid** (Ctrl+') with a size you choose. **Snapping** (Ctrl+Shift+;) makes objects, pictures, new shapes and selections click into place against other objects (edges and centers), the canvas edges and center, the grid and the guides, with pink smart-guide lines. **Equal spacing** (like Canva): when you drag an object it also snaps so its gap to a neighbor equals the gaps between other objects in the same row or column, or sits exactly in the middle between two objects, and the gaps are shown in pixels. Hold **Alt** to move without snapping. The settings are remembered.
* Selection tools: rectangle, ellipse, lasso, polygon lasso, **pen (curves)**, magic wand, **quick-select brush**, select subject (AI), select layer content. **Shift** adds, **Alt** subtracts. The program starts with **Select / Move** active.
* Move the selected pixels, copy, cut, paste, delete, fill, invert. Nothing is cut off at the canvas edge; **Image > Reveal All** shows parts moved outside.

### Layers and history
* Add, duplicate, delete, reorder, show / hide, lock, opacity, rename, merge down, flatten. **Select all**, **Lock all** and **Unlock all** buttons act on the selected layers (Ctrl+A with the Select / Move tool also selects every layer).
* A layer of plain pixels (painted, or after "To pixels") has no shadow, size or rotation settings: click **Make editable** in the Properties panel to turn it into a picture object that has them. "To pixels" does the opposite: it makes an object paintable but no longer editable as an object.
* **Groups show their parts** (like Canva): a grouped layer has an arrow to open or close the list of its parts, each with a small picture and its name (double-click to rename).
* **Edit inside a group** (like Canva): double-click a group, or click one of its parts in the Layers panel, to select just that part. Move, resize, rotate, recolor, change text, add a shadow or delete it (Delete) while it stays in the group. **Esc** goes back to the whole group. All undoable.
* **Gradient colors**: tick **Gradient** (Properties) to fill a shape or color a text with a fade through **two to eight colors**. Click a color to change it, drag its slider to move it along the gradient, **+ Add color** for another one, the cross removes one, **Reverse** flips the direction. Or pick one of eleven ready-made looks (Sunset, Ocean, Forest, Candy, Fire, Night, Gold, Glow, Rainbow, Aurora, Sunrise). Set the **Angle**, or tick **Radial** to fade from the middle outward. The gradient turns and mirrors with the object and works together with shadows and 3D.
* **Drop shadow** on any shape, text, picture or group (Properties > Shadow): color, opacity, blur, distance and angle. The shadow follows the real outline of the object. Ten one-click presets, each shown as a small picture of the shadow: Soft, Hard, Lifted, Subtle, Contact, Sticker, Long, Glow, Neon and Left.
* **Drag layers up and down** in the Layers panel to change their order (drag one of several selected layers to move them all). A line shows where the layer will land; one undo step.
* Layer icons are redrawn when you finish a change (a moment after you let go of the mouse), not while you drag, which keeps moving objects smooth.
* New shapes, text and pictures are always added in front of everything else.
* Undo / redo for everything, plus a History panel (click a step to jump back). Undo keeps at most 60 steps and about 1 GB of memory, so editing a very large picture many times cannot use up your RAM (the oldest steps are forgotten first).
* The window opens full screen the first time, then reopens with the size, position and maximized state you left it in (saved in `settings\window.json` next to `FlashEditor.exe`).

### Image
* Image Size / Resolution with 4 resize methods (Lanczos, Bicubic, Bilinear, Nearest) and quick % buttons.
* Canvas Size with anchor, Crop tool (whole picture or one object; 1:1, 4:5, 9:16, 16:9, 3:2, 4:3), Trim, rotate (90°, 180°, any angle), flip.
* **Split Asset Sheet**: select the items (or let it find them), then split into separate layers and / or transparent PNG files.

### Adjustments and filters (all with live preview)
* **Adjust:** Recolor, Auto Levels, Brightness / Contrast, Exposure / Gamma, Hue / Saturation, Vibrance, Temperature / Tint, Grayscale, Invert, Sepia, Posterize, Threshold.
* **Filter:** AI Clean Up, Enhance (no AI), Gaussian Blur, Motion Blur, Sharpen, Reduce Noise, Add Noise, Pixelate, Vignette, Emboss, Find Edges.
* With an active selection they only change the selected area.

Press **F1** in the app for all keyboard shortcuts.

---

## Install

Download one of the two installers (Windows 10 / 11, 64-bit; no .NET needed):

| Installer | Size | AI tools |
|---|---|---|
| `FlashEditor-Setup-1.0.0.exe` (Standard) | 60 MB | Asks to download each AI model (170 MB / 62 MB) the first time you use it |
| `FlashEditor-Setup-1.0.0-AI.exe` | 274 MB | Both AI models included, ready offline |

**Everything is installed in one folder** (default `C:\Program Files\Flash Editor`): the program, all libraries, the .NET runtime and the AI models (`models\`). Nothing is written to AppData or anywhere else. Uninstall (Settings > Apps, or the Start-menu shortcut) removes the whole folder, including models downloaded later. Installing one edition over the other upgrades in place.

### Build the installers

Needs the .NET 10 SDK and [Inno Setup 6](https://jrsoftware.org/isinfo.php) (`winget install JRSoftware.InnoSetup`), and the AI models in the Debug build's `models` folder (use the AI tools once in a Debug run, or pass `-ModelsFrom`):

```bash
powershell -ExecutionPolicy Bypass -File installer\build-installers.ps1 -Version 1.0.0
```

Both installers end up in `installer\output`. The script is `installer\build-installers.ps1`, the installer definition `installer\FlashEditor.iss`.

## Getting started (developers)

You need Windows 10 / 11 and the **.NET 10 SDK**.

```bash
dotnet run --project src/FlashEditor.App
```

Run the tests:

```bash
dotnet test
```

Build a stand-alone app in `publish/`:

```bash
dotnet publish src/FlashEditor.App -c Release -r win-x64 --self-contained false -o publish
```

### AI models

The models are **not** in the repository (they are large). The app downloads each one the first time you use it, after asking:

| Model | Used for | Download | License |
|---|---|---|---|
| IS-Net general use (from the rembg project) | Remove Background, Select Subject | 170 MB | Apache 2.0 |
| Real-ESRGAN x4plus (ONNX build from Qualcomm AI Hub) | AI Upscale / Clean Up | 62 MB | BSD-3 |

Files are checked (size and SHA-256) before use and saved only in the `models` folder next to `FlashEditor.exe`. After that everything works offline. When copying the app to another PC, copy the whole folder.

---

## Keyboard shortcuts (most used)

| Keys | Action |
|---|---|
| V / M / L / P / W | Select & move / rectangle-ellipse select / lasso / pen / quick select-magic wand |
| B / E / R / G / I | Brush / eraser / refine (restore-remove) / bucket / eyedropper |
| S / J / O / Q | Clone stamp / healing tools (press again to switch: spot, brush, patch) / dodge-burn / blur-sharpen |
| Ctrl+R / Ctrl+' / Ctrl+; / Ctrl+Shift+; | Rulers / grid / guides / snapping |
| U / T / C / H / Z | Shapes / text / crop / hand / zoom |
| Ctrl+Z, Ctrl+Y | Undo, redo |
| Ctrl+C, Ctrl+X, Ctrl+V | Copy, cut, paste |
| Ctrl+Shift+D, Ctrl+Shift+I | Deselect, invert selection |
| Ctrl+D | Duplicate layer / object |
| Ctrl+G, Ctrl+Shift+G | Group, ungroup |
| Ctrl+L | Lock / unlock |
| Ctrl+Alt+B | Remove background (AI) |
| Ctrl+Shift+R | Recolor |
| Ctrl+S / Ctrl+Shift+S | Save project (.flash) / save project as |
| Ctrl+Alt+S | Export as a picture (PNG, JPG...) |
| Ctrl+Tab, Ctrl+W | Next picture, close picture |
| Space + drag, mouse wheel | Pan, zoom |

---

## Project structure

Four projects, many small files with one job each:

```
Flash Editor/
├── FlashEditor.slnx                 Solution file
├── docs/screenshots/                Pictures used in this README
├── installer/                       Installer script (Inno Setup) + build-installers.ps1
├── src/
│   ├── FlashEditor.Core/            The image engine. No UI code, unit-tested.
│   │   ├── Imaging/                 PixelBuffer, colors, rectangles, blending math
│   │   ├── Documents/               Document, Layer, LayerState, LayerCrop
│   │   ├── Objects/                 Shape / Text / Image / Group objects, ObjectBox, transforms
│   │   ├── Selections/              Selection masks, polygon rasterizer, outline tracer, quick select
│   │   ├── Sheets/                  Asset sheet splitter
│   │   ├── History/                 Undo / redo manager and one class per kind of undo step
│   │   ├── Painting/                Brush strokes (paint, erase, restore), flood fill, compositing
│   │   ├── Retouch/                 Clone, heal, spot heal, patch, dodge/burn, blur/sharpen operations
│   │   ├── Transforms/              Crop, flip, rotate, resize (Lanczos, Bicubic...)
│   │   ├── Effects/                 Effect framework, Adjustments/, Filters/, Background/
│   │   └── IO/                      Decoder, encoder (PNG/JPG/WebP/ICO/BMP/GIF), exporter; TIFF and unusual formats through Windows codecs; `Project/` reads and writes .flash projects
│   │
│   ├── FlashEditor.AI/              AI features (ONNX Runtime + DirectML). Kept out of Core.
│   │   ├── Models/                  Which model, where from; download once and verify
│   │   ├── Runtime/                 Opens models on the GPU, falls back to the CPU
│   │   ├── Segmentation/            Subject finder (background removal, select subject)
│   │   ├── Upscaling/               Real-ESRGAN in overlapping tiles, blended without seams
│   │   └── Effects/                 AI background removal effect
│   │
│   └── FlashEditor.App/             The Windows app (WPF, MVVM)
│       ├── Assets/                  App icon (.ico for Windows, .png for docs)
│       ├── Themes/                  Colors, icons, styles
│       ├── Rendering/               Bitmaps, vector / text rasterizer, object renderer
│       ├── Services/                Dialogs, clipboard, live effect preview
│       ├── ViewModels/              Screen state; Commands/ has one class per menu
│       ├── Tools/                   One class per tool (Painting/, Selecting/, Shapes/, Text/...)
│       └── Views/                   Main window, canvas, panels, dialogs, reusable controls
└── tests/
    └── FlashEditor.Core.Tests/      xUnit tests (engine + AI helpers; real-model tests run when installed)
```

### How it fits together

* **Core vs App.** All image processing lives in `FlashEditor.Core`, which knows nothing about windows, so it is easy to test and the UI stays thin.
* **MVVM.** Views bind to view models; view models call into the Core and open windows only through `IDialogService`.
* **Pixels.** A layer's pixels are always canvas-sized. Content moved past the edge is kept in a hidden, larger buffer, so nothing is ever cut off.
* **Objects.** An object layer stores a small immutable description (shape, text, image, group) and its pixels are just a drawing of it. Undo only stores the before / after descriptions.
* **Undo.** Every change pushes one `IHistoryAction` after it is applied (pixel changes store only the changed rectangle).
* **Extending.** A new tool is one `ToolBase` class; a new filter is one `ImageEffect` class. The dialog, live preview, selection masking and undo come for free.

---

## Notes and limits

* Saving merges all visible layers into one picture; there is no layered project file yet.
* JPG has no transparency: transparent areas are filled with the background color.
* AI background removal keeps the *main* subject. When it gets a part wrong, fix it with the Refine brush (R).
* AI upscaling makes believable detail but cannot recover detail that was never there (e.g. a tiny face).
* The first AI run after starting the app takes a few extra seconds while the model is prepared for the graphics card.
