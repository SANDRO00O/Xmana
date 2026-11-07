# Xmana Localization

- All UI text resolves through `LocalizationService` (`Xmana.Core/Localization`).
- Keys are strongly typed constants in `K.cs`; translations live in `StringsEn.cs` / `StringsAr.cs`.
- XAML binds text with `xmana:Loc.Key="nav.home"`; text refreshes live when the language changes.
- Arabic switches the whole window to `FlowDirection.RightToLeft` (one layout system, no second UI);
  English switches back to LeftToRight. No restart is required.
- The selected language is stored locally (`LocalSettings`, key `ui.language`). On first launch the
  device language is used (Arabic if it starts with `ar`, otherwise English).
- `tools/validate_localization.py` (run in CI) fails the build if a key is missing in either language.
- UI font: Segoe UI (Arabic + Latin, shaping and diacritics). Wordmark: Inter Bold (see `assets/branding/Inter-OFL.txt`).
- Controller button names (A, B, LB / RB) are kept left-to-right inside RTL layouts; their labels are localized.

Adding a language: add a `Strings<Xx>.cs` catalog, register it in `LocalizationService.CreateDefaultCatalogs`,
add the code to `LanguageCodes`, and a choice in Settings. No screen code changes.
