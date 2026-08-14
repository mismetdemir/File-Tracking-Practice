<a id="top"></a>

# Dosya Takip ve İşleme Servisi

## İçindekiler

* [Uygulamanın Çalıştırılması](#uygulama)
* [Swagger Ekran Görüntüleri](#swagger)

  * [Tüm Endpointler](#tum-endpointler)
  * [DTO Şemaları](#dto-semalari)
  * [GET: /api/files](#get-files)
  * [GET: /api/files/{id}](#get-file-id)
  * [GET: /api/files/search?extension={extension}](#get-files-search)
  * [POST: /api/files/scan](#post-files-scan)
* [Teknik Kararlar](#teknik-kararlar)
* [Kullanılan Teknolojiler](#kullanilan-teknolojiler)
* [Özellikler](#ozellikler)
* [Kaydedilen Dosya Bilgileri](#kaydedilen-dosya-bilgileri)
* [Proje Yapısı](#proje-yapisi)
* [Yapılandırma](#yapilandirma)
* [Dosya Tarama Mantığı](#dosya-tarama-mantigi)
* [Otomatik Tarama](#otomatik-tarama)
* [Eşzamanlılık Kontrolü](#eszamanlilik-kontrolu)
* [Hata Yönetimi](#hata-yonetimi)
* [Loglama](#loglama)
* [DTO ve Mapping Kullanımı](#dto-mapping)
* [Veritabanı](#veritabani)
* [Unit Testler](#unit-testler)

---

<a id="uygulama"></a>

## Uygulamanın Çalıştırılması

### 1. Repoyu klonlayın

Projeyi Git repository üzerinden bilgisayarınıza indirin:

```bash
git clone https://github.com/mismetdemir/File-Tracking-Practice.git
cd FileTrackingPractice
```

### 2. Bağımlılıkları yükleyin

Projenin ihtiyaç duyduğu NuGet paketlerini yüklemek için:

```bash
dotnet restore
```

komutunu çalıştırın.

### 3. `appsettings.json` dosyasını düzenleyin

Taranmasını istediğiniz klasörün yolunu `appsettings.json` içerisindeki `FolderPath` alanına yazın.

Örnek:

```json
"FileScan": {
  "FolderPath": "C:\\Users\\Username\\Documents\\Files",
  "IntervalInSeconds": 60
}
```

* `FolderPath`: Uygulamanın tarayacağı klasörü belirtir.
* `IntervalInSeconds`: Otomatik klasör taramasının kaç saniyede bir yapılacağını belirtir.

### 4. Veritabanı migrationlarını uygulayın

Entity Framework CLI aracı sisteminizde kurulu değilse:

```bash
dotnet tool install --global dotnet-ef
```

Daha sonra mevcut migrationları veritabanına uygulayın:

```bash
dotnet ef database update --project FileTrackingPractice
```

### 5. Uygulamayı çalıştırın

```bash
dotnet run --project FileTrackingPractice
```

Uygulama geliştirme ortamında aşağıdaki adreslerden çalışır:

```text
http://localhost:5106
https://localhost:7276
```

### 6. Swagger arayüzünü açın

Uygulama çalıştıktan sonra API endpointlerini Swagger üzerinden görüntüleyebilir ve test edebilirsiniz.

```text
http://localhost:5106/swagger
```

veya:

```text
https://localhost:7276/swagger
```

### 7. Testleri çalıştırın

Projede bulunan unit testleri çalıştırmak için solution klasöründe:

```bash
dotnet test
```

komutunu kullanabilirsiniz.

<p align="right"><a href="#top">⬆ Başa Dön</a></p>

---

<a id="swagger"></a>

## Swagger Ekran Görüntüleri

<a id="tum-endpointler"></a>

### Tüm Endpointler

![all endpoints][all_endpoints]

<a id="dto-semalari"></a>

### DTO Şemaları

![schemas][schemas]

<a id="get-files"></a>

### GET: `/api/files`

Veritabanında bulunan tüm işlenmiş dosyaları listeler.

![get all files][get_files]

<a id="get-file-id"></a>

### GET: `/api/files/{id}`

Belirtilen ID'ye sahip dosya kaydını döndürür.

![get file by ID][get_files_id]

Kayıt bulunamazsa:

![404 Not Found][get_files_id_notfound]

<a id="get-files-search"></a>

### GET: `/api/files/search?extension={extension}`

Belirtilen dosya uzantısına sahip kayıtları getirir.

Uzantı değeri işlenmeden önce normalize edilir.

Örneğin:

```text
DOCX
.docx
docx
```

![get by extension][get_files_search]

<a id="post-files-scan"></a>

### POST: `/api/files/scan`

Klasörde herhangi bir değişiklik olmadığında:

![all skipped][post_scan1]

Bir dosyada değişiklik olduğunda:

![one file changed][post_scan2]

[all_endpoints]: SwaggerScreenshots/endpoints.png
[schemas]: SwaggerScreenshots/schemas.png
[get_files]: SwaggerScreenshots/get_files.png
[get_files_id]: SwaggerScreenshots/get_files_id.png
[get_files_id_notfound]: SwaggerScreenshots/get_files_id_notfound.png
[get_files_search]: SwaggerScreenshots/get_files_search.png
[post_scan1]: SwaggerScreenshots/post_files_scan.png
[post_scan2]: SwaggerScreenshots/post_files_scan_update.png

<p align="right"><a href="#top">⬆ Başa Dön</a></p>

---

<a id="teknik-kararlar"></a>

## Teknik Kararlar

Projede katmanlı ve modüler bir yapı kullandım.
API istekleri `Controller` katmanında karşılanırken, dosya tarama işlemleri `Service` katmanında tutuldu.
Veritabanı işlemleri Entity Framework Core üzerinden `AppDbContext` ile gerçekleştirildi.
API tarafında veritabanı modellerini doğrudan döndürmemek adına DTO ve mapping yapısı kullandım.

Veritabanı olarak kurulumu ve kullanımına alışık olduğum için SQLite tercih ettim.
Klasör yolunu ve tarama aralığını `appsettings.json` üzerinden yönetilebilir şekilde ayarladım.
Otomatik tarama fonksiyonunu `BackgroundService` sınıfında oluşturdum ve 
manuel tarama ile otomatik taramanın aynı anda çalışmasını engellemek için `SemaphoreSlim` kullanıldım.

Dosyalarda herhangi bir içerik değişikliği olup olmadığını tespit etmek için 
dosya içeriğinden SHA-256 hash değeri hesaplandı.

Performans açısından her dosya için ayrı veritabanı sorgusu yapmak yerine 
mevcut dosya kayıtları tek sorguda alınarak bellekte karşılaştırılmasını sağladım. 
Hataların merkezi şekilde yönetilmesi için `IExceptionHandler` tabanlı global exception 
handling ve standart hata cevapları için `ProblemDetails` kullandım. 
Tarama işlemleri ve oluşan hatalar `Microsoft.Extensions.Logging` ile loglandı.
Ayrıca dosya tarama senaryolarının doğruluğunu kontrol etmek amacıyla xUnit, Moq 
ve Entity Framework Core InMemory kullanarak unit testler yazdım.

<p align="right"><a href="#top">⬆ Başa Dön</a></p>

---

<a id="kullanilan-teknolojiler"></a>

## Kullanılan Teknolojiler

* .NET
* ASP.NET Core Web API
* Entity Framework Core
* SQLite
* Swagger / OpenAPI
* Microsoft.Extensions.Logging
* xUnit
* Moq
* Entity Framework Core InMemory

<p align="right"><a href="#top">⬆ Başa Dön</a></p>

---

<a id="ozellikler"></a>

## Özellikler

* Belirlenen klasörü ve alt klasörlerini tarar.
* Dosya bilgilerini SQLite veritabanına kaydeder.
* Daha önce işlenmiş ve değişmemiş dosyaların tekrar işlenmesini engeller.
* Dosya içerikleri için SHA-256 hash hesaplar.
* Daha önce kaydedilmiş bir dosyanın içeriği değiştiğinde mevcut kaydı günceller.
* Background Service ile otomatik tarama yapar.
* REST API üzerinden manuel tarama yapılmasına izin verir.
* Manuel ve otomatik taramanın aynı anda çalışmasını engeller.
* Dosya uzantısına göre arama yapılmasını sağlar.
* API cevaplarında DTO kullanır.
* Tam dosya yolu yerine istemciye relative path döndürür.
* Global exception handling ile standart hata cevapları oluşturur.
* Tarama işlemlerini ve hataları loglar.
* Dosya tarama servisi için unit testler içerir.

<p align="right"><a href="#top">⬆ Başa Dön</a></p>

---

<a id="kaydedilen-dosya-bilgileri"></a>

## Kaydedilen Dosya Bilgileri

Her dosya için aşağıdaki bilgiler tutulur:

* Dosya adı
* Dosya uzantısı
* Dosya boyutu
* Oluşturulma tarihi
* Son değiştirilme tarihi
* Dosyanın tam yolu
* SHA-256 hash değeri

Tam dosya yolu veritabanında tutulmasına rağmen API üzerinden kullanıcıya relative path döndürülür.

<p align="right"><a href="#top">⬆ Başa Dön</a></p>

---

<a id="proje-yapisi"></a>

## Proje Yapısı

```text
FileTrackingPractice/
│
├── FileTrackingPractice/
│   ├── BackgroundServices/
│   │   └── AutoFileScanService.cs
│   │
│   ├── Config/
│   │   └── FileScanSettings.cs
│   │
│   ├── Controllers/
│   │   └── FilesController.cs
│   │
│   ├── Data/
│   │   └── AppDbContext.cs
│   │
│   ├── DTOs/
│   │   ├── FileRecordDto.cs
│   │   └── ScanResultDto.cs
│   │
│   ├── Exceptions/
│   │   └── FileScanConfigurationException.cs
│   │
│   ├── Mappings/
│   │   └── FileRecordMapper.cs
│   │
│   ├── Middleware/
│   │   └── GlobalExceptionHandler.cs
│   │
│   ├── Migrations/
│   │
│   ├── Models/
│   │   └── FileRecord.cs
│   │
│   ├── Services/
│   │   ├── FileScannerService.cs
│   │   └── IFileScannerService.cs
│   │
│   ├── Program.cs
│   └── appsettings.json
│
└── FileTrackingPractice.Tests/
    └── FileScannerServiceTests.cs
```

<p align="right"><a href="#top">⬆ Başa Dön</a></p>

---

<a id="yapilandirma"></a>

## Yapılandırma

Taranacak klasör, otomatik tarama aralığı ve veritabanı bağlantısı `appsettings.json` üzerinden yönetilir.

Örnek:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=FileTrackingDb.db"
  },
  "FileScan": {
    "FolderPath": "C:\\FileTracker\\File1",
    "IntervalInSeconds": 60
  }
}
```

Projeyi çalıştırmadan önce `FolderPath` alanının bilgisayarınızda bulunan geçerli bir klasörü göstermesi gerekir.

Örneğin:

```json
"FileScan": {
  "FolderPath": "C:\\Users\\YourUser\\Documents\\Files",
  "IntervalInSeconds": 60
}
```

`IntervalInSeconds`, Background Service tarafından otomatik taramanın kaç saniyede bir gerçekleştirileceğini belirler.

<p align="right"><a href="#top">⬆ Başa Dön</a></p>

---

<a id="dosya-tarama-mantigi"></a>

## Dosya Tarama Mantığı

Tarama başladığında ayarlarda belirtilen klasör ve klasörün 
alt klasörlerinde bulunan dosyalar alınır.

Her dosyanın içeriğinden SHA-256 hash değeri hesaplanır.

Dosyanın path değeri veritabanında bulunmuyorsa yeni bir 
`FileRecord` oluşturulur ve dosya bilgileri veritabanına kaydedilir.

Dosya daha önce kaydedilmişse yeni hesaplanan hash değeri veritabanında 
bulunan hash değeriyle karşılaştırılır.

Hash değerleri aynıysa dosyanın içeriğinin değişmediği kabul edilir ve dosya tekrar işlenmez.

Hash değerleri farklıysa dosyanın içeriğinin değiştiği kabul edilir ve 
mevcut kayıt yeni dosya bilgileriyle güncellenir.

Bu sayede değişmeyen dosyaların gereksiz şekilde tekrar işlenmesi engellenirken, 
içeriği değiştirilen dosyalar yeniden işlenebilir.

<p align="right"><a href="#top">⬆ Başa Dön</a></p>

---

<a id="otomatik-tarama"></a>

## Otomatik Tarama

Otomatik klasör taraması ASP.NET Core `BackgroundService` kullanılarak gerçekleştirilmiştir.

`AutoFileScanService`, belirlenen süre aralıklarında `IFileScannerService` üzerinden klasör taraması başlatır.

Tarama aralığı:

```json
"IntervalInSeconds": 60
```

ayarı üzerinden değiştirilebilir.

Bu yapı sayesinde kullanıcı manuel olarak API isteği göndermese 
bile klasör belirli aralıklarla otomatik olarak kontrol edilir.

<p align="right"><a href="#top">⬆ Başa Dön</a></p>

---

<a id="eszamanlilik-kontrolu"></a>

## Eşzamanlılık Kontrolü

Otomatik tarama çalışırken kullanıcı aynı anda:

```http
POST /api/files/scan
```

endpointini çağırabilir.

İki taramanın aynı anda çalışması aynı dosyaların eş zamanlı olarak 
işlenmesine ve veritabanında çakışmalara neden olabilir.

Bunu engellemek için `FileScannerService` içerisinde ortak bir `SemaphoreSlim` kullanılmıştır.

Bu yapı aynı anda yalnızca bir tarama işleminin çalışmasına izin verir.

Bir tarama devam ederken başka bir tarama isteği gelirse ikinci işlem mevcut taramanın tamamlanmasını bekler.

<p align="right"><a href="#top">⬆ Başa Dön</a></p>

---

<a id="hata-yonetimi"></a>

## Hata Yönetimi

Uygulamada merkezi hata yönetimi kullanılmaktadır.

`GlobalExceptionHandler`, ASP.NET Core `IExceptionHandler` yapısını kullanarak 
uygulama içerisinde oluşan hataların tek bir noktadan yönetilmesini sağlar.

Hatalar `Microsoft.Extensions.Logging` kullanılarak loglanır.

API hata cevapları standart `ProblemDetails` formatında döndürülür.

Hata cevaplarına ayrıca `traceId` eklenerek log kayıtları ile API cevaplarının 
eşleştirilmesi kolaylaştırılmıştır.

Bu yapı sayesinde Controller veya Service içerisinde aynı hata yönetimi 
kodlarının tekrar tekrar yazılması engellenmiştir.

<p align="right"><a href="#top">⬆ Başa Dön</a></p>

---

<a id="loglama"></a>

## Loglama

Projede `Microsoft.Extensions.Logging` kullanılmaktadır.

Tarama işlemi sırasında aşağıdaki durumlar loglanır:

* Taramanın başlaması
* Yeni dosya bulunması
* Mevcut dosyanın güncellenmesi
* Değişmeyen dosyanın atlanması
* Dosya işleme sırasında hata oluşması
* Taramanın tamamlanması

Tarama tamamlandığında bulunan, eklenen, güncellenen, atlanan ve başarısız olan dosya sayıları loglanır.

<p align="right"><a href="#top">⬆ Başa Dön</a></p>

---

<a id="dto-mapping"></a>

## DTO ve Mapping Kullanımı

Veritabanı entity'leri doğrudan API üzerinden döndürülmemektedir.

`FileRecordMapper` sınıfı kullanılarak:

```text
FileRecord → FileRecordDto
```

dönüşümü gerçekleştirilir.

Bu yaklaşım veritabanı modelinin API modelinden ayrılmasını sağlar.

Aynı zamanda veritabanında tutulan tam dosya yolunun doğrudan kullanıcıya gösterilmesi engellenir.

API üzerinden bunun yerine relative path döndürülür.

<p align="right"><a href="#top">⬆ Başa Dön</a></p>

---

<a id="veritabani"></a>

## Veritabanı

Projede Entity Framework Core ile birlikte SQLite kullanılmaktadır.

Veritabanı şeması migrationlar üzerinden yönetilmektedir.

Projede bulunan migrationlar arasında:

```text
InitialCreate
AddPathToFileRecord
AddHashToFileRecord
```

bulunmaktadır.

Dosyanın path alanı için unique kontrol kullanılarak aynı dosya yolunun birden fazla kez kaydedilmesi engellenmektedir.

<p align="right"><a href="#top">⬆ Başa Dön</a></p>

---

<a id="unit-testler"></a>

## Unit Testler

Projede ayrı bir xUnit test projesi bulunmaktadır:

```text
FileTrackingPractice.Tests
```

Testlerde aşağıdaki teknolojiler kullanılmıştır:

* xUnit
* Moq
* Entity Framework Core InMemory

Test edilen senaryolar arasında şunlar bulunmaktadır:

* `FolderPath` değerinin tanımlanmamış olması
* Belirtilen klasörün bulunamaması
* Cancellation isteği
* Eşzamanlı tarama işlemleri
* Boş klasörün taranması
* Yeni dosyanın veritabanına kaydedilmesi
* Birden fazla dosyanın işlenmesi
* Daha önce işlenen ve değişmeyen dosyanın atlanması
* Alt klasörlerdeki dosyaların bulunması
* Dosya uzantısı olmayan dosyaların işlenmesi
* Farklı klasörlerde aynı isimli dosyaların işlenmesi
* Yeni ve mevcut dosyaların birlikte bulunduğu klasörlerin taranması
* SHA-256 hash değerinin kaydedilmesi
* İçeriği değiştirilen dosyanın güncellenmesi
* Tarama başlangıç ve bitiş zamanlarının oluşturulması
* Tarama işlemlerinin loglanması

Testleri çalıştırmak için:

```bash
dotnet test
```

komutu kullanılabilir.

<p align="right"><a href="#top">⬆ Başa Dön</a></p>