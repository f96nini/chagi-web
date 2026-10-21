# Cloudflare Web Analytics: verifiering

## Genomfört lokalt

- `./scripts/Test.ps1` passerade, inklusive kontroll av läsbara fighteradresser
  och stöd för tidigare Base64-kodade länkar.
- `dotnet publish Chagi.Web/Chagi.Web.csproj -c Release -o artifacts/site`
  passerade.
- `node scripts/prepare-pages.mjs artifacts/site/wwwroot /chagi-web/`
  passerade. Den förberedda `index.html` har rätt `<base>`-adress och exakt en
  Cloudflare-beacon med token från webbplatsägaren.
- Ingen separat Content Security Policy finns i webbprojektet att ändra.

## Kontroll efter publicering

1. Öppna `https://f96nini.github.io/chagi-web/` i en vanlig webbläsare.
   Upprepa gärna i Safari på iPhone.
2. I utvecklarverktygens Network-panel, kontrollera att
   `https://static.cloudflareinsights.com/beacon.min.js` laddas en gång.
3. Kontrollera POST-anrop till `https://cloudflareinsights.com/cdn-cgi/rum`.
   Om anrop uteblir, kontrollera Console, eventuell CSP och annonsblockerare.
4. Navigera via länkar från startsidan till tävlingar, fighters, en
   fighterprofil och klubb. Kontrollera att SPA-navigering rapporterar
   sidvisningar utan dubletter. Lämna gärna fliken efter sista navigeringen;
   Cloudflare kan skicka den sista sidans mätning då.
5. Kontrollera att en ny fighterprofil har en sökväg av formen
   `/chagi-web/fighters/test-alpha--testklubb-<kontrollvärde>`, så att namn
   och klubb syns i Cloudflares Path-statistik. Äldre sparade länkar med
   Base64-ID fungerar fortfarande men ger en kodad sökväg i statistiken.
6. Kontrollera senare att besök och sidvisningar syns i Cloudflares dashboard.
   Rapporteringen kan dröja; historiska besök före installationen tillkommer
   inte.

Cloudflares beacon mäter normalt SPA-navigering automatiskt. Ingen egen
sidvisningskod har lagts till, för att undvika dubbelrapportering.

Resultatet av kontrollen på den publika sidan och i Cloudflares dashboard
behöver noteras efter driftsättning.
