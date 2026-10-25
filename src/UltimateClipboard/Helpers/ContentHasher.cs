using System;
using System.Security.Cryptography;
using System.Text;

namespace UltimateClipboard.Helpers;

/// <summary>
/// Generates SHA256 hashes for clipboard content to detect duplicates.
/// </summary>
public static class ContentHasher
{
    public static string ComputeHash(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexStringLower(bytes);
    }

    public static string ComputeHash(byte[]? data)
    {
        if (data is null || data.Length == 0)
            return string.Empty;

        byte[] bytes = SHA256.HashData(data);
        return Convert.ToHexStringLower(bytes);
    }

    public static string ComputeHash(string? text, byte[]? imageData)
    {
        if (!string.IsNullOrEmpty(text))
            return ComputeHash(text);
        if (imageData is not null && imageData.Length > 0)
            return ComputeHash(imageData);
        return string.Empty;
    }
}
