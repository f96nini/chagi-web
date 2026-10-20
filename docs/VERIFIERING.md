# Leverans och verifiering – 2026-10-20

Projekt: `D:\work\tkd-appen\chagi-web`. Önskad slutplacering är
`D:\work\repos\chagi-web`, men sessionen har inte skrivrättighet där.
`scripts/Copy-ToRepos.ps1` gör en ren källkodskopia när det körs i en session med
åtkomst till målkatalogen. Inga projekt refererar med absoluta sökvägar till den
ursprungliga appen. Originalets `git status --short` är fortfarande tomt.

## Faser

| Fas | Ändrade/nya filer | Resultat och kvarvarande arbete |
| --- | --- | --- |
| 0, inventering | `docs/INVENTERING.md` | Konkreta källfiler, datakontrakt och adapterplan dokumenterades före extrahering. |
| 1, delad kod | `Chagi.Core/Models`, `Logic`, `Storage`, `WebConnection`; appens repository/cache-adaptrar, projektreferens, XAML-assemblyreferenser och Preferences-adapter | Gemensam parser och oförändrade rankingregler. Android- och Windows-byggen godkända. Befintliga regressionskontroller refererar nu Core. |
| 2, webb | `Chagi.Web/Pages`, `Components`, `Layout`, `Services`, `wwwroot`; `Chagi.Web.Checks` | Alla MVP-vyer, profiler och klassresultat implementerade. Riktiga Razor-komponenter kompilerar och renderas i 12 kontroller. Full WASM-start och visuell granskning återstår. |
| 3, data | `PublishedResultsClient`, `BrowserSnapshotStore`, `cache.js`, `Chagi.Tests` | Gemensamt manifest, SHA-256, LF/CRLF, selektiv HTTP-hämtning, 15-minutersintervall, atomisk IndexedDB-cache och offline-status. 29 data-/rankingkontroller godkända. Verklig lokal publiceringskopia verifierad separat. Live-CORS/headers återstår. |
| 4, bilder | `PublicImages`, `Avatar`, `ClubBadge`, publik konfiguration | Godkända HTTPS-URL:er, lazy loading, stabila mått och fallback även vid bildfel. Inga personbilder eller klubblogotyper har nypublicerats. Faktiska bilder kräver rättighetsunderlag och konfiguration. |
| 5, publicering | `.github/workflows/pages.yml`, `prepare-pages.mjs`, `404.html`, README; anpassat befintligt Android-releaseworkflow | Bygg-/deploy-workflow implementerat. Root-, repo- och alternativ base-adress klarar routingkontroller. GitHub-repository, Pages-inställning och verklig deploy har inte skapats/körts i denna session. |

## Utförda kontroller

`./scripts/Test.ps1` avslutades med exitkod 0: **249 godkända kontroller** i nio
.NET-testprogram plus Pages-routingkontrollerna. De täcker rankingpoäng och delade
placeringar, svensk sortering, klubbbyten, aktuell bältesklass, medaljstatistik,
historisk matchdata, mobilcache/favoriter, navigering/bakgrundsarbete, webbens
dataklient samt riktiga Razor-vyer med data i minnet.

Två ytterligare kontroller kördes med:

```powershell
dotnet run --project Chagi.Tests/Chagi.Tests.csproj -- --published-dir D:/work/repos/chagi-files-live-publish/chagi
```

Den lokala publiceringskopian, version `2026-10-19`, innehåller **22 resultatfiler,
22 tävlingar och 1 184 fighters**. Samtliga manifestfiler klarade kontrollsummorna;
resultat och match-JSON lästes med konsekventa poängsummor. Detta bevisar inte att
den aktuella nätpubliceringen har samma innehåll eller rätt CORS-headers.

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

## Ej verifierat

- NuGet och GitHub Pages gick inte att nå: DNS-fel för `api.nuget.org` respektive
  `f96nini.github.io`. WebAssembly-paketen och browser-wasm-runtime saknas i den
  lokala paketlagringen. Därför har det riktiga WebAssembly-projektet inte kunnat
  återställas, byggas eller publiceras här. `Chagi.Web.Checks` kompilerar/rendrar
  Razor och webbtjänster med installerade .NET-referenser, men verifierar inte
  WebAssembly-hostens `Program.cs`, browser-HTTP-adaptern, trimning eller startup.
- Visuell webbläsarkontroll nekades av behörighetskontrollen när den lokala
  förhandsvisningen skulle öppnas. Inget annat webbläsarverktyg användes för att
  kringgå detta. CSS-layout på mobil/desktop, tangentbordsinteraktion och riktiga
  IndexedDB-/bildfelshändelser behöver kontrolleras i webbläsaren.
- Faktisk Pages-deploy, live-CORS, cacheheaders och uppdatering efter deploy återstår.
  Workflow kör ett riktigt Release-publish och stoppar deploy om det inte lyckas.

## Första körningen med nätåtkomst

1. Kopiera till önskad katalog, återställ och kör `Chagi.Web` enligt README.
2. Kontrollera alla fyra huvudvyer, sökning/filter, profiler, bildfallback och
   tangentbord på mobil- och desktopbredder.
3. Ladda ett fullständigt arkiv, bryt dataanslutningen och kontrollera sparad-data-
   meddelandet. Uppdatera samma datakälla och kontrollera selektiva hämtningar.
4. Kontrollera publiceringsrättigheter och rättslig grund innan extern publicering.
5. Pusha till avsett repository, välj Actions som Pages-källa och verifiera både
   startsida och omladdning av en profil-/tävlingsdirektlänk under rätt base-adress.

Den tillfälliga lokala QA-servern har stoppats. Den är inte en del av webbprojektet
och ingår inte i en källkodskopia med kopieringsskriptet.
