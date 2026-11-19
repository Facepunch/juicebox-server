# juicebox-server

Juicebox let players join an [s&box](https://sbox.game) game from their phone's browser and play along: answering prompts, voting, drawing, and pressing buttons that the game puts on their screen.

Facepunch's hosted Juicebox service (`juicebox.facepunch.com`) has been **decommissioned**. This repository is a compatible replacement you can self-host, fork, and extend. It includes:

- an ASP.NET Core server with the same HTTP API and WebSocket protocol as the original service
- the original Juicebox website (in [`client/`](client)), which players use to join games

It works with the [s&box Juicebox API library](https://github.com/Facepunch/sbox-juicebox-api) once that library is pointed at your server (see [Connecting a game](#connecting-a-game)).

> [!NOTE]
> This self-hostable version was made with AI. The original service ran on Azure (Web PubSub, Azure SQL, and WebJobs); an AI coding assistant rebuilt the server on plain ASP.NET Core, WebSockets, and SQLite, and adapted the original website to work with it.

> [!IMPORTANT]
> **Facepunch does not provide support for this project, and the repository is archived.** Issues and pull requests won't be answered. Fork it if you want to make changes.

> [!WARNING]
> Juicebox predates [s&box auth tokens](https://sbox.game/dev/doc/services/auth-tokens). Anyone who can reach your server can create sessions, and nothing checks that a request comes from a real s&box game. If you plan on running this publicly, you should add auth token validation. See [Adding s&box auth tokens](#adding-sbox-auth-tokens).

## How it works

1. The **game** creates a session (`POST /api/sessions/create`) and shows its join code on screen.
2. **Players** open the website, enter the join code and a name (`POST /api/members/join`).
3. The game and every player connect to the server over WebSockets (`/api/*/negotiate` returns the URL).
4. The game sends a *display* (a title plus a form with inputs, radio buttons, drawings, or buttons) to everyone or to one player. Players' answers and button presses are relayed back to the game.
5. The game and players ping every 10 seconds. Sessions and players that stop pinging for 5 minutes are removed.

The server only relays messages; all game logic lives in the game.

## Running locally

Requirements:

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js](https://nodejs.org) 18 or newer, with [Corepack](https://nodejs.org/api/corepack.html) enabled (`corepack enable`) so the pinned Yarn 1 is used

Start the server (http://localhost:5210):

```bash
dotnet run --project src/Juicebox.Server
```

In another terminal, start the website dev server (http://localhost:3000). `client/.env.development` points it at the server above:

```bash
cd client
yarn install
yarn start
```

Run the tests:

```bash
dotnet test
```

The SQLite database is created on first run at `src/Juicebox.Server/App_Data/juicebox.db` and migrated automatically.

## Building for production

```bash
dotnet publish src/Juicebox.Server -c Release -o out
```

`dotnet publish` also builds the website and copies it into `out/wwwroot`, so the one app serves both the website and the API. Pass `-p:SkipClientBuild=true` to skip the website build.

Run it from the output directory, which is used as the content root (where `wwwroot/` and `App_Data/` are found):

```bash
cd out
dotnet Juicebox.Server.dll --urls http://0.0.0.0:8080
```

Hosting is up to you. Things to know:

- **Only run one instance.** WebSocket connections are tracked in memory and the database is a local SQLite file, so you can't load-balance across several instances.
- **Use TLS.** Put the app behind a reverse proxy (Caddy, nginx, etc.) that terminates HTTPS, forwards WebSocket upgrades, and passes the original host plus `X-Forwarded-For` and `X-Forwarded-Proto`. Those headers are trusted from loopback; if the proxy is on another machine or container, add its address to `Juicebox:TrustedProxies`. Without them the app hands out `ws://` URLs instead of `wss://` (browsers on an HTTPS page refuse those), and every player shares the proxy's IP address for rate limiting. You can also set `Juicebox:PublicUrl` to fix the WebSocket URL.
- **Keep the data directory.** `App_Data/` (in the content root by default) holds the database.

## Configuration

Settings come from `appsettings.json` or environment variables (use `__` for nesting, e.g. `Juicebox__JoinCodeLength=8`).

| Setting | Default | Description |
|---|---|---|
| `ConnectionStrings:Juicebox` | `Data Source=App_Data/juicebox.db` | SQLite connection string. Relative paths are relative to the app's content root. |
| `Juicebox:JoinCodeLength` | `6` | Length of join codes, 6–8 characters (A–Z and 1–9). |
| `Juicebox:SessionTimeout` | `00:05:00` | Sessions are destroyed when the game stops pinging for this long. |
| `Juicebox:MemberTimeout` | `00:05:00` | Players are removed when they stop pinging for this long. A removed player can rejoin with the same name. |
| `Juicebox:RetainDestroyedFor` | `7.00:00:00` | How long destroyed sessions and players are kept in the database. |
| `Juicebox:SocketTicketLifetime` | `00:01:00` | How long a WebSocket URL from `negotiate` stays valid. Each URL works once. |
| `Juicebox:MaxMessageBytes` | `1048576` | Largest WebSocket message accepted. Drawings are sent as PNG data URLs. |
| `Juicebox:PublicUrl` | *(empty)* | Base URL used to build WebSocket URLs, e.g. `https://juicebox.example.com`. When empty it comes from the request. |
| `Juicebox:CorsOrigins` | `[]` | Origins allowed to call the API from a browser. Only needed if the website is hosted on a different origin. |
| `Juicebox:TrustedProxies` | `[]` | IP addresses or CIDR ranges of reverse proxies whose `X-Forwarded-For`/`X-Forwarded-Proto` headers are trusted. Loopback is always trusted. |
| `Juicebox:JoinRateLimitPerMinute` | `30` | Join attempts per minute per IP address. Players on the same network share one IP. |
| `Juicebox:CreateRateLimitPerMinute` | `10` | Session creations per minute per IP address. |

The website takes one build-time setting: `REACT_APP_API_URL` is the server's base URL. Leave it empty when the server hosts the website.

## Connecting a game

The [s&box Juicebox API library](https://github.com/Facepunch/sbox-juicebox-api) has the old server address hardcoded in `Code/JuiceboxWebApi.cs`:

```cs
private const string apiEndpoint = "https://juicebox.facepunch.com/api/sessions";
```

Fork the library and change it to your server, e.g. `https://juicebox.example.com/api/sessions`. The `PublicKey` the library sends when creating a session is ignored by this server and can be removed. Players then join at your server's address instead of `juicebox.facepunch.com`.

## Adding s&box auth tokens

[Auth tokens](https://sbox.game/dev/doc/services/auth-tokens) let a backend check that a request came from a real Steam user in an s&box game. Juicebox was built before they existed, so session creation is unauthenticated. One way to add them:

1. In the game library, get a token with `await Sandbox.Services.Auth.GetToken( "YourServiceName" )` and send it to `sessions/create` along with the player's Steam ID.
2. In `SessionEndpoints.Create` (`src/Juicebox.Server/Api/SessionEndpoints.cs`), validate the token by posting `{ steamid, token }` to `https://public.facepunch.com/sbox/auth/token`. Reject the request unless the response has `Status == "ok"` and the same `SteamId`.
3. Optionally store the host's Steam ID on the session and rate-limit or ban by Steam ID instead of IP address.

Players on the website aren't in s&box, so auth tokens don't apply to them; they're identified by the per-player secret key returned from `members/join`.

## Protocol

All JSON uses PascalCase property names.

### HTTP API

| Endpoint | Request | Response |
|---|---|---|
| `POST /api/sessions/create` | *(body ignored)* | `{ SessionId, HostSecretKey, JoinPassword }` |
| `POST /api/sessions/negotiate` | `{ SessionId, HostSecretKey }` | `{ Endpoint }`: a WebSocket URL |
| `POST /api/sessions/ping` | `{ SessionId, HostSecretKey }` | `{ MemberNames: [...] }` |
| `POST /api/sessions/destroy` | `{ SessionId, HostSecretKey }` | `204`, and every connection in the session is closed with reason `Session Closed` |
| `POST /api/members/join` | `{ JoinPassword, MemberName }` | `{ SessionId, MemberName, MemberSecretKey }`. `404` if no session has that code, `409` if the name is taken (names are case-insensitive), `423` if the session is locked |
| `POST /api/members/negotiate` | `{ SessionId, MemberName, MemberSecretKey }` | `{ Endpoint }` |
| `POST /api/members/ping` | `{ SessionId, MemberName, MemberSecretKey }` | `204` |

`404` from `negotiate` or `ping` means the session or player no longer exists. `GET /healthz` returns `200` when the server is up.

### WebSocket messages

Connect to the `Endpoint` URL from `negotiate`. Each URL can be used once and expires after a minute, so negotiate again for every connection attempt. All messages are JSON text. Every message the server receives is answered with `ack`, or with `fail:N` if it was rejected (`fail:0` binary message, `fail:2` invalid JSON, `fail:4` missing `Message`).

**Game → server**: send to one player, or to everyone when `To` is null or empty:

```json
{ "To": "Alice", "Message": { "Type": "Display", "Display": { "Stage": { "Title": "Hello" } } } }
```

**Server → player**: the `Message` object, as-is.

**Player → server**: any JSON object. The website sends `{ "Type": "Response", "Fields": { ... } }` and `{ "Type": "Action", "Key": "..." }`.

**Server → game**:

```json
{ "Type": "Connected", "MemberName": "Alice" }
{ "Type": "Disconnected", "MemberName": "Alice" }
{ "Type": "Message", "MemberName": "Alice", "Message": { "Type": "Response", "Fields": { "answer": "potato" } } }
```

`Connected` and `Disconnected` are sent for each WebSocket, so a player who reconnects sends both. When the game itself connects or reconnects, it receives a `Connected` event for every player who is already connected.

Message bodies are forwarded byte-for-byte as the sender wrote them.

The display format the website understands is defined in [`client/src/models/JuiceboxInterfaces.ts`](client/src/models/JuiceboxInterfaces.ts).

## Differences from the original service

- Runs anywhere .NET runs. Azure Web PubSub, Azure SQL, and Azure WebJobs were replaced with plain WebSockets, SQLite, and a background service.
- No project IDs or public keys. Requests that still send `PublicKey` or `ProjectId` keep working; the values are ignored.
- Join codes are 6 characters instead of 4.
- A player whose session timed out can rejoin with the same name.
- Join and create are rate-limited per IP address.
- WebSocket URLs are single-use and short-lived, instead of reusable for a day.
- A reconnecting game is told which players are already connected.
- The website calls the API on its own origin instead of a hardcoded Facepunch URL.

## Repository layout

```
src/Juicebox.Server/      ASP.NET Core app
  Api/                    HTTP endpoints
  Realtime/               WebSocket endpoint, one-time tickets, and message relay
  Data/                   EF Core model and migrations (SQLite)
  ExpiryService.cs        removes sessions and players that stopped pinging
tests/Juicebox.Server.Tests/
client/                   Juicebox website (Create React App)
```

To change the database schema, edit the model in `Data/` and add a migration:

```bash
dotnet tool restore
dotnet ef migrations add <Name> --project src/Juicebox.Server -o Data/Migrations
```

## License

[MIT](LICENSE)
