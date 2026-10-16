# Supported File Formats in myView

`myView` supports a broad spectrum of image formats, ranging from standard web images to vector art and uncompressed camera RAW digital negatives.

---

## 1. Standard Raster Formats

| Format | Extensions | Engine | Hardware Acceleration |
| :--- | :--- | :--- | :--- |
| **JPEG** | `.jpg`, `.jpeg`, `.jpe`, `.jfif` | Windows WIC | ✅ Direct3D / DirectX |
| **PNG** | `.png` | Windows WIC | ✅ Direct3D / DirectX |
| **Bitmap** | `.bmp`, `.dib` | Windows WIC | ✅ Direct3D / DirectX |
| **GIF** | `.gif` | Windows WIC | ✅ Direct3D / DirectX |
| **WebP** | `.webp` | Windows WIC / Magick | ✅ Direct3D / DirectX |
| **TIFF** | `.tif`, `.tiff` | Windows WIC / Magick | ✅ Direct3D / DirectX |
| **Icon** | `.ico` | Windows WIC | ✅ Direct3D / DirectX |

---

## 2. Vector Graphics (Inkscape / Illustrator)

| Format | Extensions | Engine | Capabilities |
| :--- | :--- | :--- | :--- |
| **SVG** | `.svg` | SharpVectors (WPF Direct2D) | Infinite resolution zoom, scalable geometry paths, gradients, filters |
| **Compressed SVG** | `.svgz` | SharpVectors / Magick | GZip decompressed vector rendering |

---

## 3. Adobe Photoshop Files

| Format | Extensions | Engine | Capabilities |
| :--- | :--- | :--- | :--- |
| **Photoshop Document** | `.psd` | Magick.NET | Composite layers render, 8/16/32-bit channels |
| **Photoshop Big** | `.psb` | Magick.NET | Large document format (>2GB) support |

---

## 4. Digital Camera RAW Formats

`myView` performs full sensor demosaicing via LibRaw core, ensuring accurate dynamic range and color rendition:

| Camera Manufacturer | File Extensions | Supported Models |
| :--- | :--- | :--- |
| **Canon** | `.cr2`, `.cr3` | EOS R-series, 5D, 1D, Rebel, PowerShot |
| **Nikon** | `.nef`, `.nrw` | Z-series (Z9, Z8, Z6, Z5), D-series (D850, D750, etc.), Coolpix |
| **Sony** | `.arw`, `.srf`, `.sr2` | Alpha mirrorless (A1, A9, A7R, A7S, A7 IV, A6700), RX100 |
| **Adobe / Leica / Generic** | `.dng` | Leica M/SL/Q, Hasselblad, DJI Drones, Apple ProRAW, Android RAW |
| **Olympus / OM System** | `.orf` | OM-1, OM-5, E-M1, E-M5, E-M10 |
| **Panasonic Lumix** | `.rw2` | S-series (S5, S1), G-series (GH6, G9) |
| **Pentax / Ricoh** | `.pef`, `.ptx` | K-series (K-1, K-3), Ricoh GR III/IIIx |
| **Fujifilm** | `.raf` | X-Trans sensor series (X-T5, X-H2, X100V/VI), GFX Medium Format |
| **Sigma** | `.x3f` | Foveon sensor series |
| **Samsung** | `.srw` | NX-series mirrorless |
