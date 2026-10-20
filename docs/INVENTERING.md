# Inventering och filplan – 2026-10-20

Källa: den rena arbetskopian `ITF-Taekwondo-Gradering`, commit `1df90bb` (Version 2.1).
Originalkatalogen ändras inte. Endast Git-spårade filer har kopierats till `Chagi.App`;
signeringsfiler, lokala inställningar och byggartefakter följer inte med.

## Före implementationen

- MAUI, XAML, .NET 9. Ingen Blazor Hybrid eller befintliga Razor-vyer.
- `WebConnection/ChagiVersion.cs`: `https://f96nini.github.io/chagi-files/chagi/`.
- `WebConnection/ChagiDataManifest.cs`: JSON-manifest med sökvägar och SHA-256.
- `results/*.txt`: textresultat, inte ett fighter-API. `matches/index.json` och
  `matches/*.json` kompletterar även deltagare utan medalj/placering.
- `Logic/CompetitorRepository.cs`: parser, identiteter, klubbnormalisering,
  5/3/1-poäng, senaste bälte, åldersklass, rankingögonblicksbilder samt lokal lagring.
- `Logic/RankingService.cs`: kön, aktuell åldersklass och senaste bältesgrupp;
  poäng följt av guld/silver/brons, svensk namnsortering och delade placeringar.
  Säsongsfilter ska begränsa resultat men behålla fighterens aktuella klass/bälte.
- Kompletta deltagarlistor finns enligt befintlig kod först från 2026-09-26.
  Äldre listor får inte presenteras som fullständiga deltagarantal.

## Konkret filplan

| Befintlig fil | Åtgärd |
| --- | --- |
| `Models/Ranking/*.cs` | Flytta oförändrade till `Chagi.Core/Models/Ranking`. |
| `Models/Geography/*.cs`, `Models/Clubs/*.cs` | Flytta till Core. |
| `Models/ChildBelt.cs` | Flytta till Core; ersätt MAUI-bas/Color med egna Id/Name/Level och ColorHex. Inga UI-anrop använder den gamla Color-egenskapen; MAUI:s `BeltDisplayHelper` finns kvar. |
| `Logic/CompetitorRepository.cs` | Extrahera den exakta parsningen och dess hjälpfunktioner till `CompetitorResultsParser`; behåll cache, filåtkomst och favoriter i appens repository. |
| `Logic/RankingService.cs`, `RankingPositionService.cs`, `SwedishNameComparer.cs` | Flytta till Core utan algoritmändringar. |
| `Logic/RankingClassFilterService.cs`, `ResultsFilterSettings.cs` | Flytta till Core med `IPreferenceStore`; MAUI-adapter anropar riktiga Preferences. |
| `Logic/CompetitionCatalogService.cs` | Flytta ren katalogbyggnad och typer till Core. Appens async-cache blir `CompetitionCatalogRepository`. |
| `Logic/ClubStatisticsService.cs` | Flytta ren statistik till Core; appens laddning blir `ClubStatisticsRepository`. |
| Övriga rena medalj-, klubb- och deltagartjänster | Flytta oförändrade till Core. |
| `Views/Summary/MedalSummaryRowModel.cs` | Flytta den rena modellen till Core med bevarat namespace. |
| `Logic/CompetitionMatchService.cs` | Extrahera JSON-parser/normalisering till Core; behåll paket- och filåtkomst i MAUI. |
| `WebConnection/ChagiDataManifest.cs` och kontrollsummelogik | Dela kontrakt och exakt LF/CRLF-hantering; separata lagringsadaptrar. |
| XAML, navigering, Preferences, FileSystem, bildresurser | Stannar i MAUI. Webbens Razor-vyer byggs separat. |

## Faser och verifiering

1. Skapa Core och koppla appens projektreferens. Kör befintliga resultatkontroller
   mot Core samt bygg Android/Windows där installerade beroenden tillåter.
2. Skapa fristående Blazor WASM: svenska, tillgängliga, responsiva listor,
   profilsidor, klassresultat och filtrerad ranking. Inga fiktiva resultat.
3. HTTP-manifest med selektiva nedladdningar och kontrollsummor, atomisk
   cacheuppdatering samt tydlig offline-status. Matchdata läses för samma
   deltagarunderlag som mobilen. Inga extra bilder hämtas vid start.
4. Valfria godkända bild-URL:er i separat presentationskonfiguration, lazy loading,
   fasta bildmått och initialer som fallback. Inga personbilder kopieras.
5. GitHub Pages-workflow, korrekt base-adress och dokumenterad 404-omdirigering
   som bevarar direktlänkar. README och faktisk verifieringsstatus.

## Miljöbegränsningar som upptäckts

- Sessionen får skriva i `D:\work\tkd-appen`, inte i önskade `D:\work\repos`.
  Projektet förbereds därför i `D:\work\tkd-appen\chagi-web` utan absoluta
  beroenden till originalkatalogen.
- HTTP-anrop mot GitHub Pages och NuGet misslyckas med DNS-fel i sessionen.
  Live-CORS och nya NuGet-beroenden kan därför ännu inte verifieras.
- .NET SDK 9.0.318 finns installerad och matchar appens befintliga SDK-policy.
