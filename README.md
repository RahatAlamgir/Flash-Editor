# Flash Editor

A small Photoshop-style image editor for Windows, written in **C# / .NET 10 / WPF**, with **SkiaSharp** for reading and writing image files.

## Features

| Area | What you can do |
|---|---|
| **Several pictures** | Every opened or new picture gets its own **tab** (double-click or right-click a tab to rename it) above the canvas, with its own layers, undo history, selection and zoom. Ctrl+Tab / Ctrl+Shift+Tab switch, Ctrl+W (or the tab's ✕, or a middle-click) closes; unsaved changes are asked about. Dropping several files opens them all. |
| **Files** | Open PNG, JPG, WEBP, ICO (also BMP, GIF). Save as PNG, JPG, WEBP or ICO (the icon file contains sizes 16 to 256 px). Drag an image onto the window to open it, or hold Ctrl while dropping to add it as a layer. |
| **Export** | File > Export (Ctrl+Alt+S): pick format, **quality** and **output size** (smaller or larger), and optionally **crop** (drag a box on the preview, or pick 1:1, 4:5, 9:16, 16:9...), with the real file size shown before saving. The open picture is not changed. |
| **Editable objects (Canva-style)** | Shapes, text and placed / pasted images stay **editable**: click one to select it, drag to move, drag the handles to resize, the round handle to rotate. The **Properties panel** changes fill and border color, border width, corner roundness, star points, shape type, text, font, size, bold / italic, alignment, flip, exact position / size / rotation. Painting on an object turns it into pixels automatically (Ctrl+Z undoes that). Painted (pixel) layers also get handles: resizing one turns it into a resizable image. |
| **Lock** | Lock a layer or object (padlock in the Layers panel, right-click > Lock, or Ctrl+L): it can still be selected and copied, but not moved, resized, painted, edited or deleted. |
| **Group** | Shift+click objects on the canvas (or Ctrl / Shift+click in the Layers panel) to select several; drag to move them together. Ctrl+G groups them so they move, resize and rotate as one; Ctrl+Shift+G ungroups (each part keeps its new place, size and angle). |
| **Right-click menu** | On the canvas: cut, copy, paste, duplicate, delete, bring to front / forward / backward / back, flip, edit text, remove background, select subject, convert to pixels. |
| **Remove background** | The **Remove Background** button at the top of the Layers panel (also Image or Layer menu, or Ctrl+Alt+B) uses **AI** to find the main subject (person, animal, product...), so it works on busy photo backgrounds too. Sliders: edge sharpness, shrink / grow, feather. The AI model (170 MB) is downloaded once on first use and then works offline. **Image > Remove Background by Color** is the older non-AI tool for plain backgrounds: Finds the background color along the edges and makes it transparent, with live preview, tolerance, edge softness and feather settings. |
| **Layers** | Add, duplicate, delete, move up and down, show or hide (eye icon), opacity slider, rename (double-click the name), merge down, flatten. Live thumbnails. |
| **Tools** | Select / Move, Rectangle select, Ellipse select, Lasso, Polygon lasso, Pen, Quick Select, Magic wand, Crop (whole picture or just the selected layer / object; 1:1, 4:5, 16:9... presets), Brush, Eraser, Paint bucket, Eyedropper, Shapes, Text, Hand, Zoom |
| **Shapes** | Rectangle (with rounded corners), ellipse / circle, triangle, star, polygon, heart, arrow, speech bubble, line. Fill on/off + color, border on/off + color + width. Click for a default size or drag; Shift = equal width and height. |
| **Selection** | Rectangle, ellipse / circle, freehand lasso, polygon lasso, **pen (P): smooth curves** (click = corner, click-and-drag = curve, click the first point or Enter to select), **Quick Select brush (W)**: drag over an object and the selection spreads into similar colors around the brush (Alt+drag removes, [ ] = size), magic wand (press W again) (similar color), **Select Subject (AI)**, Select Layer Content. Shift = add to the selection, Alt = subtract. M and L switch between the variants. Drawn with a high-contrast animated outline and a light blue tint. Move the selected pixels (Move tool or arrow keys), copy, cut, paste (as a new layer), delete, fill, select all, deselect, invert. |
| **Move** | Moving a layer or selection past the canvas edge does **not** cut it off: the hidden part is kept and comes back when you move it back. Image > Reveal All grows the canvas to show everything. |
| **Image** | Image Size / Resolution (Ctrl+Alt+I, or click the size in the status bar): shrink or enlarge with quick % buttons and 4 resize methods (Lanczos, Bicubic, Bilinear, Nearest). Canvas Size (Ctrl+Alt+C) with anchor, Reveal All, Trim Transparent Edges, Crop (tool or "Crop to Selection"), rotate 90°, 180° or any angle, flip horizontal or vertical (whole image or one layer). |
| **History** | Undo / Redo plus a History panel (minimized by default; click its header to open). Click any step to jump back to it. |
| **Split asset sheet** | Image > Split Asset Sheet into Pieces (or right-click). **Select the items first** with any selection tool (e.g. Magic Wand on the background, then Select > Invert): every separate selected area becomes one piece, cut out in the selection's shape. Without a selection it finds every separate item on a sheet with a transparent or plain background automatically; shows numbered boxes (updated live), and adds each piece as its own movable layer and / or saves each as a transparent PNG (name_01.png, name_02.png...). "Join parts closer than" keeps an item and its label together. |
| **Crop one object** | Crop tool > "Selected layer / object only", or right-click > Crop This Object: trims just that picture / layer, the canvas stays the same. Right-click > Crop Layer to Selection keeps only the selected shape of a layer. The result stays a movable, resizable picture. |
| **AI Upscale / Clean Up** | Image > AI Upscale / Clean Up (or Filter > AI Clean Up): Real-ESRGAN AI makes a picture sharper and cleaner at the same size, or 2x / 3x / 4x larger with sharp detail. Strength mixes in the original. Works in 128 px tiles blended without seams. Runs on the **graphics card** (DirectML: NVIDIA, AMD or Intel) when there is one, about 20x faster than the processor (a 1536 x 1024 picture: about 25 s instead of 6 min); otherwise on the processor. A progress window shows the time left and has Cancel. Model: Real-ESRGAN x4plus (BSD-3), ONNX build from Qualcomm AI Hub, 62 MB download, installed once. |
| **Enhance** | Filter > Enhance (clean up blur): removes a little noise, sharpens details (ignoring tiny noise-level differences) and adds clarity. Image > Upscale 2x doubles the resolution with Lanczos and then enhances (for small or soft pictures). Helps slightly blurry photos; heavy blur needs AI. |
| **Recolor** | Adjust > Recolor (Ctrl+Shift+R, or right-click): select a part (e.g. a dress with the Magic Wand, Lasso or Select Subject), then pick the new color on a **color graph** (or quick colors / hex). Each pixel keeps its own light and dark, so folds, shadows and highlights stay natural. "Keep shading" and "Strength" fine-tune it; the picture updates live. |
| **Adjustments** | Recolor, Auto Levels, Brightness/Contrast, Exposure/Gamma, Hue/Saturation, Vibrance, Temperature/Tint, Grayscale, Invert, Sepia, Posterize, Threshold |
| **Filters** | Enhance, Gaussian Blur, Motion Blur, Sharpen, Reduce Noise, Add Noise, Pixelate, Vignette, Emboss, Find Edges (each with live preview) |
| **Exact values** | Every slider (brush size, border width, corners, rotation, font size, opacity, tolerance...) has a number box next to it: type a value and press Enter, or use the Up / Down keys. |
| **View** | Mouse-wheel zoom at the cursor, Space + drag or middle mouse to pan, Fit, 100% |
| **Colors** | Foreground and background swatches with an HSV color picker (hex, RGB, quick swatches), swap (X), reset (D) |

Adjustments, filters and Remove Background apply to the **active layer**. If there is a selection, they only change the selected area.

Press **F1** in the app to see all keyboard shortcuts.

## How to run

You need the **.NET 10 SDK**.

```bash
dotnet run --project src/FlashEditor.App
```

Run the tests:

```bash
dotnet test
```

Build a stand-alone `.exe` (in `publish/`):

```bash
dotnet publish src/FlashEditor.App -c Release -r win-x64 --self-contained false -o publish
```

## Project structure

The code is split into four projects:

```
Flash Editor/
├── FlashEditor.slnx                 Solution file (opens all projects)
├── src/
│   ├── FlashEditor.Core/            The image engine. No UI code, fully unit-tested.
│   │   ├── Common/                  ObservableObject (change notifications)
│   │   ├── Imaging/                 PixelBuffer (pixel storage), colors, rectangles, blending math
│   │   ├── Documents/               Document (canvas + layer stack), Layer, LayerState (pixels outside the canvas)
│   │   ├── Objects/                 Editable objects: ShapeObject, TextObject, ImageObject, GroupObject, ObjectBox (position,
│   │   │                            size, rotation), ObjectTransforms, renderer interface
│   │   ├── Sheets/                  SheetSplitter: finds the separate items on an asset / sprite sheet
│   │   ├── Selections/              Selection mask + outline, polygon rasterizer (lasso), mask outline tracer
│   │   ├── History/                 Undo/redo manager and one class per kind of undo step
│   │   ├── Painting/                Brush strokes, flood fill, layer compositing, selection copy/clear
│   │   ├── Transforms/              Crop, flip, rotate, resize (resampling)
│   │   ├── Effects/                 Effect base classes + EffectApplier (selection masking)
│   │   │   ├── Adjustments/         Brightness/Contrast, Hue/Saturation, Grayscale, Invert, Sepia
│   │   │   ├── Filters/             Blur, Sharpen, Noise, Pixelate
│   │   │   └── Background/          Remove Background
│   │   └── IO/                      File formats: decoder, encoder, ICO writer, exporter (quality + size)
│   │
│   ├── FlashEditor.AI/              AI features (uses ONNX Runtime). Kept out of Core on purpose.
│   │   ├── Models/                  ModelInfo (which model, where from), ModelStore (download once, verify, find on disk)
│   │   ├── Upscaling/               AiUpscaler: Real-ESRGAN in overlapping tiles, blended without seams
│   │   ├── Segmentation/            SubjectSegmenter (runs the model, caches the result), MaskOps (pre/post-processing math)
│   │   └── Effects/                 AiBackgroundRemovalEffect (plugs into the normal effect dialog + live preview)
│   │
│   └── FlashEditor.App/             The Windows app (WPF)
│       ├── App.xaml                 Startup + theme (Fluent dark)
│       ├── Themes/                  Colors, icons (vector paths), converters, control styles, text box style
│       ├── Converters/              Small XAML value converters
│       ├── Rendering/               PixelBuffer <-> WPF bitmaps, rasterizer, ObjectRenderer + shape outlines, checkerboard
│       ├── Services/                Dialogs, clipboard, live effect preview
│       ├── ViewModels/              Screen state (MVVM)
│       │   ├── Commands/            One class per menu: File, Edit, Image, Layer, Effects, View
│       │   └── Infrastructure/      RelayCommand
│       ├── Tools/                   One class per tool + shared tool interfaces
│       │   ├── Options/             Tool settings shown in the options bar
│       │   ├── Painting/            Brush, Eraser, Bucket, Eyedropper
│       │   ├── Selecting/           Select/Move (+ PixelMover, TransformHandles), rectangle/ellipse select,
│       │   │                        lasso, polygon lasso, pen (curves), magic wand, selection add/subtract
│       │   ├── Shapes/              Shapes tool (creates editable shape objects)
│       │   ├── Text/                Text tool
│       │   ├── Transform/           Crop tool
│       │   └── Navigation/          Hand, Zoom
│       └── Views/
│           ├── MainWindow.xaml      Menu, layout, keyboard shortcuts
│           ├── Editor/              CanvasView: zoom, pan, overlays, mouse to tool, handle box, right-click menu
│           ├── Controls/            Reusable controls: NumberSlider (slider + number box), ColorGraph (color picker graph)
│           ├── Panels/              Toolbox, options bar, Properties panel, Layers panel, History panel
│           └── Dialogs/             New image, image size, canvas size, export, recolor, effect settings, text, color picker
│               └── Controls/        SizeEditor (linked width / height / percent), shared by Image Size and Export
└── tests/
    └── FlashEditor.Core.Tests/      xUnit tests for the engine (pixels, history, selections,
                                     transforms, effects, painting, file formats) and the AI helpers.
                                     The real-model test runs only when the model is installed.
```

### How the pieces fit together (architecture)

* **Core vs App.** All image processing lives in `FlashEditor.Core`, which knows nothing about windows or buttons. That makes it easy to test, and the UI stays thin.
* **MVVM.** Views (XAML) bind to view models. View models call into the Core and never create windows themselves; they ask `IDialogService`.
* **Pixels.** A `PixelBuffer` is a plain BGRA byte array. Each `Layer` owns one. `LayerViewModel` copies only the changed rectangle into a WPF `WriteableBitmap`, so painting stays fast.
* **Pixels outside the canvas.** `Layer.Pixels` is always exactly canvas-sized, so tools and effects never deal with offsets. When something is moved past the edge, the layer also keeps a larger hidden buffer (`LayerState`) that is kept in sync with every edit. The Move tool, Canvas Size and Reveal All use it.
* **Undo.** Every change pushes one `IHistoryAction` *after* it is applied:
  * `PixelChangeAction` stores only the changed rectangle (before and after), for brush strokes, fills and effects.
  * `LayerStructureAction` handles adding, deleting and reordering layers.
  * `LayerPropertyAction` handles opacity, visibility and name (slider drags merge into one step).
  * `CanvasChangeAction` handles crop, resize, rotate, flip and canvas size (it swaps whole layer states).
  * `LayerContentAction` handles moves (swaps one layer's state, including hidden pixels).
* **Editable objects.** A layer can hold a `LayerObject` (shape, text or image). Its pixels are then just a drawing of the object, made by `ObjectRenderer` in the app (the engine only knows the `IObjectRenderer` interface). Editing an object makes a new immutable object; `ObjectChangeAction` stores only the small before/after descriptions, so undo is cheap, and repeated slider changes merge into one step. Anything that paints pixels first converts the object (undoable). Crop, canvas size, resize, rotate and flip move objects along instead of flattening them.
* **Tools.** Each tool is a `ToolBase` subclass that gets mouse and key events from `CanvasView` and talks to the rest of the app through `IToolHost`. To add a tool, write one class and add it to `ToolsViewModel`.
* **Effects.** Each effect is an `ImageEffect` that lists its `EffectParameter`s. The settings dialog, live preview, selection masking and undo are handled generically. To add a filter, write one class and add one line to `EffectCommands` and the menu.

## Notes and limits

* Crop keeps cut-off pixels on their layers (Image > Reveal All brings them back); resize and rotate drop pixels hidden outside the canvas (undo brings them back). Use Reveal All first if you want to keep them.
* Saving **merges all visible layers** into one image, since PNG, JPG, WEBP and ICO cannot store layers. There is no layered project format yet.
* AI runs on the GPU through DirectML (ONNX Runtime DirectML build) and falls back to the CPU automatically. The first GPU run of a model takes a few extra seconds while it is prepared.
* AI models live in a `models` folder next to `FlashEditor.exe` (or `%LOCALAPPDATA%\FlashEditor\models`): `isnet-general-use.onnx` (background removal) and `real_esrgan_x4plus/` (upscaling: `.onnx` + `.data`). Each is verified (size + SHA-256) after download.
* **AI Remove Background** keeps the *main* subject. With no clear subject (a landscape) it may keep everything. The app looks for the model file in a `models` folder next to `FlashEditor.exe` first, then in `%LOCALAPPDATA%\FlashEditor\models` (where its own download goes). When publishing or copying the app elsewhere, copy the `models` folder along to avoid a new download. The model is IS-Net general use (Apache 2.0 license, from the rembg project). Delete it to free 170 MB, the app offers to download it again when needed. On a typical PC the first run takes a few seconds; moving the sliders afterwards is instant.
* **Remove Background by Color** is not AI. It handles plain, gradient, shadowed or slightly noisy backgrounds (studio, wall, sky, product shots, logos), also when the subject touches an edge of the picture. It cannot separate a subject from a busy background (a room, trees, a crowd); there, use the Lasso + Delete or the Eraser. If almost nothing is removed, the app says why.
* JPG has no transparency. Transparent areas are filled with the **background color** when saving as JPG.
* The selection outline is not part of undo history (as in many editors). Pixel changes always are.
