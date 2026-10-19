# Developer feeds — October 19, 2026

This records developer-feed snapshot `e509026e`; the later [M4A continuation](windows-audio-budget-checkpoint.md) records the newer source while preserving this evidence.

Vela remains an unreleased 0.2.2 preview. The retained `appUpdates` residual now has an on-demand **Developer feeds** tool. It reads release headlines from user-configured RSS 2.0 and Atom 1.0 sources. It does not scan installed applications, compare versions, download installers or replace Microsoft Store/winget updates. Sources default to empty; existing profiles do not enable the new tool automatically.

Source snapshot **`e509026e-c631-411e-aa02-5a4827387f05`** has **123 compile inputs**, following MP4 snapshot `b627a158`. The physical x64 Dell passes **173 core checks** (16 new feed checks) and **18 Windows backend checks**. Native x64 XAML/OCR publishing and ARM64 cross-publishing pass with zero compiler warnings/errors. ARM64 execution is not tested on this laptop. The previous MP4, JPEG, command search and lifecycle evidence remains historical evidence for its named snapshots.

## Behavior and bounds

The native form saves/edits/removes up to 16 named HTTP(S) feeds, refreshes on request, filters titles/source names locally, and shows the selected article URL before a separate Open action. Feed URLs are stored in settings and included in exports; fetched headlines stay only in the tool window. No source is fetched on save or startup. There are no notifications or scheduled polls.

At most two feeds run concurrently, each with a 20-second deadline and a 1 MiB response limit, including responses without Content-Length. Redirects are manually limited to three HTTP(S) hops; embedded credentials, unsupported schemes and HTTPS-to-HTTP redirects are rejected. The production client disables cookies/default credentials and bounds response headers. Gzip/deflate/Brotli decoding is enabled, with the byte limit applied to decoded data. Feed servers receive the requesting IP; private or token-bearing URLs remain the user's responsibility when sharing exported settings.

XML document types/external entities are prohibited. XML nesting, document size, examined items, title length, per-source items (100) and combined unique articles (200) are bounded. Only headline text, dates and usable web links are retained. RSS permalink GUID fallback, Atom alternate HTML links and inherited XML bases are supported. Dates are sorted newest first, with unknown dates last; article fragments do not create duplicate rows. Feed content is displayed as text. Article bodies, images and enclosures are never fetched by Refresh. Opening a selected link invokes the user's browser explicitly.

Failures are named per source while successful sources remain readable. Cancellation keeps the previous headline set. Closing the tool, disabling it or exiting the app requests cancellation through the existing tracked-operation lifetime.

## Actual verification

Core tests cover RSS/Atom parsing, relative/XML-base links, dates and Unicode; HTML-like title text; invalid links and duplicate fragments; GUID fallback; malformed/DTD/deep/oversized XML; normalization and old/null settings; partial failures, order and aggregation limits; bounded redirects and body reads; pre-request/request cancellation; per-source timeout handling; and two-source concurrency. Network tests use an injected HTTP handler and do not contact external services.

Physical x64 UI tests use a fresh `--data-directory` profile and generated HTTP fixtures bound only to `127.0.0.1`. Empty refresh and a `file:` address produce red validation errors without requests. Saving RSS, Atom and unavailable-source fixtures performs no fetch. Explicit Refresh displays two RSS headlines, then three combined RSS/Atom headlines in date order, alongside a named red HTTP 503 failure and a truthful 2/3 source count. Scrolling reaches all results and article details at the unchanged 150% scaling. Filtering to “newer” and selecting the article shows the full local URL without requesting the article. Editing the selected feed updates it without adding a duplicate. Active refresh disables source editing; Cancel closes the delayed local response, restores controls and retains the previous headlines. A separate refresh reaches the actual 20-second deadline: the slow response closes, timeout and HTTP 503 errors are named, and the successful Atom headline remains (1/3 feeds read). Closing the tool during another active delayed refresh also closes that response and removes the tool window. The resident panel still activates normally, and General → Exit ends the isolated app.

External developer endpoints, authentication, RSS 1.0/RDF, JSON Feed, localization, Narrator/high contrast, mixed DPI and large real-world feed collections are untested or unsupported. The Open browser action and removal through the UI were not exercised while the user was away. No claim of an installed-version update checker or a completed port is made. Remaining work stays in [the audit](windows-feature-audit.md).

## Evidence and preservation

`artifacts/developer-feeds-2026-10-19/` holds native build/backend logs, bounded UI observations and private validation fixtures. An initial C# build failed on an unsupported StackPanel property; the corrected build is the verified source above. Early core checks exposed unhandled size/redirect rejections; explicit per-feed handling fixed them before final validation. These failed attempts are preserved in the local evidence rather than counted as passes.

Normal settings, startup registration, the five accepted taskbar-cue files, Mica configuration and all 77 audit decisions are preserved. The runtime catalog grows from 30 to 31 implemented tools, with the new optional feed reader confined to the previously retained residual. No installer, archive, release, remote repository or public upload was created.

Implementation references: [Atom RFC 4287](https://www.rfc-editor.org/rfc/rfc4287), [RSS 2.0 specification](https://www.rssboard.org/rss-specification), [Microsoft XML DTD processing](https://learn.microsoft.com/en-us/dotnet/api/system.xml.xmlreadersettings.dtdprocessing?view=net-10.0), and [manual HTTP redirect configuration](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpclienthandler.allowautoredirect?view=net-10.0).
