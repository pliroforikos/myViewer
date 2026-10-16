# Architectural Overview: myView

`myView` is an ultra-fast, lightweight image viewer built with modern C# (.NET 10) and Windows Presentation Foundation (WPF), leveraging hardware acceleration and asynchronous background pipelines to provide instant loading and smooth navigation.

---

## 1. High-Performance Hybrid Decoding Pipeline

To achieve instant open times while supporting complex professional formats, `myView` uses a three-tier hybrid decoding strategy:

```
                      [ File Path ]
                            │
               Is it Vector (.svg, .svgz)?
                 ├── YES ──► SharpVectors Engine ──► DrawingImage (Infinite Zoom)
                 └── NO
                      │
               Is it Standard WIC (.jpg, .png, .bmp, .webp, .gif)?
                 ├── YES ──► Windows Imaging Component (GPU) ──► BitmapSource (~2-5ms)
                 └── NO
                      │
               RAW Camera (.cr2, .nef, .arw, .dng) / Photoshop (.psd, .psb)
                 └─────────► Magick.NET (LibRaw Full Demosaic) ──► Bgra32 BitmapSource
```

### Tier 1: Windows Imaging Component (WIC) Fast Path
- Formats: JPEG, PNG, BMP, GIF, WebP, TIFF, ICO.
- Speed: typically **2 to 5 milliseconds** on modern NVMe/SSD storage.
- Operates directly through Windows hardware-accelerated codecs into WPF's DirectX rendering pipeline.
- Automatically inspects EXIF orientation tags (`0x0112`) and applies lossless rotation transforms (`TransformedBitmap`).
- Bitmaps are frozen (`Freeze()`) immediately upon decoding to allow safe cross-thread memory sharing and zero GPU recreation overhead.

### Tier 2: Vector Direct2D Engine
- Formats: SVG, SVGZ (Inkscape, Illustrator, Figma exports).
- Powered by `SharpVectors.Converters.Wpf`.
- Generates native WPF vector geometries (`DrawingGroup` / `DrawingImage`), providing infinite resolution zoom without pixelation or memory bloat.

### Tier 3: Magick.NET Full RAW & PSD Engine
- Formats: Canon CR2/CR3, Nikon NEF/NRW, Sony ARW/SRF/SR2, Adobe DNG, Olympus ORF, Panasonic RW2, Pentax PEF, Fuji RAF, Sigma X3F, Photoshop PSD/PSB.
- Full sensor demosaicing via LibRaw core.
- Decodes into raw 32-bit BGRA byte arrays and constructs frozen `BitmapSource` instances.

---

## 2. In-Memory LRU Cache & Asynchronous Pre-Caching

Navigating through images in a folder often feels sluggish when each file is decoded on demand. `myView` solves this via `ImageCacheService`:

1. **Thread-Safe LRU Cache:** Maintains the last 12 decoded images in memory.
2. **Neighbor Prefetching:** Whenever the user displays image index `i`:
   - A background thread automatically prefetches images `i+1`, `i+2`, `i-1`, and `i-2`.
   - When the user presses `Right Arrow` or `Left Arrow`, the target image is already in memory and renders with **0 ms latency**.
3. **Cooperative Cancellation:** If the user rapidly holds down an arrow key, ongoing background pre-fetches are immediately canceled via `CancellationTokenSource`, preventing thread pool starvation and memory churn.

---

## 3. Viewport Geometry & Pan/Zoom Transformations

The viewport uses a GPU-accelerated WPF `Canvas` with an `Image` element governed by a composite `TransformGroup`:

```
TransformGroup
├── ScaleTransform     (ScaleX, ScaleY) -> Centered at (PixelWidth / 2, PixelHeight / 2)
├── RotateTransform    (Angle: 0, 90, 180, 270)
└── TranslateTransform (X, Y)
```

### Cursor-Centered Zooming Math
When scrolling the mouse wheel at cursor coordinate $P = (x_m, y_m)$ in viewport space:

$$\text{scaleRatio} = \frac{S_{\text{new}}}{S_{\text{old}}}$$
$$X_{\text{new}} = x_m - (x_m - X_{\text{old}}) \times \text{scaleRatio}$$
$$Y_{\text{new}} = y_m - (y_m - Y_{\text{old}}) \times \text{scaleRatio}$$

This guarantees that the point directly underneath the user's cursor remains stationary on the screen during magnification.

---

## 4. Metadata & EXIF Extraction Architecture

The metadata engine uses `MetadataExtractor`:
- Fast binary header scanning without loading full pixel buffers.
- Extracts camera manufacturer, body model, lens model, shutter speed, aperture, ISO, focal length, shooting mode, flash, and date.
- Parses GPS coordinates, formatting them to DMS (Degrees, Minutes, Seconds) and generating direct Google Maps deep-links.
- Organizes all raw metadata into categorized directories with live search filtering.
