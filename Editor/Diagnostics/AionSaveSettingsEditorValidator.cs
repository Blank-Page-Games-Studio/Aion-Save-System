// Assets/SaveSystem/Editor/Diagnostics/AionSaveSettingsEditorValidator.cs
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;

namespace BPG.Aion.Editor.Diagnostics
{
    public enum ValidationSeverity
    {
        Info,
        Warning,
        Error
    }

    public readonly struct ValidationMessage
    {
        public ValidationSeverity Severity { get; }
        public string Message { get; }

        public ValidationMessage(ValidationSeverity severity, string message)
        {
            Severity = severity;
            Message = message;
        }
    }

    public static class AionSaveSettingsEditorValidator
    {
        public const string RelativeFolderInvalidCharsMessage =
            "Relative Save Folder contains invalid path characters.";

        public const string RelativeFolderAbsoluteMessagePrefix =
            "Relative Save Folder is an absolute path while Use Persistent Data Path is enabled. Effective path: ";

        public const string DefaultProfileNameEmptyMessage =
            "Default Profile Name is empty after trimming.";

        public const string DefaultProfileNameSeparatorMessage =
            "Default Profile Name contains path separators ('/' or '\\').";

        public const string StreamingChunkSizeNotMultipleMessage =
            "Streaming Chunk Size is not a multiple of 4096 bytes and will be rounded.";

        public const string EncryptionKeyMissingMessage =
            "Encryption is enabled but Key Provider Id is empty or whitespace.";

        public static List<ValidationMessage> Validate(AionSaveSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            var messages = new List<ValidationMessage>();

            var relativeFolder = settings.RelativeSaveFolder ?? string.Empty;
            if (ContainsInvalidPathChars(relativeFolder))
            {
                messages.Add(new ValidationMessage(ValidationSeverity.Warning, RelativeFolderInvalidCharsMessage));
            }

            if (settings.UsePersistentDataPath && !string.IsNullOrWhiteSpace(relativeFolder) && Path.IsPathRooted(relativeFolder))
            {
                var effective = settings.GetEffective();
                messages.Add(new ValidationMessage(
                    ValidationSeverity.Warning,
                    RelativeFolderAbsoluteMessagePrefix + effective.EffectiveSaveFolderPath));
            }

            var profileName = settings.DefaultProfileName ?? string.Empty;
            var trimmedProfile = profileName.Trim();
            if (string.IsNullOrEmpty(trimmedProfile))
            {
                messages.Add(new ValidationMessage(ValidationSeverity.Warning, DefaultProfileNameEmptyMessage));
            }
            else if (ContainsPathSeparator(trimmedProfile))
            {
                messages.Add(new ValidationMessage(ValidationSeverity.Warning, DefaultProfileNameSeparatorMessage));
            }

            if (settings.StreamingChunkSizeBytes % 4096 != 0)
            {
                messages.Add(new ValidationMessage(ValidationSeverity.Warning, StreamingChunkSizeNotMultipleMessage));
            }

            if (settings.EnableEncryption && string.IsNullOrWhiteSpace(settings.KeyProviderId))
            {
                messages.Add(new ValidationMessage(ValidationSeverity.Error, EncryptionKeyMissingMessage));
            }

            return messages;
        }

        private static bool ContainsPathSeparator(string value)
        {
            return value.IndexOf(Path.DirectorySeparatorChar) >= 0 ||
                   value.IndexOf(Path.AltDirectorySeparatorChar) >= 0;
        }

        private static bool ContainsInvalidPathChars(string value)
        {
            var invalid = Path.GetInvalidPathChars();
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                if (c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar)
                    continue;
                if (Array.IndexOf(invalid, c) >= 0)
                    return true;
            }

            return false;
        }
    }
}
