# Chagi Web

Responsiv, svensk Blazor WebAssembly-webbplats för ITF-taekwondo. Visar tävlingar,
klassresultat, fighters, tävlingshistorik och filtrerad ranking. Ren statisk hosting;
ingen backend, administration, databas eller inloggning.

Projektet utgår från MAUI-appen `f96nini/ITF-Taekwondo-Gradering`, commit `1df90bb`.
Originalets arbetskopia har inte ändrats. Delade modeller och beräkningar har en
enda implementation i `Chagi.Core`, som refereras av båda klienterna.

```text
Chagi.sln                 Alla projekt, inklusive mobilappen
Chagi.Web.sln             Web/Core/tester, utan MAUI-workload
Chagi.App/                Befintlig XAML/MAUI-app och plattformsadaptrar
Chagi.Core/               Parser, modeller, ranking, statistik, datakontrakt
Chagi.Web/                Fristående Blazor WebAssembly
Chagi.Tests/              Körbara kontroller för HTTP-data, cache och ranking
Chagi.Web.Checks/         Kompilerar och renderar riktiga Razor-komponenter i tester
scripts/                  Tester, Pages-paketering och kopiering till repos
docs/INVENTERING.md        Ursprunglig filplan och arkitekturbeslut
docs/VERIFIERING.md        Vad som faktiskt har byggts och testats
```

## Kör lokalt

Kräver .NET SDK 9.0.3xx eller senare 9.0-patch enligt `global.json` och internet
för den första NuGet-återställningen. Node 22+ används bara för Pages-skript/tester.

```powershell
dotnet restore Chagi.Web/Chagi.Web.csproj
dotnet run --project Chagi.Web/Chagi.Web.csproj
# http://localhost:5180

./scripts/Test.ps1
# Eller bara delade datakontroller:
dotnet run --project Chagi.Tests/Chagi.Tests.csproj
dotnet run --project Chagi.Web.Checks/Chagi.Web.Checks.csproj
```

MAUI utvecklas separat med tillhörande .NET 9-workloads och Android/Windows SDK:

```powershell
dotnet build Chagi.App/ITF-Taekwondo-Gradering.csproj -f net9.0-android
dotnet build Chagi.App/ITF-Taekwondo-Gradering.csproj -f net9.0-windows10.0.19041.0
```

Det kompletta solution-bygget kräver alla relevanta plattformsverktyg. Webbens
workflow installerar inte MAUI; det kör däremot mobilens plattformsoberoende
regressionstester mot Core och appens riktiga lagringsadaptrar med teststand-ins.
Razor-kontrollerna körs direkt i .NET med data i minnet och kräver inga
WebAssembly-paket. De verifierar markup och komponentbindningar, men ersätter
inte en kontroll av CSS, JavaScript och WebAssembly-start i webbläsaren.

## Samma publicerade data

`Chagi.Web/wwwroot/appsettings.json` använder appens befintliga basadress:
`https://f96nini.github.io/chagi-files/chagi/`. Den är publik konfiguration,
inte en hemlighet. Inga API-nycklar får läggas här.

Manifest och SHA-256 beskriver `results/*.txt`, `matches/index.json` och match-JSON.
Det finns inget påhittat fighter-API. `CompetitorResultsParser` är extraherad ur
mobilens repository. Båda klienterna använder samma parser, ranking, medalj-
statistik, klubbnormalisering och LF/CRLF-kompatibla kontrollsummor.

Startsidan läser de tre senaste textresultaten och tillhörande matchdata.
Tävlingslistan, fighterlistan och rankingen läser hela arkivet vid behov.
Filer återanvänds efter kontrollsummeverifiering; bara ändrade/saknade filer hämtas.
Ett nytt manifest kontrolleras efter 15 minuter vid nästa sidladdning i appen.
Webbläsarens `fetch cache: no-cache` låter HTTP-cachen hantera ETag/Last-Modified
utan egna CORS-känsliga villkorsheaders. Dataklienten förutsätter inte att Pages
exponerar ETag för JavaScript. Ingen extra proxy/backend behövs.

En komplett snapshot sparas atomiskt i IndexedDB. Vid nätverks- eller checksummefel
visas tidigare fullständiga data med tydlig varning. En ofullständig cache används
aldrig som komplett arkiv. Datacache är inte samma sak som en offlineinstallerad
app: ingen service worker cachar själva klienten. Första besöket kräver internet.

Ranking behåller aktuell åldersklass och senaste bältesgrupp när säsongen filtreras.
Klubbfiltret avgränsar rankingens jämförelsegrupp. Okänt kön/klass uppfinns inte.
Profiler använder stabila namn/klubbnycklar i URL:er eftersom appens Guid-ID:n
genereras vid import. Appens speciella tävlings-medaljtopplista använder fortsatt
sin egna befintliga täta poänggruppering; dess regler ändras inte av webbrankingen.

## GitHub Pages

1. Skapa ett GitHub-repository för projektet och pusha innehållet till `main`
   (eller `master`). Koppla rätt remote; projektet pekar inte automatiskt på appens
   gamla repository.
2. Välj **Settings → Pages → Build and deployment → GitHub Actions**.
3. Workflow `pages.yml` kör tester, publicerar WebAssembly och laddar upp endast
   `artifacts/site/wwwroot`. Deploy-jobbet använder miljön `github-pages`.
4. Kontrollera rättslig grund och publiceringsrättigheter, särskilt för minderåriga,
   innan webbplatsen görs publik. En tidigare apppublicering räcker inte som underlag.

`configure-pages` ger korrekt base-adress för `/repo/` och egen domän.
`prepare-pages.mjs` skriver `<base>`, skapar `.nojekyll` och anpassar `404.html`.
En direktlänk som `/repo/fighters/<id>?q=...#historik` går via Pages 404-sida till
roten och återställs med `history.replaceState` före Blazor startar. Ett första
404-svar och omdirigering är en begränsning för sökrobotar och delningsförhandsvisningar.
Om fullständig SEO behövs senare krävs prerendering eller annan hosting.

Lokal publicering under en projektadress:

```powershell
dotnet publish Chagi.Web/Chagi.Web.csproj -c Release -o artifacts/site
node scripts/prepare-pages.mjs artifacts/site/wwwroot /chagi-web/
```

GitHub Pages styr HTTP-cacheheaders. Ingen service worker låser HTML till en gammal
version. Blazor använder sina genererade resursmanifest; ladda om sidan efter deploy
och kontrollera både startsida och en direktlänk. Liveheaders, CORS och faktisk
uppdatering efter deploy måste verifieras mot det publicerade ursprunget.

## Bilder

Inga personfoton eller klubblogotyper har kopierats från mobilens resurser till
webbens publika katalog. Använd `PublicImages:Clubs:<klubb-id>` eller
`PublicImages:Fighters:<profil-id>` i `appsettings.json` för godkända HTTPS-bilder.
Klubb-id:n finns i `ClubCatalogService`; fighter-id finns i profilens URL.
Initialer används som fallback, även om en bild inte kan laddas. Komponenterna
använder lazy loading, asynkron avkodning och stabila bildmått. Små WebP/AVIF-bilder
kan ligga på Pages och senare ersättas med objektlagrings-URL:er utan modelländring.

## Flytta till D:\work\repos

Arbetet gjordes i `D:\work\tkd-appen\chagi-web` eftersom den sessionen saknar
skrivrättighet utanför sin workspace. Projektet har inga absoluta källreferenser.
Kör följande i en vanlig PowerShell-session för en ren källkodskopia:

```powershell
./scripts/Copy-ToRepos.ps1
# Standardmål: D:\work\repos\chagi-web (måste vara en ny katalog)
```

Skriptet kopierar, skriver inte över ett befintligt mål och lämnar originalet kvar.
Byggartefakter, signering, Git-interna filer och lokal QA följer inte med.
