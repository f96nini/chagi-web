# Leverans och verifiering – 2026-10-20

Projekt: `D:\work\repos\chagi-web`, en checkout av
<https://github.com/f96nini/chagi-web>. Webbplats:
<https://f96nini.github.io/chagi-web/>. Den äldre arbetskopian finns kvar under
`D:\work\tkd-appen\chagi-web`. Inga projekt refererar med absoluta sökvägar till
den ursprungliga appen; dess arbetskopia har inte ändrats.

## Faser

| Fas | Ändrade/nya filer | Resultat och kvarvarande arbete |
| --- | --- | --- |
| 0, inventering | `docs/INVENTERING.md` | Konkreta källfiler, datakontrakt och adapterplan dokumenterades före extrahering. |
| 1, delad kod | `Chagi.Core/Models`, `Logic`, `Storage`, `WebConnection`; appens repository/cache-adaptrar, projektreferens, XAML-assemblyreferenser och Preferences-adapter | Gemensam parser och oförändrade rankingregler. Android- och Windows-byggen godkända. Befintliga regressionskontroller refererar nu Core. |
| 2, webb | `Chagi.Web/Pages`, `Components`, `Layout`, `Services`, `wwwroot`; `Chagi.Web.Checks` | Tävlingar, fighters, ranking och hela klubbflödet med katalog, profil, fighters och klubbmedaljer. Riktiga Razor-komponenter kompilerar och renderas i 16 kontroller. Release-WASM byggdes och startade på den publika sajten; startsida, ranking och tävlingsdetalj kontrollerades i mobilbredd. |
| 3, data | `PublishedResultsClient`, `BrowserSnapshotStore`, `cache.js`, `Chagi.Tests` | Gemensamt manifest, SHA-256, LF/CRLF, selektiv HTTP-hämtning, 15-minutersintervall, atomisk IndexedDB-cache och offline-status. 29 data-/rankingkontroller godkända. Publicerad datakälla svarade HTTP 200 med `Access-Control-Allow-Origin: *`; startsida och ranking läste live-data. |
| 4, bilder | `PublicImages`, `Avatar`, `ClubBadge`, `wwwroot/images` | Appens blå palett, appikon, bakgrund, medaljer och klubblogotyper återanvänds. Fallback för okända klubbar och fighters utan bild, lazy loading och stabila mått. Godkända HTTPS-URL:er kan ersätta enskilda bilder. |
| 5, publicering | `.github/workflows/pages.yml`, `prepare-pages.mjs`, `404.html`, README; anpassat befintligt Android-releaseworkflow | Root-, repo- och alternativ base-adress klarar routingkontroller. Repositoryt `f96nini/chagi-web` skapades; Pages använder Actions. Första workflow-körningen [39452495326](https://github.com/f96nini/chagi-web/actions/runs/39452495326) byggde och distribuerade med `success`. Startsida och WebAssembly-resurs svarade HTTP 200; omladdning av `/ranking` fungerade i webbläsaren. |

## Utförda kontroller

`./scripts/Test.ps1` avslutades med exitkod 0: **253 godkända kontroller** i nio
.NET-testprogram plus Pages-routingkontrollerna. De täcker rankingpoäng och delade
placeringar, svensk sortering, klubbbyten, aktuell bältesklass, medaljstatistik,
historisk matchdata, mobilcache/favoriter, navigering/bakgrundsarbete, webbens
dataklient samt riktiga Razor-vyer med data i minnet. De fyra nya Razor-kontrollerna
täcker klubbkatalog, klubbprofil, tävlingsmedaljer och appens klubblogotyp.

Den nya klubbkatalogen har kontrollerats lokalt i webbläsare mot publicerad data:
**42 klubbar**, däribland resultatklubbar utanför katalogen. Klubbprofil,
utövarflik och klubbens tävlingsmedaljer öppnades från riktiga resultat. Sidornas
bilder och appens blå färgsättning granskades i desktopbredd.

Två ytterligare kontroller kördes med:

```powershell
dotnet run --project Chagi.Tests/Chagi.Tests.csproj -- --published-dir D:/work/repos/chagi-files-live-publish/chagi
```

Den lokala publiceringskopian, version `2026-10-19`, innehåller **22 resultatfiler,
22 tävlingar och 1 184 fighters**. Samtliga manifestfiler klarade kontrollsummorna;
resultat och match-JSON lästes med konsekventa poängsummor. Den publika sajten visade
dataversion `2026-10-19`, 22 tävlingar och rankingresultat från samma nätkälla.

`dotnet publish Chagi.Web/Chagi.Web.csproj -c Release -o artifacts/site` avslutades
med exitkod 0. Det lokala bygget gjordes utan valfri `wasm-tools`-optimering.
GitHub Actions körde tester, publicerade WebAssembly och distribuerade sajten.

Följande fullständiga Debug-byggen avslutades med exitkod 0:

```powershell
dotnet build Chagi.App/ITF-Taekwondo-Gradering.csproj -f net9.0-android --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
dotnet build Chagi.App/ITF-Taekwondo-Gradering.csproj -f net9.0-windows10.0.19041.0 --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
```

Android producerade APK, inklusive debugsignerad APK. Windows producerade exe och
assembly. Windowsbygget rapporterade 41 XAML-bindningsvarningar; de berör kvarvarande
okompilerade bindningar i appens befintliga vyer. Androids första kompilering gav
samma varningstyper; det avslutande inkrementella Androidbygget hade 0 varningar
och 0 fel. Inga kontroller på fysisk Android-enhet eller av MAUI:s UI gjordes.

## Kvarvarande QA

- Mobilbredd för de nya klubbsidorna, tangentbordsnavigering, sök- och filterkombinationer samt
  felande bild-URL:er har inte granskats fullt ut i webbläsare.
- IndexedDB-cache vid avbruten dataanslutning och selektiva hämtningar efter
  uppdatering av manifestet har täckning i kodkontroller men ingen live-simulering.
- GitHub Pages cacheheaders och klientuppdatering efter en senare deploy har inte
  verifierats. Direktlänkar fungerar i webbläsare efter omladdning, men Pages ger
  först ett HTTP 404-svar innan `404.html` återställer routen.
- MAUI-appen har byggts för Android och Windows men inte körts på fysisk enhet.
