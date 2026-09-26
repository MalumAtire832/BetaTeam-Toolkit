using Malumware.BetaTeam.Lib.IO.Fin.Blocks;
using Malumware.BetaTeam.Lib.IO.Parsers;

namespace Malumware.BetaTeam.Lib.IO.Fin
{
    public class FinStreamParser : AbstractParser<FinFile>
    {
        public const string TOP_LEVEL_OBJECT = "Top Level Object";
        public const string END_OF_FILE = "End Of File";

        // The longest class name in the shipped files is about 25 characters
        public const int MAX_CLASS_NAME_LENGTH = 64;

        private readonly string _name;
        private readonly FinHeader _header;
        private readonly FinBlockRegistry _registry;

        public FinStreamParser(Stream stream, string name, FinHeader header, FinBlockRegistry registry)
            : base(stream)
        {
            _name = name;
            _header = header;
            _registry = registry;
        }

        public override FinFile Parse()
        {
            var reader = new FinBlockReader(Reader, _registry);
            var objects = new List<NiObject>();
            var topLevelObjects = new List<NiObject>();
            var current = "class name";
            var currentOffset = reader.Position;

            try
            {
                while (true)
                {
                    current = "class name";
                    currentOffset = reader.Position;
                    var className = reader.ReadSizedString(MAX_CLASS_NAME_LENGTH);
                    if (className == END_OF_FILE)
                    {
                        break;
                    }

                    // The marker is followed by the class name of the object it marks
                    var isTopLevel = className == TOP_LEVEL_OBJECT;
                    if (isTopLevel)
                    {
                        currentOffset = reader.Position;
                        className = reader.ReadSizedString(MAX_CLASS_NAME_LENGTH);
                    }

                    var block = _registry.CreateBlock(className)
                        ?? throw FinUnknownClass.Exception(className, currentOffset);
                    current = className;
                    block.ClassName = className;
                    block.Offset = currentOffset;
                    block.Load(reader);

                    objects.Add(block);
                    if (isTopLevel)
                    {
                        topLevelObjects.Add(block);
                    }
                }
            }
            catch (EndOfStreamException e)
            {
                throw new InvalidDataException(
                    $"Unexpected end of file while reading {current} at offset 0x{currentOffset:X}", e
                );
            }

            // The engine stops at the marker and ignores anything after it
            var leftover = reader.Length - reader.Position;
            if (leftover != 0)
            {
                throw new InvalidDataException(
                    $"{leftover} bytes left after End Of File at offset 0x{reader.Position:X}"
                );
            }

            FinLinker.Link(objects, reader.Refs);

            return new FinFile(_name, _header, objects, topLevelObjects);
        }
    }
}
