# Chagi Web

Responsiv, svensk Blazor WebAssembly-webbplats för ITF-taekwondo. Visar tävlingar,
klassresultat, fighters, klubbar, klubbmedaljer, klubbens poängliga,
tävlingshistorik, VS, matchdetaljer och filtrerad Poängliga. Ren statisk hosting;
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
Tävlingslistan, fighterlistan och Poängligan läser hela arkivet vid behov.
Filer återanvänds efter kontrollsummeverifiering; bara ändrade/saknade filer hämtas.
Fighterprofilens VS grupperar inbördes möten per disciplin eller motståndare med
appens beräkning i `Chagi.Core`. Matchdetaljerna hämtas ur samma publicerade
matchfiler och länkas från berörda tävlingsklasser och VS-möten. Äldre möten som
bara bygger på olika pallplaceringar markeras särskilt.
Ett nytt manifest kontrolleras efter 15 minuter vid nästa sidladdning i appen.
Webbläsarens `fetch cache: no-cache` låter HTTP-cachen hantera ETag/Last-Modified
utan egna CORS-känsliga villkorsheaders. Dataklienten förutsätter inte att Pages
exponerar ETag för JavaScript. Ingen extra proxy/backend behövs.

En komplett snapshot sparas atomiskt i IndexedDB. Vid nätverks- eller checksummefel
visas tidigare fullständiga data med tydlig varning. En ofullständig cache används
aldrig som komplett arkiv. Datacache är inte samma sak som en offlineinstallerad
app: ingen service worker cachar själva klienten. Första besöket kräver internet.

Poängligan behåller aktuell åldersklass och senaste bältesgrupp när säsongen filtreras.
Klubbfiltret avgränsar ligans jämförelsegrupp. Okänt kön/klass uppfinns inte.
Profiler använder stabila namn/klubbnycklar i URL:er eftersom appens Guid-ID:n
genereras vid import. Appens speciella tävlings-medaljtopplista använder fortsatt
sin egna befintliga täta poänggruppering; dess regler ändras inte av webbens Poängliga.

Webbens fighterlänkar använder läsbara namn och klubbar följt av ett stabilt
kontrollvärde. Tidigare Base64-kodade fighterlänkar fortsätter att fungera.
Cloudflare Web Analytics är manuellt inbäddad i `Chagi.Web/wwwroot/index.html`.
Den mäter sidvägar, vilket även omfattar namn och klubb i fighterprofilernas
adresser. Cloudflares automatiska SPA-mätning används; ingen egen
sidvisningsbeacon är implementerad.

## GitHub Pages

Webbplatsen är publicerad på <https://f96nini.github.io/chagi-web/> från
<https://github.com/f96nini/chagi-web>. GitHub Pages använder GitHub Actions som
källa. En push till `main` kör `pages.yml`: tester, WebAssembly-publicering och
uppladdning av endast `artifacts/site/wwwroot`. Deploy-jobbet använder miljön
`github-pages`. Kontrollera rättslig grund och publiceringsrättigheter innan nya
identifierande uppgifter eller bilder läggs till, särskilt för minderåriga.

`configure-pages` ger korrekt base-adress för `/repo/` och egen domän.
`prepare-pages.mjs` skriver `<base>`, versionsmärker CSS, skapar `.nojekyll` och anpassar `404.html`.
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
och kontrollera både startsida och en direktlänk. Startsidan, WebAssembly-resurser,
datakällans CORS-headers och omladdning av `/ranking` har verifierats mot den
publicerade sajten. Uppdatering efter en framtida deploy återstår att verifiera.
Poängligan nås via `/poangligan`; den tidigare adressen `/ranking` fungerar också.

## Bilder och färger

Webben använder appens blå färgpalett, appikon, bakgrundsgrafik, medaljikoner och
klubblogotyper från `Chagi.App`/MAUI-resurserna. De publicerade kopiorna ligger i
`Chagi.Web/wwwroot/images`. Katalogklubbar utan logotyp använder appens generiska
klubbsymbol. Ingen personbild har kopierats. Använd
`PublicImages:Clubs:<klubb-id>` eller `PublicImages:Fighters:<profil-id>` i
`appsettings.json` för att ersätta en bild med en godkänd HTTPS-URL. Klubb-id:n
finns i `ClubCatalogService`; fighter-id finns i profilens URL. Fighters utan
personbild visas med initialer. Bilderna har lazy loading, asynkron avkodning och
stabila bildmått.

## Projektkopia på Heisenberg

`D:\work\repos\chagi-web` är en Git-checkout som spårar
`f96nini/chagi-web`. Den äldre arbetskopian under `D:\work\tkd-appen\chagi-web`
finns kvar. Projektet har inga absoluta källreferenser till den äldre kopian.
