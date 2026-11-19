namespace Juicebox.Server.Api;

internal static class ApiResults
{
	public static IResult MissingFields() => Results.BadRequest( "Missing required fields" );
}

public static class RateLimits
{
	public const string Join = "join";
	public const string Create = "create";
}
