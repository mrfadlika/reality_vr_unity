# Dokumen Handover Proyek — 1Eagle Virtual Reality (Yard Editor)

Dokumen ini berisi panduan singkat mengenai daftar *tools* (beserta versinya) yang wajib diinstal untuk menjalankan *source code*, melakukan *build*, serta mengatur konfigurasi sebelum *deploy*.

---

## 1. Tools & Software yang Harus Diinstal

Untuk membuka, menjalankan, dan mem-build *source code* proyek ini, instal daftar *tools* berikut:

| No | Nama Tool / Software | Versi Wajib / Direkomendasikan | Kegunaan |
| :---: | :--- | :--- | :--- |
| **1** | **Unity Hub** | Versi Terbaru (`v3.x`) | Manajemen instalasi Unity Editor dan pembuka proyek. |
| **2** | **Unity Editor** | **`6000.2.14f1`** *(Unity 6.2)* | Engine utama untuk membuka dan mem-build *source code*. |
| **3** | **Modul Unity: WebGL Build Support** | Sesuai versi Unity (`6000.2.14f1`) | Modul wajib di dalam Unity Hub untuk mengekspor proyek ke Web (HTML5/WebGL). |
| **4** | **IDE / Code Editor** | **Visual Studio 2022** atau **VS Code** | Untuk mengedit script C# (`Assets/Scripts/`) dan file konfigurasi (`data/.env`). |
| **5** | **Git** | `v2.40+` | Untuk *clone*, *pull*, dan *push* repository *source code*. |
| **6** | **Python 3** atau **Node.js** *(Opsional)* | Python `3.10+` / Node.js `v18+` | Hanya digunakan jika ingin menjalankan *local web server* untuk mengetes hasil build WebGL di komputer lokal (`python -m http.server 8080` atau `npx serve`). |
| **7** | **Web Browser Modern** | Google Chrome / Microsoft Edge / Brave | Untuk membuka dan menjalankan hasil build WebGL (pastikan *Hardware Acceleration* aktif). |

---

## 2. Daftar Package Internal Unity (Otomatis Terinstal)

Saat proyek dibuka pertama kali, Unity Package Manager akan otomatis mengunduh *dependencies* berikut dari file `Packages/manifest.json` (tidak perlu diinstal manual):
- **Universal Render Pipeline (URP):** `17.2.0`
- **Unity Input System:** `1.16.0`
- **Unity UI (uGUI / TextMeshPro):** `2.0.0`
- **XR Plugin Management & OpenXR:** `4.7.0` & `1.16.0`
- **De-Panther WebXR Export & Interactions:** `0.25.0` (via OpenUPM registry)

---

## 3. Cara Menjalankan Source Code di Unity Editor

1. **Clone / Download Repository** proyek ini ke komputer.
2. Buka **Unity Hub** -> klik tombol **Add** -> **Add project from disk**, lalu pilih folder root proyek **`Reality Eagle`**.
3. Pastikan versi Editor yang terpilih di Unity Hub adalah **`6000.2.14f1`**, lalu buka proyek.
4. Setelah Unity Editor terbuka, buka *Scene* utama di panel *Project*:
   ```text
   Assets/Scenes/Copy.unity
   ```
   *(Catatan: Gunakan scene `Copy.unity`, jangan gunakan `SampleScene.unity`).*
5. Tekan tombol **Play (▶)** di bagian atas Unity Editor untuk menjalankan aplikasi secara langsung.

---

## 4. Cara Build Proyek ke WebGL

1. Pastikan scene **`Assets/Scenes/Copy.unity`** sedang terbuka.
2. Buka menu **`File -> Build Profiles`** (atau **`File -> Build Settings`**).
3. Pilih platform **WebGL**.
4. Pada daftar **Scene List**, pastikan hanya **`Assets/Scenes/Copy.unity`** yang dicentang.
5. Klik tombol **Build** dan pilih folder tujuan penyimpanan hasil build (contoh: `YardEditor_WEBGL`).
6. Tunggu proses build selesai (~1–3 menit). Setelah selesai, folder `data/` beserta file konfigurasi server akan otomatis tersalin ke dalam folder hasil build.

---

## 5. Cara Mengubah Endpoint API & Layout Sebelum Deploy (Tanpa Build Ulang)

Setiap kali aplikasi akan di-deploy untuk pelabuhan atau proyek yang berbeda, **tidak perlu melakukan build ulang di Unity**. Cukup ubah file di dalam folder **`data/`** pada hasil build:

1. **Mengubah URL API Backend (`data/.env` dan `data/app.properties`):**
   Buka file **`data/.env`** menggunakan Notepad / VS Code, lalu ganti URL sesuai alamat API pelabuhan target:
   ```ini
   API_MASTER_BLOCK=https://domain-api-pelabuhan.co.id/api/vr/getMasterBlock
   API_CONTAINER_LOC=https://domain-api-pelabuhan.co.id/api/vr/getCtrLoc
   ```
2. **Mengubah Tata Letak Default Lapangan (`data/denah.json`):**
   File **`data/denah.json`** menyimpan koordinat posisi *Yard Blocks*, *Roads*, *Walls*, dan *Accessories*.

---

## 6. Cara Menjalankan Hasil Build WebGL

Hasil build WebGL (`index.html`) tidak bisa dibuka dengan klik dua kali langsung (`file://`), melainkan harus dijalankan melalui *Web Server*:

- **Menjalankan di Lokal (Testing PC):**
  Buka terminal / PowerShell di dalam folder hasil build (`YardEditor_WEBGL`), lalu jalankan:
  ```bash
  python -m http.server 8080
  ```
  Kemudian buka browser dan akses: `http://localhost:8080`

- **Deploy ke Hosting / Server Produksi:**
  Unggah seluruh isi folder hasil build (`YardEditor_WEBGL`) ke layanan hosting seperti **Cloudflare Pages**, **Netlify**, **Vercel**, atau web server internal (**Apache / Nginx / IIS**).
