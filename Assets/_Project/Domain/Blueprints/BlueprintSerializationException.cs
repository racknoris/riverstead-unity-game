using System;

namespace Contraption.Domain.Blueprints
{
    /// <summary>Why a blueprint payload could not be read.</summary>
    public enum BlueprintSerializationError
    {
        /// <summary>The payload was empty, or not valid JSON at all.</summary>
        MalformedJson,

        /// <summary>Valid JSON, but with no schema version — so its shape cannot be trusted.</summary>
        MissingSchemaVersion,

        /// <summary>A schema version this build does not know how to read.</summary>
        UnsupportedSchemaVersion,

        /// <summary>Well-formed and correctly versioned, but the contents are not a valid blueprint.</summary>
        InvalidContent
    }

    /// <summary>
    /// Thrown when a stored blueprint cannot be read.
    ///
    /// Typed and loud by design (`docs/TASKS.md` Milestone 2). The failure mode being guarded
    /// against is a save file that silently loads as an empty or half-populated machine, which
    /// looks to the player like their work vanished rather than like an error.
    /// </summary>
    public sealed class BlueprintSerializationException : Exception
    {
        public BlueprintSerializationException(
            BlueprintSerializationError error,
            string message,
            Exception? innerException = null)
            : base(message, innerException)
        {
            Error = error;
        }

        public BlueprintSerializationError Error { get; }
    }
}
