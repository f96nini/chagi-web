# Kompletta tävlingsdeltagare

Appens resultatformat använder `0. Namn - Klubb` för deltagande utan rapporterad
placering. Raden räknas som en tävlingsstart men ger inga medaljer eller poäng.
Placering 1–3 och äldre resultatfiler fungerar som tidigare.

Appen läser också deltagarna direkt från detaljerad matchdata i `matches/`.
Alla namngivna deltagare på röd och blå sida tas med, även i individuella försök
och matcher utan poäng. Upprepade matcher räknas som en start per klass.
Medaljresultat kompletterar deltagandet med placering och poäng. Cache byggs om
när deltagarunderlaget ändras. Äldre tävlingar utan matchdata använder resultatfilerna.

Resultatfilen kan dessutom kompletteras för äldre appversioner med
`participants.json`, `placements.json` och `categories.json`:

```powershell
./scripts/Import-CompetitionParticipants.ps1 `
  -SnapshotDirectory 'D:/work/repos/chagi-files/chagi/results/competitions/torslanda-2026-09-26/2026-09-26_16-30-36' `
  -ResultFile 'D:/work/repos/chagi-files/chagi/results/2026-09-26_torslandacupen.txt' `
  -ManifestPath 'D:/work/repos/chagi-files/chagi/manifest.json'
```

Resultatfilen ska redan innehålla tävlingens namn, datum, plats, kategorier och
medaljresultat. Importen kontrollerar att medaljerna stämmer, slår ihop upprepade
deltagar-ID:n inom samma kategori och kompletterar med övriga deltagare. Samma
import kan köras flera gånger utan att skapa dubbletter. Lagposter behålls som
lagposter; underlaget anger inte lagens individuella medlemmar.

Publicera den uppdaterade resultatfilen och manifestet tillsammans i `chagi-files`
för att äldre appversioners synkning ska hämta deltagarna. Detaljvyns nya visning kräver
också den uppdaterade appversionen. Äldre tävlingar där endast medaljresultat finns
kan inte kompletteras med okända deltagare.

## Matchdetaljer

Exportera `matches.json` från samma färdiga snapshot med
`Export-CompetitionMatches.ps1`. Ange `-SnapshotDirectory`, `-ResultFile` och
`-DataDirectory` (katalogen `chagi` i datarepot). Med
`-BundleDirectory ./Resources/Raw/matches` följer datan även med appen offline.

Exporten skapar `matches/index.json` och en JSON-fil per tävling och uppdaterar
synkmanifestets checksummor. Publicera dessa filer tillsammans med manifestet.
Matchsidan matchar tävlingens datum och namn samt klassens fullständiga namn,
inklusive bältesgrupp. Framtida tävlingar kan därför läggas till via datasynkning.
Poäng och flaggor är separata värden; decimalflaggor och extramatcher bevaras.
