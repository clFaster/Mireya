namespace Mireya.Api.Constants;

public static class NanoIdGen
{
    /// <summary>
    ///     The hexadecimal alphabet used for generating screen identifiers
    /// </summary>
    public const string HexAlphabet = "0123456789ABCDEF";

    /// <summary>
    ///     The length of the generated screen identifier
    /// </summary>
    /// <remarks>If changed, don't forget to update the DB Model</remarks>
    public const int ScreenIdentifierLength = 10;

    /// <summary>
    ///     The numeric alphabet used for generating screen passwords
    /// </summary>
    public const string ScreenPasswordAlphabet = "0123456789";

    /// <summary>
    ///     The length of the generated screen password
    /// </summary>
    public static int ScreenPasswordLength { get; set; } = 12;
}
