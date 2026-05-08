
# Stock Control (ASP.NET Core)

Bu proje stok ve katalog yonetimi icin gelistirilmis bir web uygulamasidir (ASP.NET Core 8, MVC).

**Gereksinim:** [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) ve `Microsoft.AspNetCore.App` 8.x runtime.

## Onceki surume gore eklenenler

- Modern ve mobil uyumlu site tasarimi (yenilenmis layout, responsive menu, kart yapisi)
- Aktivite loglari (siparisler, stok hareketleri, urun/kategori islemleri, yetki degisiklikleri)
- Admin bildirimleri (dusuk stok ve acik stok talepleri)
- Katalog + sepet + checkout akisi
- Uye kayit olma ekrani (kullanici adi/sifre ile kayit)
- Yetki paneli:
  - uye arama
  - uye olusturma
  - yetki verme/kaldirma
  - tum yetkileri tek tikla verme/kaldirma
  - sifre sifirlama
  - uye silme
- `admin123` hesabi patron hesabi olarak korunur (silinemez, sifresi/yetkisi panelden degistirilemez)

## Temel ozellikler

- Login sistemi (Admin/User)
- Kategori bazli katalog listeleme
- Urun ekleme, duzenleme, silme
- Urun gorseli yukleme
- Stok giris/cikis islemleri
- Hareket gecmisi
- SQLite veritabani baglantisi

## Teknik Kararlar

- Neden SQLite?
  - Kurulum maliyeti dusuk ve sifir ek servis gerektirir.
  - Tek dosya veritabani yapisi sayesinde yerel gelistirme ve demo sureci hizli ilerler.
  - Entity Framework Core ile kolay entegre olur ve daha sonra SQL Server/PostgreSQL'e gecis icin uygun bir baslangic sunar.

- Neden role-based (Admin/User) yetkilendirme?
  - Stok giris/cikis, urun duzenleme ve kategori yonetimi gibi kritik islemler sadece Admin tarafinda sinirlanir.
  - User rolunun katalog/urun inceleme ile sinirli kalmasi, yetki hatalarini ve veri guvenligi riskini azaltir.
  - Isletme tarafinda sorumluluk ayrimini netlestirir ve panel kullanimini sadelestirir.

## Calistirma

```bash
dotnet restore
dotnet run
```

Uygulama acildiginda varsayilan olarak login sayfasina yonlenir.

## Test kullanicilari

- Admin: admin123 / admin123
- User: user123 / user123

## Not

Eski veritabani ile uyumsuzluk yasarsaniz `stockcontrol.db` dosyasini silip tekrar calistirin.
