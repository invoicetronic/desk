namespace Desk.Models;

/// <summary>
/// How the API returns an invoice payload: plain XML text, or Base64 of the original file bytes
/// (signed .p7m files, and XML files in an encoding other than UTF-8).
/// </summary>
public enum PayloadEncoding
{
    Xml = 1,
    Base64 = 2
}
