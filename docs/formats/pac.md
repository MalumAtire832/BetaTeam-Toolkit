# PAC Archives

`.pac` files are uncompressed archives that hold almost all of the game's data. The game never extracts them:
it memory-maps the whole archive and reads files straight out of that mapped view.

## Overview

```
┌──────────────────────┐ 0x00
│ Header (16 bytes)    │
├──────────────────────┤ 0x10
│ Directory            │  file entries, then sub-directories (recursive)
├──────────────────────┤ header.DirectorySize
│ File data            │  contents of each file, stored back to back
└──────────────────────┘ EOF
```

## Header

| Offset | Type   | Name          | Description                                                   |
|--------|--------|---------------|---------------------------------------------------------------|
| 0x00   | char[4]| Magic         | Always `PACK`. The only field the game checks.                |
| 0x04   | u32    | ArchiveSize   | Total file size in bytes. The game ignores it.                |
| 0x08   | u32    | Unknown       | Always 0. The game ignores it.                                |
| 0x0C   | u32    | DirectorySize | Where the directory ends, counted from the start of the file. |

The game reads the directory as a stream that starts at `0x10` and is `DirectorySize` bytes long. The last directory
byte always comes right before the first file's data, so in practice `DirectorySize` is also where the file data
starts. Readers shouldn't rely on that, though: every entry stores its own absolute offset.

`ArchiveSize` and `Unknown` were probably written by the packing tool for its own bookkeeping. `Unknown` may be the
high half of a 64-bit size (see *Offsets* below), but nothing confirms this.

## Directory

The directory is a tree. Each directory node lists its files first, then its sub-directories, and each
sub-directory is itself a full directory node:

```
Directory
  u32        FileCount
  FileEntry  × FileCount
  u32        SubDirectoryCount
  SubDirectory × SubDirectoryCount

FileEntry
  char[]     Name        null-terminated, at most 63 characters
  u32        OffsetHigh
  u32        OffsetLow
  u32        Size
  FILETIME   LastWriteTime

SubDirectory
  char[]     Name        null-terminated, at most 63 characters
  Directory              (the nested directory node)
```

A file in a sub-directory is addressed as `SUBDIR\FILE.EXT`. The game accepts both `\` and `/` as separators.

None of the shipped archives use sub-directories. Every one of them has a flat root directory, so the root's
`SubDirectoryCount` is always 0. A reader that ignores sub-directories will still read the shipped files correctly,
but it will get the structure wrong.

### File Names
Names are stored upper-case, and the game upper-cases a name before looking it up, so lookups are
case-insensitive, just like the Windows file system. Names are limited to 63 characters plus the null terminator.
Longer names would break the directory read.

### Offsets

A file's offset is stored as two 32-bit halves, high half first:

```
offset = (OffsetHigh << 32) | OffsetLow
```

The high half comes first because that's the order Win32 APIs use. `MapViewOfFile` takes its offset as separate
`dwFileOffsetHigh` and `dwFileOffsetLow` parameters, and the game passes these two fields to it unchanged.
Because of this, the fields are not a normal little-endian `u64`. Reading them as one gives the wrong value for any
offset above 4 GB. That never happens in practice (the largest shipped archive is about 120 MB, and Windows 9x
file systems couldn't hold files that large anyway), so `OffsetHigh` is always 0.

### Timestamps

Every entry has the file's last-modified time as a Windows `FILETIME`. This isn't just metadata: the game uses it
to decide which copy of a file wins when there are several (see below).

## How the game uses archives

At startup the game registers all of its archives. When it opens a file by name:

1. It searches every registered archive and picks the matching entry with the newest timestamp.
2. It then checks whether a loose file with the same name exists on disk.
3. If the loose file is newer than the archived copy, it reads the loose file. Otherwise it reads from the archive.

This is effectively a patching system: a newer archive or a newer loose file replaces an older one, and nothing
has to be repacked. During development it would have let artists drop updated assets next to the game and see
them straight away.
This behaviour comes from reading the game's code and hasn't been tested on a running game, but it suggests that
modded files placed next to the archives with a recent modification date will override the originals.

## Quirks

- **Self-references.** `Ambient.pac` and `LMap.pac` each contain an entry for the archive itself (`AMBIENT.PAC`,
  `LMAP.PAC`) with offset 0, size 0 and timestamp 0. The packing tool most likely swept up its own half-written
  output file. These entries can be safely skipped.
- **No padding or alignment.** File data is stored back to back in directory order with no gaps. The last file
  ends exactly at the end of the archive.
- **No compression or checksums.** Every file is stored byte-for-byte.
