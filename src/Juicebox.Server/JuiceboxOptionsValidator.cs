using System.Net;
using Microsoft.Extensions.Options;

namespace Juicebox.Server;

public sealed class JuiceboxOptionsValidator : IValidateOptions<JuiceboxOptions>
{
	public ValidateOptionsResult Validate( string? name, JuiceboxOptions options )
	{
		var errors = new List<string>();

		if ( options.JoinCodeLength is < Secrets.MinJoinCodeLength or > Secrets.MaxJoinCodeLength )
		{
			errors.Add( $"JoinCodeLength must be between {Secrets.MinJoinCodeLength} and {Secrets.MaxJoinCodeLength}." );
		}

		RequirePositive( options.SessionTimeout, nameof(options.SessionTimeout) );
		RequirePositive( options.MemberTimeout, nameof(options.MemberTimeout) );
		RequirePositive( options.RetainDestroyedFor, nameof(options.RetainDestroyedFor) );
		RequirePositive( options.ExpiryInterval, nameof(options.ExpiryInterval) );
		RequirePositive( options.SocketTicketLifetime, nameof(options.SocketTicketLifetime) );

		if ( options.MaxMessageBytes <= 0 )
		{
			errors.Add( "MaxMessageBytes must be greater than zero." );
		}

		if ( options.JoinRateLimitPerMinute <= 0 || options.CreateRateLimitPerMinute <= 0 )
		{
			errors.Add( "JoinRateLimitPerMinute and CreateRateLimitPerMinute must be greater than zero." );
		}

		if ( !string.IsNullOrWhiteSpace( options.PublicUrl ) &&
		     (!Uri.TryCreate( options.PublicUrl, UriKind.Absolute, out var publicUrl ) ||
		      (publicUrl.Scheme != Uri.UriSchemeHttp && publicUrl.Scheme != Uri.UriSchemeHttps)) )
		{
			errors.Add( $"PublicUrl must be an absolute http:// or https:// URL, got '{options.PublicUrl}'." );
		}

		foreach ( var proxy in options.TrustedProxies )
		{
			if ( !IPAddress.TryParse( proxy, out _ ) && !IPNetwork.TryParse( proxy, out _ ) )
			{
				errors.Add( $"TrustedProxies entry '{proxy}' is not an IP address or CIDR range." );
			}
		}

		return errors.Count == 0
			? ValidateOptionsResult.Success
			: ValidateOptionsResult.Fail( errors.Select( e => $"{JuiceboxOptions.SectionName}:{e}" ) );

		void RequirePositive( TimeSpan value, string setting )
		{
			if ( value <= TimeSpan.Zero )
			{
				errors.Add( $"{setting} must be greater than zero." );
			}
		}
	}
}
