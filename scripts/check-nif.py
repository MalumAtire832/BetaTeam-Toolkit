#!/usr/bin/env python3
"""Check NIF files against nif.xml, the block definitions NifSkope reads NIF files with.

Every block is read field by field as nif.xml describes it for the file's version. A file passes when every block
type is known, the footer ends exactly at the end of the file, and every link points at an existing block of the
type nif.xml expects.

Usage: scripts/check-nif.py <nif.xml> <file-or-directory>... [--tree]

nif.xml is at https://github.com/niftools/nifxml. Only versions 3.3.0.13 to 4.2.2.0 are supported, where each block
starts with its type name; that covers the files `betateam convert fin nif` writes.
"""

import os
import re
import struct
import sys
import xml.etree.ElementTree as ET

BASIC_FORMATS = {
    "byte": "<B", "sbyte": "<b", "char": "<b", "ushort": "<H", "short": "<h", "uint": "<I", "ulittle32": "<I",
    "int": "<i", "uint64": "<Q", "int64": "<q", "float": "<f", "hfloat": "<e", "FileVersion": "<I",
    "Ref": "<i", "Ptr": "<i", "StringOffset": "<I", "NiFixedString": "<I", "BlockTypeIndex": "<h",
}

OPERATORS = [
    ("#AND#", " and "), ("#OR#", " or "), ("#LTE#", "<="), ("#GTE#", ">="), ("#LT#", "<"), ("#GT#", ">"),
    ("#EQ#", "=="), ("#NEQ#", "!="), ("#RSH#", ">>"), ("#LSH#", "<<"), ("#BITAND#", "&"), ("#BITOR#", "|"),
    ("#ADD#", "+"), ("#SUB#", "-"), ("#MUL#", "*"), ("#DIV#", "//"),
]


class CheckError(Exception):
    pass


def version_number(text):
    parts = [int(p) for p in text.split(".")]
    while len(parts) < 4:
        parts.append(0)
    return (parts[0] << 24) | (parts[1] << 16) | (parts[2] << 8) | parts[3]


class NifXml:
    def __init__(self, path):
        root = ET.parse(path).getroot()
        self.tokens = []
        for token in root.findall("token"):
            for entry in token:
                self.tokens.append((entry.get("token"), entry.get("string")))
        self.enums = {}
        self.structs = {}
        self.niobjects = {}
        for element in root:
            name = element.get("name")
            if element.tag in ("enum", "bitflags", "bitfield"):
                self.enums[name] = element.get("storage")
            elif element.tag in ("struct", "compound"):
                self.structs[name] = element
            elif element.tag == "niobject":
                self.niobjects[name] = element

    def inherits(self, name, ancestor):
        while name is not None:
            if name == ancestor:
                return True
            element = self.niobjects.get(name)
            name = element.get("inherit") if element is not None else None
        return False

    def niobject_fields(self, name):
        chain = []
        while name is not None:
            element = self.niobjects[name]
            chain.append(element)
            name = element.get("inherit")
        for element in reversed(chain):
            yield from element.findall("field")


class Reader:
    def __init__(self, xml, data):
        self.xml = xml
        self.data = data
        self.pos = 0
        self.version = 0
        self.links = []

    def take(self, size):
        if self.pos + size > len(self.data):
            raise CheckError(f"unexpected end of file at 0x{self.pos:X}")
        chunk = self.data[self.pos:self.pos + size]
        self.pos += size
        return chunk

    def unpack(self, fmt):
        return struct.unpack(fmt, self.take(struct.calcsize(fmt)))[0]

    def read_line(self):
        end = self.data.find(b"\n", self.pos)
        if end < 0:
            raise CheckError("header string has no line end")
        line = self.data[self.pos:end].decode("latin-1")
        self.pos = end + 1
        return line

    # Expressions use field names with spaces, nif.xml's operator tokens and version numbers
    def evaluate(self, expression, values, arg=None):
        # Tokens are listed so that expanding them in order leaves only operators and the version globals
        globals_ = {"#VER#": str(self.version), "#USER#": "0", "#BSVER#": "0"}
        operators = dict(OPERATORS)
        text = expression
        for token, replacement in self.xml.tokens:
            if token in text and token not in operators:
                text = text.replace(token, globals_.get(token, replacement))
        if arg is not None:
            text = text.replace("#ARG#", str(arg))
        for token, replacement in OPERATORS:
            text = text.replace(token, replacement)
        text = re.sub(r"\b(\d+\.\d+\.\d+\.\d+)\b", lambda m: str(version_number(m.group(1))), text)
        # A field name is a run of capitalised words; fields that weren't read (other versions, other games)
        # count as zero
        names = {}

        def name_to_variable(match):
            if match.group(0) == "INFINITY":
                return match.group(0)
            key = f"v{len(names)}"
            names[key] = values.get(match.group(0), 0)
            return key

        text = re.sub(r"(?<![\w.])[A-Z][A-Za-z0-9]*(?: [A-Z0-9][A-Za-z0-9]*)*(?![\w])", name_to_variable, text)
        text = text.replace("&&", " and ").replace("||", " or ")
        text = re.sub(r"!(?!=)", " not ", text)
        try:
            return eval(text, {"__builtins__": {}, "INFINITY": float("inf")}, names)
        except Exception as e:
            raise CheckError(f"can't evaluate {expression!r} as {text!r}: {e}")

    def applies(self, field, values, block_type, arg):
        since, until = field.get("since"), field.get("until")
        if since and self.version < version_number(since):
            return False
        if until and self.version > version_number(until):
            return False
        if field.get("versions"):
            return False
        if field.get("onlyT") and not self.xml.inherits(block_type, field.get("onlyT")):
            return False
        if field.get("excludeT") and self.xml.inherits(block_type, field.get("excludeT")):
            return False
        if field.get("vercond") and not self.evaluate(field.get("vercond"), values, arg):
            return False
        if field.get("cond") and not self.evaluate(field.get("cond"), values, arg):
            return False
        return True

    def read_fields(self, fields, block_type, arg, template, path):
        values = {}
        for field in fields:
            if not self.applies(field, values, block_type, arg):
                continue
            name = field.get("name")
            field_type = field.get("type")
            field_template = field.get("template")
            if field_type == "#T#":
                field_type = template
            if field_template == "#T#":
                field_template = template
            field_arg = self.evaluate(field.get("arg"), values, arg) if field.get("arg") else None
            length = self.evaluate(field.get("length"), values, arg) if field.get("length") else None
            width = self.evaluate(field.get("width"), values, arg) if field.get("width") else None
            where = f"{path}.{name}"
            if length is None:
                values[name] = self.read_value(field_type, block_type, field_arg, field_template, where)
            else:
                rows = []
                for i in range(int(length)):
                    if width is None:
                        rows.append(self.read_value(field_type, block_type, field_arg, field_template, f"{where}[{i}]"))
                    else:
                        rows.append([self.read_value(field_type, block_type, field_arg, field_template,
                                                     f"{where}[{i}][{j}]") for j in range(int(width))])
                values[name] = rows
        return values

    def read_value(self, field_type, block_type, arg, template, path):
        if field_type in ("Ref", "Ptr"):
            value = self.unpack("<i")
            self.links.append((path, field_type, template, value))
            return value
        if field_type == "bool":
            return self.unpack("<I" if self.version <= 0x04000002 else "<B")
        if field_type in BASIC_FORMATS:
            return self.unpack(BASIC_FORMATS[field_type])
        if field_type in ("HeaderString", "LineString"):
            return self.read_line()
        if field_type in self.xml.enums:
            return self.read_value(self.xml.enums[field_type], block_type, arg, template, path)
        if field_type in self.xml.structs:
            fields = self.xml.structs[field_type].findall("field")
            return self.read_fields(fields, block_type, arg, template, path)
        raise CheckError(f"{path}: unknown type {field_type}")


def check(xml, path, show_tree):
    with open(path, "rb") as f:
        data = f.read()
    reader = Reader(xml, data)
    header_string = reader.read_line()
    match = re.search(r"Version ([\d.]+)$", header_string)
    if not match:
        raise CheckError(f"not a NIF header: {header_string!r}")
    reader.version = version_number(match.group(1))
    if not 0x0303000D <= reader.version <= 0x04020200:
        raise CheckError(f"version {match.group(1)} isn't supported by this checker")

    header = reader.read_fields(xml.structs["Header"].findall("field")[1:], None, None, None, "Header")
    if header["Version"] != reader.version:
        raise CheckError(f"header string says 0x{reader.version:08X}, version field 0x{header['Version']:08X}")

    types = []
    for index in range(header["Num Blocks"]):
        start = reader.pos
        length = reader.unpack("<I")
        block_type = reader.take(length).decode("latin-1")
        if block_type not in xml.niobjects:
            raise CheckError(f"block {index}: unknown type {block_type!r}")
        if xml.niobjects[block_type].get("abstract") == "true":
            raise CheckError(f"block {index}: {block_type} is abstract")
        types.append(block_type)
        values = reader.read_fields(list(xml.niobject_fields(block_type)), block_type, None, None, f"[{index}] {block_type}")
        if show_tree:
            name = values.get("Name")
            name = name.get("String", {}).get("Value") if isinstance(name, dict) else None
            label = bytes(c & 0xFF for c in name).decode("latin-1") if name else ""
            print(f"  [{index}] 0x{start:X} {block_type} {label}".rstrip())

    footer = reader.read_fields(xml.structs["Footer"].findall("field"), None, None, None, "Footer")
    if reader.pos != len(data):
        raise CheckError(f"{len(data) - reader.pos} bytes left after the footer")

    for where, kind, template, value in reader.links:
        if value == -1:
            continue
        if not 0 <= value < len(types):
            raise CheckError(f"{where}: {kind} {value} is outside the {len(types)} blocks")
        if template and not xml.inherits(types[value], template):
            raise CheckError(f"{where}: links a {types[value]}, expected {template}")

    return len(types), len(footer["Roots"])


def main(argv):
    arguments = [a for a in argv[1:] if not a.startswith("--")]
    if len(arguments) < 2:
        print(__doc__.strip())
        return 2
    xml = NifXml(arguments[0])
    show_tree = "--tree" in argv

    paths = []
    for argument in arguments[1:]:
        if os.path.isdir(argument):
            for directory, _, files in os.walk(argument):
                paths.extend(os.path.join(directory, f) for f in sorted(files) if f.lower().endswith(".nif"))
        else:
            paths.append(argument)

    failed = 0
    for path in paths:
        try:
            if show_tree:
                print(path)
            blocks, roots = check(xml, path, show_tree)
            print(f"ok    {path}: {blocks} blocks, {roots} roots")
        except CheckError as e:
            failed += 1
            print(f"FAIL  {path}: {e}")
    print(f"{len(paths) - failed} of {len(paths)} files passed")
    return 0 if failed == 0 else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv))
