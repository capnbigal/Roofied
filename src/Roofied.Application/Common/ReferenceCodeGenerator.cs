using System.Security.Cryptography;
using Roofied.Application.Abstractions;

namespace Roofied.Application.Common;

/// <summary>
/// Generates short, unambiguous report reference codes like "RPT-7F3K9Q".
/// Uses a crypto RNG and a Crockford-style alphabet (no easily confused characters).
/// </summary>
public sealed class ReferenceCodeGenerator : IReferenceCodeGenerator
{
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ"; // no I, L, O, U
    private const int Length = 6;

    public string Generate()
    {
        Span<byte> bytes = stackalloc byte[Length];
        RandomNumberGenerator.Fill(bytes);
        Span<char> chars = stackalloc char[Length];
        for (var i = 0; i < Length; i++)
            chars[i] = Alphabet[bytes[i] % Alphabet.Length];
        return $"RPT-{new string(chars)}";
    }
}
