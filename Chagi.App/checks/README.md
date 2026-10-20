# Background work checks

This is historical documentation from the source app. In the extracted Chagi Web
project these checks reference `Chagi.Core`. The current run, mobile build results
and remaining browser/deployment limits are recorded in
[`../../docs/VERIFIERING.md`](../../docs/VERIFIERING.md). Run all current checks from
the project root with `./scripts/Test.ps1`.

The latest 2026-10-19 edits were checked for C# syntax and well-formed XML only;
no build or executable test run was performed, as requested by the user.
Additional ClubDetails checks cover unique club fighter counts, result-only clubs
(including Skurup and Mariestad), Swedish sorting under an English device locale,
and separate medal-count and 5/3/1 points leagues. The fighter summary components
now use trophies and centered content; fighter-list medal icons are unchanged.

Run from the application directory:

```powershell
dotnet run --project checks/BackgroundWorkChecks.csproj
dotnet run --project checks/MedalLeaderboardChecks.csproj
dotnet run --project checks/HeadToHeadChecks.csproj
dotnet run --project checks/BeltClassificationChecks.csproj
dotnet run --project checks/DataSyncChecks.csproj
dotnet run --project checks/NavigationChecks.csproj
dotnet build ITF-Taekwondo-Gradering.csproj -f net9.0-android --no-restore
```

The checks run the production progress coordinator and head-to-head service with
a single-thread synchronization context and small UI/data stand-ins. They verify
worker execution even when data tasks complete synchronously, UI dispatch,
overlapping operations, exception propagation, cancellation, and progress cleanup.
They do not measure native rendering or Android frame times.

VS checks also cover historical podium inference across all four disciplines,
shared bronzes, duplicate results, actual-match precedence, winner-only matches,
Swedish first-name ordering and opponent totals across disciplines and club aliases.
On 2026-10-19 all 40 head-to-head checks and 17 background-work checks passed,
as did Android and Windows builds. Windows UI verification confirmed the Medaljer
and Badges tabs, collapsed discipline lists, alphabetical opponents, combined
opponent totals, and discipline-grouped details. Each meeting now names its winner
in bold, with an explicit podium-placement qualifier for inferred outcomes. Detailed
matches also retain match points and flags.

The medal leaderboard checks build production competition catalogs and verify
5/3/1 scoring, aggregation across disciplines, tied podium positions, Swedish
surname ordering, separate identities, zero-point participants and empty lists.
They also check competition age labels without gender, multiple age classes for
one fighter, and expansion of non-medallists only in groups with more than four
participants, preserving every tied medal winner and the full participant count.

The head-to-head checks cover Folke Beecken's six Stenungsund meetings, red/blue
participants, opponent grouping, distinct clubs and placement comparisons. They
also run the real repository, match parser and persistent cache to verify that
historical Billdal results use GBG Kampsport and old favorite selections survive.

The belt classification checks import Karl Anefors's Stenungsund classes and
verify the white-belt ranking placement. Single-color classes take precedence
over combined classes on the latest competition date for all colors and Kup/Dan
labels. Source order, match-only participants, cache repair and advancement at a
later competition are covered with the real repository and ranking services.

Data sync checks use an in-memory HTTP server, controlled clock and isolated disk
storage. They verify persisted throttling, ETag/304, selective file downloads,
legacy installation migration, checksum failures, cancellation, offline use,
atomic snapshot publication, removals, local repair and foreground-only polling.
GitHub Pages LF versus publisher CRLF (including a final PowerShell CRLF) is
verified against those exact representations; downloaded bytes are preserved.
UI refresh checks cover visible and hidden pages, retained selections and
overlapping loads.

The test source uses `.cs.test` and generated output lives outside the application
directory so the MAUI project's default recursive C# glob does not include tests.

## Adding heavy work

- For a page with an existing spinner, enable it before awaiting the data service
  and disable it in `finally`, after applying the result.
- Otherwise use `PageWorkService.RunAsync(page, workflow, status)` around the
  complete workflow. The workflow runs on the UI thread; explicitly use `Task.Run`
  for synchronous computation. Apply results to controls after the await.
- `PageWorkService.RunBackgroundAsync` dispatches a data-only delegate to a worker
  and manages progress until it completes. Never create or update controls there.
- Data-service entry points dispatch CPU-heavy parsing and aggregation to workers.
  `async` and `ConfigureAwait(false)` alone do not move synchronously completed
  cached operations off the calling thread.
- Keep unavoidable control creation in short batches. CollectionView should be
  preferred for long lists. Navigation uses `NavigationUiService` on the UI thread;
  loading indicators must not add modal pages to the navigation history.

The navigation checks cover root fallback, local back versus home, modal cleanup,
overlapping transitions, late results, exception recovery, navigation parameters,
page cancellation, the default H/D filter, every saved gender/discipline, invalid
saved values and an empty class selection persisted across restarts. The UI
stand-ins do not substitute for the native Android checks below.

Android regression checks:
- Native back from every page, including match details, goes directly to Home.
- Another native back on Home finishes the activity.
- The single in-app back arrow returns one level and preserves filters and scroll.
- Exercise back during cold loads and repeated quick taps on competition links.
- Uncheck every class, return to results, restart, then select a class again.
- Cancel settings, including theme preview, via Cancel and native back; verify
  that only Save persists edits and both buttons return Home.

Handled navigation errors and unhandled failures are recorded locally in
`FileSystem.AppDataDirectory/navigation-errors.log` (one rotated backup, no upload).
The corresponding `navigation-events.log` records the last routes even if a
native crash terminates the process before managed error handlers can run.

Verified on 2026-10-17 with an Android 15 / API 35 x64 emulator and a separate
debug application ID: native back while opening sparring details goes Home, the
next native back finishes the activity, the edge back gesture also goes Home,
and the in-app arrow returns to competition results. Unchecking all five classes
produced an empty results view without crashing and remained empty after process
restart, including Fighters. Settings Save, Cancel, theme preview and discarding
edits through native back were checked. No navigation errors or native crashes
were recorded during these flows. All 49 automated assertions passed. Physical
Android devices and release builds still require device validation.

On Android, manually exercise cold and cached loads, filters/search, fighter tabs,
match details, clubs, calendar, study/quiz, and manual synchronization. Verify that
progress animates, navigation stays responsive, and errors leave no stuck overlay.

Saved result filters were verified on the Android 15 / API 35 test emulator:
first launch selects H/D; gender and discipline follow changes between Fighters,
All fighters and competition details. Force-stopping and relaunching the process
restores gender, discipline, a Kadetter-only class selection and a favorite star.
A second restart preserves an explicitly empty class selection, also in All
fighters. No application crashes were recorded during these checks.

Automatic data synchronization was verified on 2026-10-17 on the isolated Android
15 / API 35 emulator against the actual GitHub Pages source. A clean installation
downloaded 25 files (598,912 bytes). Returning to the foreground and restarting the
process within fifteen minutes both skipped network requests. Manual sync used
ETag/304 with zero data files downloaded. In a deliberate local corruption test,
the next due automatic check received 304, repaired only the damaged result file
(31,167 bytes), and updated the already-open Stenungsund Medaljtoppen from zero
to the correct 18-point leaders without leaving the page. The newly available
venue information was also applied. No server files were changed for this test.
The 31 data-sync assertions and 26 head-to-head/identity assertions passed.
Opening a fighter and VS after synchronization, native back to Home, and another
native back to exit the activity were also verified. No managed application
errors or native crashes were recorded in these flows.
