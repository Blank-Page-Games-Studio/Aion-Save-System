// com.bpg.aion/Runtime/Core/SaveErrorTranslator.cs
#nullable enable
using System;
using System.IO;
using System.Security.Cryptography;

namespace BPG.Aion
{
    /// <summary>
    /// Maps internal exceptions/statuses to user-friendly messages.
    /// </summary>
    public static class SaveErrorTranslator
    {
        public static string Friendly(Exception ex)
        {
            return ex switch
            {
                UnauthorizedAccessException => "Access denied. The game cannot write to the save folder.",
                DirectoryNotFoundException => "Save folder not found. It will be created automatically.",
                IOException io when io.Message.Contains("sharing", StringComparison.OrdinalIgnoreCase)
                    => "The save file is locked by another process.",
                CryptographicException => "Decryption failed. The save may be corrupted or the key is invalid.",
                NotSupportedException when ex.Message.Contains("compression", StringComparison.OrdinalIgnoreCase)
                    => "Compression is not supported on this platform.",
                PlatformNotSupportedException => "This platform does not support the selected encryption method.",
                _ => "An unexpected error occurred during saving. Try again."
            };
        }

        public static string Friendly(ResultStatus status) => status switch
        {
            ResultStatus.Ok => "Success.",
            ResultStatus.NotFound => "Save not found.",
            ResultStatus.Corrupt => "Save data appears to be corrupted.",
            ResultStatus.Unauthorized => "Permission denied writing/reading save data.",
            ResultStatus.Error => "An error occurred.",
            _ => "Unknown status."
        };
    }
}
