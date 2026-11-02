# Show Translation Keys

A debug tool for RimWorld translators. It replaces translated text with the
raw translation key (`<<Command_ColonistDraft>>`), so you can visually map
each key to the UI element that uses it.

Instead of grepping XMLs and guessing which button is which key, enable this
mod, open the screen, and read the keys directly off the UI.

## Features

- Wraps every translated string with the raw key on `Translator.Translate`,
  `Def.label`/`LabelCap` (including subclass overrides) and
  `BackstoryDef.baseDesc`.
- Target-key highlighting (`>>key<<`) so a specific key stands out on screen.
- Stack-trace logging, once per location, so you know which `Window` /
  `MainTabWindow` class used a key.
- Auto-detected UI class + configurable hotkey (default `F10`) to reopen it.
- UI catalog: scans every `Window` in the running game and lets you open any
  of them with a click.
- Context generator: spawns a pawn/item/creature carrying the Def you want to
  inspect (backstory, trait, hediff, gene, pawnkind, thing, precept, research,
  tale).
- Art/tale support: sculptures spawn with a random tale and can be rerolled
  until the target tale appears.
- Prefix filter (e.g. only wrap `Command_*` keys), wrapper style toggle
  (`<< >>` or `[[ ]]`).
- Fully localized settings UI (English, Portuguese/Brazil).
- Silent UTF-8 encoding check on startup: if the mod's own translation files
  are saved with the wrong encoding, the log warns you.

## Requirements

- **RimWorld 1.6**
- **Harmony** (brrainz.harmony)

## Installation

1. Install **Harmony** and enable it **above** this mod.
2. Copy the mod folder into `RimWorld/Mods/ShowTranslationKeys/`.
3. Ensure the structure is:
```

ShowTranslationKeys/
├── About/About.xml
├── Assemblies/ShowTranslationKeys.dll
└── Languages/...

```
4. Enable **Show Translation Keys** after Harmony in the mod list.

## Building from source

Requires **.NET SDK 8+** (no Visual Studio needed).

### Windows
```bat
build.bat
```

### Linux / macOS

```
./build.sh
```

The first build downloads the RimWorld reference assemblies via NuGet.

## Usage

### See all keys

Enable the mod. Every translatable string becomes `<<key>>`. Disable
`Show translation keys in UI` to return to normal text; target-key detection
keeps running in the background.

### Track a specific key

Paste the key into **Target key**. It will appear as `>>key<<` wherever the
game renders it, and the mod will log (once per location) the C# stack trace
that produced it.

### Reopen the same screen

The mod remembers the `Window`/`MainTabWindow` class that used your target
key. Press **F10** (configurable) to reopen it from anywhere.

### Force-context for a Def

Type a DefName into **DefName** and press **Generate context**. The mod spawns
a pawn or item configured with that Def and selects it. Supported Def types:
`BackstoryDef`, `TraitDef`, `HediffDef`, `PawnKindDef`, `ThingDef`, `GeneDef`,
`PreceptDef`, `ResearchProjectDef`, `TaleDef`.

Genes are applied as **xenogenes**, so they override endogenous genes (useful
when a gene would otherwise be masked by an existing one).

### Narrow things down

- **Key prefix filter** — e.g. `Command_` to only wrap buttons.
- **Wrapper style** — `<< >>` or `[[ ]]`.
- **UI catalog** — scan every `Window` in the running game and open any of them
with a click.

## Known limitations

- **Gizmos** (`Command_*`) only render when a pawn is selected — they are not
force-openable windows.
- **Rule packs** (like `subject_story` in `ResearchProjectDef`) generate text
dynamically and bypass `Translator.Translate`. Translate them through the
XML `DefInjected` files instead.
- Some `Page_*` classes depend on in-game state and cannot be opened from the
settings window (`Object reference not set` in the log).
- `CompArt.JustCreatedBy` requires at least one living colonist in the current
map to record a tale.
- The mod makes the game **unreadable on purpose** while key wrapping is on.
Disable `Show translation keys in UI` when not translating.

## Translating the mod

All strings live in `Languages/English/Keyed/ShowTranslationKeys.xml`. To add a
language, copy that file to `Languages/<LanguageName>/Keyed/ShowTranslationKeys.xml`,
translate the values, and submit a PR. Keep the `<STK_*>` tags untouched.

**Save XML files as UTF-8 without BOM.** Saving as ANSI/Windows-1252 produces
mojibake (`traduÃ§Ã£o`) and the mod will warn about it in the log.

