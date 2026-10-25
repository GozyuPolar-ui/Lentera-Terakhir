# Lentera Terakhir

> Kabut menelan dunia. Hanya tempat yang cahayanya menyala yang selamat.

**Lentera Terakhir** adalah game JRPG dark-fantasy singkat (target 60 sampai 90 menit) yang dibuat dengan Unity. Kamu bermain sebagai murid penjaga lentera yang terbangun sendirian di hutan berkabut, dengan sebuah lentera yang hampir padam di sampingnya, dan tidak ingat bagaimana dia sampai di sana.

**Status:** dalam pengembangan aktif. Progres saat ini: prolog hutan selesai, main menu sudah dibangun (belum diuji penuh), dan pengerjaan sekarang ada di **Desa Hearth**.

## Premis

Dulu ada kerajaan Aurelia, diterangi Lentera Agung dan tiga menara suar di perbatasannya. Kerajaan itu lenyap, dan orang-orang tidak ingat bagaimana. Kini kabut menelan tempat demi tempat: bukan dihancurkan, tapi dilupakan.

Nyalakan kembali tiga menara suar, kumpulkan kepingan ingatanmu, dan cari tahu apa sebenarnya kabut itu.

## Gambaran gameplay

- **Sudut pandang orang pertama (FPP)** untuk eksplorasi, dengan suasana sinematik: kabut, cahaya lentera, dan atmosfer gelap yang hangat.
- **Cahaya sebagai mekanik inti.** Lentera pemain adalah sumber cahaya sekaligus sumber daya. Gauge Cahaya dipakai untuk melawan makhluk kabut.
- **Tanpa random encounter dan tanpa grinding.** Musuh terlihat di peta, level dan stat dibuat tetap.
- **Battle turn-based** dengan empat perintah: Serang, Skill, Item, Jaga.
- **Tiga menara suar** dengan mekanik berbeda: puzzle cahaya, area gelap dengan jarak pandang terbatas, dan gabungan keduanya.
- **Dua ending** yang dibedakan oleh satu pilihan di akhir cerita.

## Progres

| Bagian | Status |
|---|---|
| Prolog hutan (cutscene bangun, jalan menuju Hearth) | Selesai |
| Generator hutan dan latar gunung berlapis | Selesai |
| Main menu bergaya atmosferik | Dibangun, belum diuji penuh |
| Layar loading | Direncanakan |
| Desa Hearth (rumah, jalan, rumput, NPC) | **Sedang dikerjakan** |
| Sistem interaksi dan dialog | Sebagian (dasar sudah ada) |
| Battle turn-based dan Gauge Cahaya | Belum |
| Tiga menara suar, tiga boss | Belum |
| Save dan load, audio, polish | Belum |

## Tech stack

- **Unity 6** (6000.5.0f1), Universal Render Pipeline (URP)
- C# dengan TextMeshPro dan uGUI
- Rencana: Yarn Spinner (dialog), DOTween (animasi UI dan battle)

## Tool editor buatan sendiri

Konten dunia dibuat dengan tool editor khusus, bukan disusun manual satu-satu. Semuanya ada di menu **Tools > Lentera**:

| Tool | Fungsi |
|---|---|
| Forest Generator | Membangun hutan padat lengkap dengan jalan berkelok, pohon, semak, pencahayaan, awan, latar gunung berlapis, dan pemain |
| House Builder | Menyusun rumah modular (dinding, lantai, atap) dengan ukuran dan jumlah lantai yang bisa diatur, dan menyimpannya sebagai prefab |
| Village Builder | Menebar rumah di kedua sisi jalan, menghadap ke jalan, dengan props, lewat seed yang bisa diulang |
| Main Menu Scene Builder | Membangun scene main menu dengan kamera bernapas, lentera berkedip, kabut, dan post-processing |

## Struktur proyek

```
Assets/
  Art/                  Model, karakter, dan aset pihak ketiga
  Audio/                Musik dan SFX
  _Project/
    Scenes/             MainMenu, ForestPembuka, ...
    Scripts/
      Core/             Pemain dan sistem inti
      Rendering/        Kamera dan efek visual
      Editor/           Tool editor (Tools > Lentera)
      UI/               Menu, loading, dialog
    Materials/  Meshes/  Prefabs/
```

## Cara menjalankan

1. Buka proyek dengan **Unity 6000.5.0f1** (atau Unity 6 yang kompatibel).
2. Biarkan Unity selesai meng-import dan compile.
3. Buka `Assets/_Project/Scenes/MainMenu.unity` (atau `ForestPembuka.unity` untuk langsung ke prolog), lalu tekan **Play**.

Kontrol: **WASD** atau stick kiri gamepad untuk bergerak. Tombol interaksi (E) menyusul bersama sistem dialog.

## Aset dan kredit

Game ini memakai aset pihak ketiga. Lisensi dicek di halaman masing-masing; tabel ini hanya mencatat apa yang dipakai.

| Aset | Pembuat | Dipakai untuk | Lisensi |
|---|---|---|---|
| Stylized Nature MegaKit | Quaternius | Pohon, semak, tanaman | CC0 |
| Medieval Village MegaKit | Quaternius | Bangunan desa | Cek halaman paket |
| Ultimate Modular Ruins Pack | Quaternius | Reruntuhan | Cek halaman paket |
| KayKit Adventurers 2.0 (Free) | Kay Lousberg | Karakter dan animasi dasar | Cek halaman paket |
| Universal Base Characters | Quaternius | Badan karakter NPC (rencana) | CC0 |
| Modular Character Outfits, Fantasy | Quaternius | Pakaian NPC (rencana) | CC0 |
| Universal Animation Library | Quaternius | Animasi karakter (rencana) | Cek halaman paket |
| Fantasy UI Borders | Kenney | UI menu (rencana) | Cek halaman paket |
| Musik hutan (`forestbgm.ogg`) | Belum dicatat | Latar musik hutan | Perlu dicatat |

Kredit dan lisensi tiap aset akan dilengkapi di berkas `CREDITS.md` sebelum rilis.

## Roadmap

- [x] Prolog hutan dan generator hutan
- [x] Main menu (dibangun)
- [ ] Layar loading
- [ ] Desa Hearth lengkap dengan NPC, dialog, dan interaksi
- [ ] Menara 1: Hutan Berkabut
- [ ] Menara 2: Tambang Tua
- [ ] Menara 3: Rawa dan Reruntuhan
- [ ] Jantung Kabut dan dua ending
- [ ] Battle turn-based dengan Gauge Cahaya
- [ ] Save dan load, audio, polish, build

## Pembuat

Dibuat oleh **Kudo Raymond** ([@GozyuPolar-ui](https://github.com/GozyuPolar-ui)).

## Lisensi

Kode: hak cipta penulis, lisensi akan ditentukan. Aset pihak ketiga mengikuti lisensi masing-masing (lihat bagian Aset dan kredit).
