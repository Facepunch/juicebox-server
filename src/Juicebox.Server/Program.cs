using System.Net;
using System.Threading.RateLimiting;
using Juicebox.Server;
using Juicebox.Server.Api;
using Juicebox.Server.Data;
using Juicebox.Server.Realtime;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder( args );

builder.Services.AddOptions<JuiceboxOptions>()
	.Bind( builder.Configuration.GetSection( JuiceboxOptions.SectionName ) )
	.ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<JuiceboxOptions>, JuiceboxOptionsValidator>();

// SQLite database. Relative paths are resolved against the content root so `dotnet run` and published apps agree.
var connectionString = new SqliteConnectionStringBuilder(
	builder.Configuration.GetConnectionString( "Juicebox" ) ?? "Data Source=App_Data/juicebox.db" );
if ( connectionString.DataSource != ":memory:" && !Path.IsPathRooted( connectionString.DataSource ) )
{
	connectionString.DataSource = Path.Combine( builder.Environment.ContentRootPath, connectionString.DataSource );
}

var dataDirectory = Path.GetDirectoryName( connectionString.DataSource );
if ( !string.IsNullOrEmpty( dataDirectory ) )
{
	Directory.CreateDirectory( dataDirectory );
}

builder.Services.AddDbContext<JuiceboxDbContext>( o => o.UseSqlite( connectionString.ToString() ) );

builder.Services.ConfigureHttpJsonOptions( o =>
{
	// The game and website use PascalCase JSON.
	o.SerializerOptions.PropertyNamingPolicy = null;
} );

// Only trust X-Forwarded-* from loopback (the default) and the configured proxies, so clients can't spoof their IP.
builder.Services.AddOptions<ForwardedHeadersOptions>()
	.Configure<IOptions<JuiceboxOptions>>( ( o, juicebox ) =>
	{
		o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
		foreach ( var proxy in juicebox.Value.TrustedProxies )
		{
			if ( IPAddress.TryParse( proxy, out var address ) )
			{
				o.KnownProxies.Add( address );
			}
			else
			{
				o.KnownIPNetworks.Add( System.Net.IPNetwork.Parse( proxy ) );
			}
		}
	} );

builder.Services.AddCors();
builder.Services.AddOptions<CorsOptions>()
	.Configure<IOptions<JuiceboxOptions>>( ( o, juicebox ) => o.AddDefaultPolicy( policy => policy
		.WithOrigins( juicebox.Value.CorsOrigins )
		.AllowAnyHeader()
		.AllowAnyMethod() ) );

builder.Services.AddRateLimiter( o =>
{
	o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
	o.AddPolicy( RateLimits.Join, context => PerIpPerMinute( context, j => j.JoinRateLimitPerMinute ) );
	o.AddPolicy( RateLimits.Create, context => PerIpPerMinute( context, j => j.CreateRateLimitPerMinute ) );
} );

builder.Services.AddHealthChecks();
builder.Services.AddSingleton( TimeProvider.System );
builder.Services.AddSingleton<SessionRelay>();
builder.Services.AddSingleton<SocketTickets>();
builder.Services.AddSingleton<ExpiryService>();
builder.Services.AddHostedService( sp => sp.GetRequiredService<ExpiryService>() );

var app = builder.Build();

await using ( var scope = app.Services.CreateAsyncScope() )
{
	var db = scope.ServiceProvider.GetRequiredService<JuiceboxDbContext>();
	await db.Database.MigrateAsync();
	await db.Database.ExecuteSqlRawAsync( "PRAGMA journal_mode=WAL;" );
}

app.UseForwardedHeaders();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors();
app.UseRateLimiter();
app.UseWebSockets( new WebSocketOptions
{
	KeepAliveInterval = TimeSpan.FromSeconds( 30 ),
	KeepAliveTimeout = TimeSpan.FromSeconds( 60 )
} );

app.MapSessionEndpoints();
app.MapMemberEndpoints();
app.MapSocketEndpoint();
app.MapHealthChecks( "/healthz" );

// Unknown API routes are 404s; everything else is a client-side route in the website.
app.Map( "/api/{**path}", () => Results.NotFound() );
app.MapFallbackToFile( "index.html" );

app.Run();

static RateLimitPartition<string> PerIpPerMinute( HttpContext context, Func<JuiceboxOptions, int> permits ) =>
	RateLimitPartition.GetFixedWindowLimiter(
		context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
		_ => new FixedWindowRateLimiterOptions
		{
			PermitLimit = permits( context.RequestServices.GetRequiredService<IOptions<JuiceboxOptions>>().Value ),
			Window = TimeSpan.FromMinutes( 1 )
		} );

public partial class Program;
