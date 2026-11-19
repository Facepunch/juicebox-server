using System.Security.Cryptography;

namespace Juicebox.Server;

public static class Secrets
{
	public const int SecretKeyLength = 32;
	public const int MinJoinCodeLength = 6;
	public const int MaxJoinCodeLength = 8;

	private const string SecretKeyChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

	// No zero, to avoid confusion with the letter O.
	private const string JoinCodeChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ123456789";

	public static string GenerateSecretKey() => RandomNumberGenerator.GetString( SecretKeyChars, SecretKeyLength );

	public static string GenerateJoinCode( int length )
	{
		ArgumentOutOfRangeException.ThrowIfLessThan( length, MinJoinCodeLength );
		ArgumentOutOfRangeException.ThrowIfGreaterThan( length, MaxJoinCodeLength );
		return RandomNumberGenerator.GetString( JoinCodeChars, length );
	}
}
