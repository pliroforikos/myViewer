# myView ⚡
**Ultra-Fast, Lightweight Image & Vector Viewer for Windows**

<p align="center">
  <img src="app_icon_256.png" width="128" height="128" alt="myView Logo" />
  <br><br>
  <img src="https://img.shields.io/badge/.NET-10.0-512BD4?style=flat-square&logo=dotnet" alt=".NET 10" />
  <img src="https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-0078D6?style=flat-square&logo=windows" alt="Windows" />
  <img src="https://img.shields.io/badge/Speed-%3C5ms%20Decoding-10B981?style=flat-square" alt="Decode Speed" />
  <img src="https://img.shields.io/badge/License-CC%20BY--NC%204.0-orange?style=flat-square" alt="CC BY-NC 4.0 License" />
  <br><br>
  <b>myView</b> - Σχεδιασμένο για αστραπιαία ταχύτητα, ελάχιστη κατανάλωση πόρων και υποστήριξη επαγγελματικών format.
</p>

---

## 📚 Αναλυτική Τεκμηρίωση (Documentation)

- 🏗️ **[Αρχιτεκτονική & Pipeline (Architecture)](docs/ARCHITECTURE.md)**: Ανάλυση του υβριδικού συστήματος αποκωδικοποίησης (WIC, LibRaw, SharpVectors), LRU cache και μαθηματικών zoom.
- 🎨 **[Υποστηριζόμενα Formats (Supported Formats)](docs/SUPPORTED_FORMATS.md)**: Πλήρης λίστα όλων των υποστηριζόμενων camera RAW, raster, Photoshop και διανυσματικών αρχείων.
- ⌨️ **[Συντομεύσεις Πληκτρολογίου & Ποντικιού (Shortcuts)](docs/KEYBOARD_SHORTCUTS.md)**: Πλήρης οδηγός χειρισμού και gestures.
- 📄 **[Άδεια Χρήσης (License)](LICENSE)**: Creative Commons Attribution-NonCommercial 4.0 (CC BY-NC 4.0).

---

## 🚀 Χαρακτηριστικά & Λειτουργίες (Features)

1. **Ασύλληπτη Ταχύτητα & Ελαφρύ Μέγεθος:**
   - Ταχύτατη αποκωδικοποίηση μέσω GPU Hardware Acceleration (DirectX/WIC) για standard εικόνες (~2-5ms).
   - Ασύγχρονο πολυνηματικό decoding (Task-based) που δεν μπλοκάρει ποτέ το UI.
   - **Background Pre-caching:** Αυτόματη προφόρτωση επόμενης και προηγούμενης εικόνας στο παρασκήνιο (LRU cache) ώστε η εναλλαγή με βελάκια να είναι απολύτως στιγμιαία (0ms latency).

2. **Υποστηριζόμενα Αρχεία (Supported Formats):**
   - **Standard Raster:** JPG, JPEG, PNG, BMP, GIF, WebP, TIFF, ICO.
   - **Vector / Inkscape:** SVG, SVGZ (άπειρο ζουμ χωρίς pixelation μέσω vector engine).
   - **Adobe Photoshop:** PSD, PSB (πλήρης υποστήριξη composite στρωμάτων).
   - **Camera RAW:** Πλήρης επεξεργασία αισθητήρα (Full RAW demosaicing) για CR2, CR3 (Canon), NEF, NRW (Nikon), ARW, SRF, SR2 (Sony), DNG (Adobe/Leica/Mobile), ORF (Olympus), RW2 (Panasonic), PEF, PTX (Pentax), RAF (Fuji), X3F (Sigma), SRW (Samsung).

3. **Πλοήγηση & Ταξινόμηση (Navigation & Sorting):**
   - **Βελάκι Δεξιά / Αριστερά (Right / Left Arrow):** Επόμενη / Προηγούμενη εικόνα.
   - **Home:** Πρώτη εικόνα του τρέχοντος φακέλου.
   - **End:** Τελευταία εικόνα του φακέλου.
   - **Ταξινόμηση (Sort):** Αλφαβητικά (A-Z, Z-A με natural numeric sorting: π.χ. `img2` πριν το `img10`), ανά Ημερομηνία τροποποίησης (Νεότερα/Παλαιότερα) και ανά Μέγεθος αρχείου. Πλήκτρο `S` ή κλικ στο badge ταξινόμησης.

4. **Έλεγχος Μεγέθυνσης & Προβολής (Pan & Zoom):**
   - **Ροδέλα Ποντικιού (Mouse Wheel):** Ομαλό Zoom In & Out εστιασμένο ακριβώς στη θέση του κέρσορα.
   - **Σύρσιμο (Click & Drag):** Ελεύθερη μετακίνηση (Pan) όταν η εικόνα είναι σε μεγέθυνση.
   - **Fit Screen (F ή 0):** Αυτόματη προσαρμογή στο παράθυρο με διατήρηση των αναλογιών.
   - **Native Size 100% 1:1 (1):** Προβολή σε πραγματικές διαστάσεις pixel.
   - **Double-click:** Εναλλαγή μεταξύ Fit και 100%.
   - **Περιστροφή & Αναστροφή:** Rotate 90° Clockwise (`R`), Counter-Clockwise (`L`), Flip Horizontal (`H`).

5. **Διαδραστική Περικοπή & Αποθήκευση (Interactive Crop & Save - `C`):**
   - Ενεργοποίηση με το πλήκτρο `C` ή το κουμπί περικοπής στην κάτω μπάρα εργαλείων.
   - 8 λαβές μεγέθυνσης/σμίκρυνσης και δυνατότητα ελεύθερης μετακίνησης του πλαισίου με πλέγμα κανόνα των τρίτων (rule-of-thirds grid).
   - Live ένδειξη διαστάσεων σε πραγματικά pixels (`W × H px`).
   - Πλήρης υποστήριξη JPG, PNG, GIF, WebP και BMP.
   - **Αποθήκευση ως... (Save As):** Επιλογή νέου ονόματος/διαδρομής/τύπου αρχείου (`Enter`), με αυτόματη προσθήκη στη λίστα του φακέλου και άμεση πλοήγηση σε αυτό.
   - **Αντικατάσταση (Overwrite):** Ασφαλής ατομική αντικατάσταση του αρχικού αρχείου (atomic replacement via temp file) και αυτόματη ανανέωση της cache.
   - Αυτόματη κατοχύρωση (baking) τυχόν περιστροφής/αναστροφής και διατήρηση χρωματικών προφίλ (ICC profiles) & EXIF metadata.
   - **Μηδενική επίπτωση στην ταχύτητα:** Η μηχανή crop φορτώνεται αποκλειστικά on-demand χωρίς να επηρεάζει στο ελάχιστο την αστραπιαία ταχύτητα προβολής (~2-5ms).

6. **Ευθυγράμμιση Ορίζοντα & Μικρο-περιστροφή (Horizon Straightening & Deskew - `E`):**
   - Ιδεατός εικονικός ορίζοντας με δυνατότητα σχεδίασης γραμμής αναφοράς πάνω σε οποιαδήποτε κεκλιμένη γραμμή κειμένου, ορίζοντα θάλασσας ή άκρη αντικειμένου.
   - Αυτόματος υπολογισμός της γωνίας κλίσης και άμεση περιστροφή ώστε η εικόνα να ευθυγραμμιστεί τέλεια.
   - Slider ακριβείας $\pm 45^\circ$ με βήμα $0.1^\circ$ και κουμπιά μικρο-ρύθμισης $\pm 0.5^\circ$.
   - Πλήρες αρχιτεκτονικό πλέγμα οδηγών (alignment grid) σε ολόκληρη την οθόνη για απόλυτο οπτικό έλεγχο.
   - Αυτόματο Crop (Auto-Crop) μέγιστου εγγεγραμμένου παραλληλογράμμου χωρίς μαύρα/λευκά κενά στις γωνίες.
   - Live περιστροφή σε πραγματικό χρόνο (60 FPS Hardware Accelerated) χωρίς καθυστέρηση.

7. **Διόρθωση Προοπτικής & Keystone Εγγράφων (Keystone Warp & Document Deskew - `P`):**
   - Σχεδιασμένο ειδικά για κακοφωτογραφημένα έγγραφα, διαγωνίσματα, βιβλία, αποδείξεις και σημειώσεις που τραβήχτηκαν υπό γωνία (τραπεζοειδής παραμόρφωση / keystone).
   - **Άνοιγμα Γωνιών (Outward Corner Stretch):** Δυνατότητα σύρσης των πάνω γωνιών προς τα έξω (open outward) για να ισιώσει το κείμενο και να γίνει παραλληλόγραμμο, χωρίς να αλλοιώνονται οι κάτω γωνίες.
   - **Συμμετρική Λειτουργία (Symmetric Mode):** Τραβώντας την πάνω αριστερή γωνία προς τα έξω, ανοίγει αυτόματα και ισόποσα η πάνω δεξιά γωνία με μία μόνο κίνηση του ποντικιού.
   - **Slider Ανοίγματος Κορυφής (Keystone Slider):** Ρύθμιση ανοίγματος/στενέματος σε ποσοστό `%` με κουμπιά βήματος ακριβείας `-` και `+`.
   - **Live Προεπισκόπηση σε Πραγματικό Χρόνο (Real-Time Warp Preview):** Το έγγραφο στην οθόνη μεταμορφώνεται ζωντανά καθώς κινείτε τις λαβές ή το slider!
   - **Αυτόματη Περικοπή Περιθωρίων (Auto-Crop):** Αυτόματος υπολογισμός και περικοπή στα καθαρά όρια του εγγράφου ώστε να μην υπάρχουν κενά περιθώρια.
   - **Διπλή Λειτουργία:** Άμεση εναλλαγή με ένα κλικ μεταξύ «↔️ Άνοιγμα (Keystone Warp)» και «✂️ Quad Crop (Επιλογή Εγγράφου)».
   - Προαιρετική επιλογή **Βελτίωσης Κειμένου (Auto-Enhance)** για αύξηση της αντίθεσης και κρυστάλλινη αναγνωσιμότητα των γραμμάτων.

8. **Αντιγραφή Εικόνας & Επιλογής στο Πρόχειρο (Copy to Clipboard - `Ctrl+C`):**
   - **Άμεση Επικόλληση (`Ctrl+V`):** Αντιγράφει την εικόνα στο Πρόχειρο των Windows με ένα πάτημα πλήκτρου ή κλικ, έτοιμη για επικόλληση σε Microsoft Word, PowerPoint, Paint, Photoshop, Telegram, Discord, Teams, Browsers, WhatsApp, Email κ.λπ.
   - **Πολυμορφικά Δεδομένα (Multi-Format Clipboard Data):**
     - `Bitmap / DIB`: Απόλυτη συμβατότητα με κλασικές εφαρμογές (Office, Paint κ.λπ.).
     - `PNG Stream (32-bit ARGB)`: Διατηρεί πλήρη διαφάνεια (Alpha channel / Transparency) για PNG, WebP, SVG σε σύγχρονες εφαρμογές (Discord, Telegram, Web browsers).
     - `FileDrop`: Κατά την επικόλληση στην Εξερεύνηση των Windows (Explorer), δημιουργεί απευθείας αντίγραφο του αρχείου.
   - **Αντιγραφή Επιλεγμένης Περικοπής:** Όταν βρίσκεστε σε λειτουργία Περικοπής (`C`), το `Ctrl+C` αντιγράφει απευθείας **μόνο το επιλεγμένο τμήμα** (cropped selection) χωρίς να χρειάζεται αποθήκευση σε αρχείο!
   - **Αντιγραφή Διαδρομής Αρχείου (`Ctrl+Shift+C`):** Αντιγράφει την πλήρη διαδρομή (path) του τρέχοντος αρχείου στο πρόχειρο.
   - **Toast Notifications:** Διακριτική οπτική ειδοποίηση επιβεβαίωσης κατά την αντιγραφή.

9. **Σύγχρονο Μενού Δεξιού Κλικ (Dark Context Menu):**
   - Δεξί κλικ οπουδήποτε πάνω στην εικόνα ανοίγει ένα κομψό, σκοτεινό μενού άμεσων επιλογών:
     - 📋 Αντιγραφή Εικόνας (`Ctrl+C`)
     - 📁 Αντιγραφή Διαδρομής Αρχείου (`Ctrl+Shift+C`)
     - ✂️ Περικοπή (Crop) (`C`)
     - 📐 Ίσιωμα Ορίζοντα (`E`)
     - 📐 Προοπτική & Skew Εγγράφων (`P`)
     - 🔄 Περιστροφή Δεξιά / Αριστερά (`R`, `L`) & Οριζόντια Αναστροφή (`H`)
     - ℹ️ Πληροφορίες EXIF (`I`)
     - ℹ️ Περί myView (`F1` / `A`)

10. **Αναλυτικό Πάνελ EXIF & Metadata (`I`):**
    - Αναδιπλούμενο, ημιδιαφανές πλαϊνό πάνελ χωρίς διακοπή της προβολής.
    - **Camera & Lens:** Κατασκευαστής, μοντέλο μηχανής, φακός.
    - **Exposure Badges:** Ταχύτητα κλείστρου (Shutter), Διάφραγμα (f-number), Ευαισθησία (ISO), Εστιακό μήκος (Focal length), Αντιστάθμιση έκθεσης (Exposure Bias).
    - **Shooting Conditions:** Φλας, White Balance, Φωτομέτρηση (Metering).
    - **GPS Coordinates:** Γεωγραφικό πλάτος/μήκος με απευθείας κουμπί προβολής στο Google Maps.
    - **Αναζήτηση & Πλήρες Δέντρο:** Search box για άμεσο φιλτράρισμα όλων των raw tags.
    - **Αντιγραφή στο Clipboard:** Κουμπί για αντιγραφή όλων των στοιχείων με ένα κλικ.

11. **Μοντέρνο Minimalist Dark UI & Fullscreen (`F11`):**
    - Μέγιστη επιφάνεια προβολής της εικόνας χωρίς περιττά πλαίσια (Frameless Windows 11 Chrome).
    - Floating HUD μπάρα εργαλείων που εξαφανίζεται αυτόματα όταν το ποντίκι είναι αδρανές για 2.5 δευτερόλεπτα και επανέρχεται με την παραμικρή κίνηση.
    - Πλήρης υποστήριξη **Drag & Drop** (σύρετε οποιαδήποτε εικόνα ή φάκελο στο παράθυρο).
    - Υποστήριξη Command-line (π.χ. `myView.exe "C:\Photos\image.jpg"`).
    - Πλήρης οθόνη (**Fullscreen**, `F11`) για παρουσιάσεις και καθαρή προβολή.

12. **Ενσωμάτωση στα Windows & Ορισμός ως Προεπιλογή:**
    - **Ορισμός ως Προεπιλογή (Default Viewer):** Με 1 κλικ μέσα από το παράθυρο «Περί» (`F1` / `A`), η εφαρμογή δηλώνεται στο μητρώο των Windows (`HKCU` - χωρίς απαίτηση δικαιωμάτων διαχειριστή) και ανοίγουν οι ρυθμίσεις προεπιλογών των Windows.
    - **Open With:** Αυτόματη εμφάνιση του myView στο δεξί κλικ «Άνοιγμα με...» για όλα τα υποστηριζόμενα αρχεία.

13. **Αυτοματοποιημένες Δοκιμές Αξιοπιστίας (Automated Self-Test Engine):**
    - 11 ενσωματωμένες σουίτες δοκιμών (`dotnet run -- --test`) που πιστοποιούν την ορθότητα της αποκωδικοποίησης (raster, vector, RAW), του multi-format clipboard, του caching, των γεωμετρικών μετασχηματισμών και των μεταδεδομένων.

---

## ⌨️ Συντομεύσεις Πληκτρολογίου & Ποντικιού (Shortcuts)

| Συντόμευση | Ενέργεια |
| :--- | :--- |
| **`Ctrl + C`** | Αντιγραφή εικόνας ή επιλεγμένης περικοπής στο Clipboard (Copy Image / Crop Selection) |
| **`Ctrl + Shift + C`** | Αντιγραφή πλήρους διαδρομής αρχείου στο Clipboard (Copy File Path) |
| **`Δεξί Κλικ (Right Click)`** | Άνοιγμα μενού επιλογών (Context Menu) |
| **`→` / `Space` / `Page Down`** | Επόμενη εικόνα (Next Image) |
| **`←` / `Backspace` / `Page Up`** | Προηγούμενη εικόνα (Previous Image) |
| **`Home`** | Πρώτη εικόνα φακέλου (First Image) |
| **`End`** | Τελευταία εικόνα φακέλου (Last Image) |
| **`+` / `=` / `Wheel Up`** | Zoom In (Μεγέθυνση στο σημείο του δείκτη) |
| **`-` / `Wheel Down`** | Zoom Out (Σμίκρυνση) |
| **`F` / `0`** | Προσαρμογή στην οθόνη (Fit to Screen) |
| **`1`** | Πραγματικό μέγεθος 100% (1:1 Native Pixel Size) |
| **`Διπλό Κλικ (Double Click)`** | Εναλλαγή μεταξύ Fit και 100% |
| **`C`** | Ενεργοποίηση / Ακύρωση περικοπής (Toggle Crop Mode) |
| **`E`** | Ενεργοποίηση / Ακύρωση ευθυγράμμισης ορίζοντα (Toggle Horizon Straighten) |
| **`P`** | Ενεργοποίηση / Ακύρωση διόρθωσης προοπτικής εγγράφου (Toggle Document Skew / Keystone) |
| **`Enter`** | Αποθήκευση ως νέο αρχείο (στο Crop, Horizon ή Perspective mode) |
| **`R`** | Δεξιόστροφη περιστροφή 90° (Rotate Right) |
| **`L`** | Αριστερόστροφη περιστροφή 90° (Rotate Left) |
| **`H`** | Οριζόντια αναστροφή (Flip Horizontal) |
| **`S`** | Αλλαγή σειράς ταξινόμησης (Όνομα, Ημερομηνία, Μέγεθος) |
| **`I`** | Άνοιγμα / Κλείσιμο πάνελ EXIF & Μεταδεδομένων (Toggle EXIF Sidebar) |
| **`F1` / `A`** | Άνοιγμα / Κλείσιμο παραθύρου Περί (About Dialog & Default Viewer) |
| **`F11`** | Πλήρης οθόνη (Toggle Fullscreen) |
| **`Ctrl + O` / `O`** | Άνοιγμα αρχείου εικόνας (Open File) |
| **`Delete`** | Διαγραφή τρέχουσας εικόνας (με επιβεβαίωση) |
| **`Esc`** | Ακύρωση επεξεργασίας, κλείσιμο διαλόγου Περί, πάνελ EXIF ή έξοδος από Fullscreen |

---

## 👨‍🏫 Περί & Δημιουργός (About & Creator)

<p align="center">
  <img src="avatar.jpg" width="100" height="100" style="border-radius: 50%;" alt="Λέων Προκόπης" />
  <br>
  <b>Λέων Προκόπης</b> (Καθηγητής Πληροφορικής)
  <br>
  🌐 <a href="https://plarg.gr"><b>plarg.gr</b></a> — Εφαρμογές, εργαλεία &amp; σημειώσεις πληροφορικής
</p>

### ⚖️ Άδεια Χρήσης (License)

Το **myView** διατίθεται υπό την άδεια:  
**[Creative Commons Αναφορά Δημιουργού - Μη Εμπορική Χρήση 4.0 Διεθνές (CC BY-NC 4.0)](https://creativecommons.org/licenses/by-nc/4.0/deed.el)**

> **Δωρεάν χρήση και διανομή για εκπαιδευτικούς σκοπούς, με αναφορά στον δημιουργό (Λέων Προκόπης) και χωρίς εμπορική χρήση.**
>
> Είστε ελεύθεροι να μοιραστείτε, να αντιγράψετε και να τροποποιήσετε το έργο υπό τον όρο της αναφοράς του δημιουργού ([plarg.gr](https://plarg.gr)) και αποκλειστικά για μη εμπορικούς σκοπούς.

---

## 📦 Λήψη & Εγκατάσταση (Installation & Download)

Μπορείτε να κατεβάσετε την τελευταία έκδοση από τα **[GitHub Releases](https://github.com/pliroforikos/myViewer/releases)**:

| Αρχείο | Τύπος | Περιγραφή |
| :--- | :--- | :--- |
| **`myView-v1.0.6-setup.exe`** | **Windows Setup Installer** *(Προτεινόμενο)* | Πλήρης εγκαταστάτης (Inno Setup) με οδηγό βήμα-προς-βήμα, συντομεύσεις, απεγκατάσταση και αυτόματη δήλωση προεπιλογής. Δεν απαιτείται .NET. |
| **`myView-v1.0.6-lightweight-setup.exe`** | **Lightweight Setup** (~11 MB) | Ελαφρύς εγκαταστάτης για συστήματα που διαθέτουν ήδη εγκατεστημένο το .NET 10 Runtime. |
| **`myView-v1.0.6-standalone-win-x64.zip`** | **Portable Zip (Standalone)** | Φορητή έκδοση σε zip — απλώς αποσυμπιέστε και τρέξτε το `myView.exe` χωρίς καμία εγκατάσταση. |
| **`myView-v1.0.6-windows-x64.zip`** | **Portable Zip (Lightweight)** | Φορητή έκδοση (~13 MB) για χρήστες με .NET 10. |

---

## 🛠️ Εκτέλεση & Μεταγλώττιση (Build & Run)

Απαιτήσεις: [.NET 10 SDK](https://dotnet.microsoft.com/)

```powershell
# Μεταγλώττιση (Build)
dotnet build

# Εκτέλεση αυτοματοποιημένων δοκιμών (Automated Self-Test)
dotnet run -- --test

# Εκτέλεση εφαρμογής (Run Application)
dotnet run

# Δημιουργία Standalone Release Build
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o dist/myView-standalone-win-x64

# Δημιουργία Setup Installer (απαιτεί Inno Setup 6)
iscc installer\myView_Standalone_Setup.iss
```
