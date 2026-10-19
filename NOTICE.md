# Attribution and identity

Vela is an independent, unofficial Windows port inspired by [Vorssaint](https://github.com/vorssaint/vorssaint-utils), copyright 2026 Vorssaint. The source feature inventory and four-step onboarding flow were studied at commit `794608a58d8a88608d3c3cb2e4f22373cf52bfdc`. Windows implementation code and the Vela icon are newly authored for this port.

This project is distributed under GPL-3.0-or-later; see [LICENSE](LICENSE). Microsoft Windows, WinUI and VMware Fusion are their respective owners' trademarks. Refer to the NuGet dependencies for their own licenses.

Upstream's [trademark policy](https://github.com/vorssaint/vorssaint-utils/blob/794608a58d8a88608d3c3cb2e4f22373cf52bfdc/TRADEMARKS.md) requires unofficial forks to use a different name, icon, identity and distribution channel. Vela does not reuse the Vorssaint logo, bundle identity, signing identity, update feed or trade dress, and is not an official Vorssaint release. No upstream app files, preferences, notes or recordings were modified or migrated.

The offline English OCR worker uses Charles Weld’s [Tesseract .NET wrapper 5.2.0](https://github.com/charlesw/tesseract), Tesseract OCR 5.2.0 and tessdata_fast (Apache-2.0), Leptonica 1.82.0 (BSD), and InteropDotNet (MIT). Included OCR license files are in `Ocr/Licenses` in builds and `src/Vela.OcrWorker/Licenses` in source. The pinned model URL and SHA-256 are recorded in `src/Vela.OcrWorker/tessdata/provenance.json`. The worker needs the Microsoft Visual C++ x64 runtime; that Microsoft component is not redistributed by this project.
