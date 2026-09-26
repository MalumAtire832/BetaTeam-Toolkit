using System.Diagnostics.CodeAnalysis;

namespace Malumware.BetaTeam.Lib.IO.Fin
{
    // Blocks carry no size, so an unknown class can't be skipped. InvalidDataException is sealed, so the class name
    // and offset travel in the exception's Data instead of in a subclass.
    public static class FinUnknownClass
    {
        private const string CLASS_NAME_KEY = "FinClassName";
        private const string OFFSET_KEY = "FinOffset";

        public static InvalidDataException Exception(string className, long offset)
        {
            var exception = new InvalidDataException($"Unknown class \"{className}\" at offset 0x{offset:X}");
            exception.Data[CLASS_NAME_KEY] = className;
            exception.Data[OFFSET_KEY] = offset;

            return exception;
        }

        public static bool TryGet(Exception exception, [NotNullWhen(true)] out string? className, out long offset)
        {
            className = exception.Data[CLASS_NAME_KEY] as string;
            offset = exception.Data[OFFSET_KEY] as long? ?? 0;

            return className is not null;
        }
    }
}
