# String Tables

All text the player reads (menu labels, unit names and descriptions, mission names and goals, the adventure briefing,
the credits) lives in plain-text string tables in `Locale_en.pac`. Keeping text out of the code and out of the
level files let the game be translated by swapping one archive: the `_en` suffix on the archive and on every file
name marks the language.

| File              | Contents                                                                    |
|-------------------|-----------------------------------------------------------------------------|
| `BRIEFING_EN.TXT` | The adventure briefing, one entry per page (`BriefingTitle01`…`14`)         |
| `CREDITS_EN.TXT`  | Credits pages (`Page1Title1`, `Page1Names2`, …)                             |
| `ETC_EN.TXT`      | UI labels and messages, keyed by the name of the UI element that shows them |
| `KBD_EN.TXT`      | Keyboard shortcuts (`Kbd Escape`, `Kbd A`, …)                               |
| `SPACES_EN.TXT`   | Names of the compounds and missions, and each mission's goal text          |
| `UNITS_EN.TXT`    | Name and description of every unit (`cu0001 name`, `cu0001 desc`)           |

## Layout

Each file is a list of entries. An entry is a tag line with a key between double equals signs, followed by the value
on the next lines:

```
####
#
# Assorted UI Strings
#
####

==MenuAdventureButton==
Adventure

==cu0001 desc==
The Belt is used to connect motors to belt-driven equipment. It attaches to red belt-wheels.
```

The files are Windows-1252 text with CRLF line endings. The only character outside ASCII in the English files is
`’` (byte `0x92`), in the briefing.

### Keys

- A tag line starts with `==` and has a second `==` somewhere after it. The key is the text between them and may
  contain spaces and punctuation (`==Unit's rope attachment already in use==`). Anything after the closing `==` is
  ignored.
- Keys are case-insensitive: the game uppercases them on load.
- The game loads every `.TXT` in the archive into one shared table. If a key appears twice, in the same file or in two
  different files, the first one wins and the later one is ignored. The shipped files have no repeated keys.
- Lines before the first tag are ignored, which is where the files keep their header comments.

### Values

- A value is every line after the tag line, up to the next line that starts with `==`. Lines are joined with a
  line feed.
- Lines starting with `#` are comments and are left out of the value.
- Blank lines are kept. The shipped files put a blank line between entries, so most values end with a line feed
  (`"Adventure\n"`). This toolkit keeps it, to match what the game gets back. Most tools will want to trim it.
- A line starting with `==` always ends the value, even when it isn't a valid tag (for example `====`). The lines
  after it belong to no entry until the next valid tag.

### Escapes

A backslash starts an escape sequence, both in values and in tag lines:

| Escape         | Result                                                                  |
|----------------|-------------------------------------------------------------------------|
| `\n`           | Line feed                                                               |
| `\t`           | Tab                                                                     |
| `\xHH`, `\XHH` | The byte with hex value `HH`. Exactly two characters are consumed; one that isn't a hex digit counts as `0` |
| `\` at line end | Joins the line with the next one                                       |
| `\` + anything else | That character (`\\` is a backslash)                               |

Carriage returns are dropped before escapes are handled. That's why `\` at the end of a line still works in a file with
CRLF line endings, and why a carriage return can only be put in a value as `\x0D`.

`KBD_EN.TXT` is the only file that uses escapes: it maps shortcut names to Windows virtual-key codes
(`==Kbd Escape==` → `\x1B`, `==Kbd Up==` → `\x26`). Letter shortcuts are written as the letter itself.

## How keys are used

The keys tie the text to the rest of the game's data:

- `UNITS_EN` keys start with a unit ID (`cu0001`, `b0001`, `og0004`, `u0179`), the same ID used for the unit's model
  in `Fin.pac`, its behaviour DLL in `Bhvr.pac` and its placements in the `.PUZ` level files.
- `SPACES_EN` keys start with a mission ID (`M1020 name`, `M1020 goal`) matching the level file `M1020.PUZ`.
- Many `ETC_EN` keys are the names of buttons in `UILAYOUT.TXT` (`MenuAdventureButton`), so a button's label can
  be found from its name. This is inferred from matching names; not every button has an entry.
