import os
from PIL import Image, ImageDraw

os.makedirs('SampleImages', exist_ok=True)

# 1. Sample PNG
img_png = Image.new('RGBA', (800, 600), (0, 0, 0, 0))
d = ImageDraw.Draw(img_png)
d.rectangle([50, 50, 750, 550], fill=(20, 30, 50, 200), outline=(0, 210, 255, 255), width=4)
d.text((100, 100), 'myView - PNG Test Image', fill=(255, 255, 255, 255))
d.ellipse([300, 200, 500, 400], fill=(255, 100, 50, 220))
img_png.save('SampleImages/test1_shapes.png')

# 2. Sample BMP
img_bmp = Image.new('RGB', (640, 480), (240, 240, 245))
d = ImageDraw.Draw(img_bmp)
d.rectangle([20, 20, 620, 460], outline=(100, 100, 100), width=3)
d.text((50, 50), 'myView - BMP Standard Test', fill=(0, 0, 0))
img_bmp.save('SampleImages/test2_bitmap.bmp')

# 3. Sample JPG with simulated EXIF
img_jpg = Image.new('RGB', (1920, 1080), (15, 25, 40))
d = ImageDraw.Draw(img_jpg)
d.text((100, 100), 'myView - Full HD Landscape JPEG', fill=(255, 255, 255))
exif = img_jpg.getexif()
exif[0x010F] = 'Sony'
exif[0x0110] = 'ILCE-7M4'
exif[0x0131] = 'myView Camera v1.0'
exif[0x0132] = '2026:10:16 20:30:00'
img_jpg.save('SampleImages/test3_landscape.jpg', exif=exif)

# 4. Sample SVG (Inkscape vector graphic)
svg_content = """<?xml version="1.0" encoding="UTF-8" standalone="no"?>
<svg xmlns="http://www.w3.org/2000/svg" width="800" height="600" viewBox="0 0 800 600">
  <defs>
    <linearGradient id="bgGrad" x1="0%" y1="0%" x2="100%" y2="100%">
      <stop offset="0%" style="stop-color:#0f172a;stop-opacity:1" />
      <stop offset="100%" style="stop-color:#1e293b;stop-opacity:1" />
    </linearGradient>
    <linearGradient id="cyanGrad" x1="0%" y1="0%" x2="100%" y2="100%">
      <stop offset="0%" style="stop-color:#00d2ff;stop-opacity:1" />
      <stop offset="100%" style="stop-color:#3a7bd5;stop-opacity:1" />
    </linearGradient>
  </defs>
  <rect width="800" height="600" rx="24" fill="url(#bgGrad)" stroke="#334155" stroke-width="4"/>
  <circle cx="400" cy="300" r="160" fill="url(#cyanGrad)" opacity="0.85" />
  <polygon points="400,160 520,380 280,380" fill="#f59e0b" opacity="0.75" />
  <text x="400" y="310" font-family="Segoe UI, sans-serif" font-size="32" font-weight="bold" fill="#ffffff" text-anchor="middle">Inkscape SVG Vector</text>
  <text x="400" y="350" font-family="Segoe UI, sans-serif" font-size="18" fill="#94a3b8" text-anchor="middle">Infinite Zoom without Pixelation</text>
</svg>"""
with open('SampleImages/test4_inkscape.svg', 'w', encoding='utf-8') as f:
    f.write(svg_content)

print('Sample PNG, BMP, JPG, and SVG generated successfully!')
