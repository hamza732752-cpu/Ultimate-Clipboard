using UltimateClipboard.Helpers;
using Xunit;

namespace UltimateClipboard.Tests;

public class HasherTests
{
    [Fact]
    public void ComputeHash_String_ProducesDeterministicHash()
    {
        string text = "Ultimate Clipboard Rocks!";
        string hash1 = ContentHasher.ComputeHash(text);
        string hash2 = ContentHasher.ComputeHash(text);

        Assert.NotEmpty(hash1);
        Assert.Equal(hash1, hash2);
        Assert.Equal(64, hash1.Length); // SHA256 hex is 64 characters
    }

    [Fact]
    public void ComputeHash_EmptyOrNull_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, ContentHasher.ComputeHash((string?)null));
        Assert.Equal(string.Empty, ContentHasher.ComputeHash(""));
        Assert.Equal(string.Empty, ContentHasher.ComputeHash((byte[]?)null));
        Assert.Equal(string.Empty, ContentHasher.ComputeHash(new byte[0]));
    }

    [Fact]
    public void ComputeHash_Bytes_ProducesDeterministicHash()
    {
        byte[] data = new byte[] { 0x10, 0x20, 0x30, 0x40, 0x50 };
        string hash1 = ContentHasher.ComputeHash(data);
        string hash2 = ContentHasher.ComputeHash(data);

        Assert.NotEmpty(hash1);
        Assert.Equal(hash1, hash2);
    }
}
