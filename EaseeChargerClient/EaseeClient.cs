using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EaseeChargerClient;

/// <summary>Provides an access token for authenticated REST and SignalR requests.</summary>
public interface IEaseeTokenProvider
	{
	/// <summary>Obtains an access token for the next authenticated request.</summary>
	/// <returns>A task yielding a bearer access token without the Bearer prefix.</returns>
	/// <param name="cancellationToken">Cancels the asynchronous operation. Cancellation does not undo a request already accepted by the server.</param>
	Task<string> GetAccessTokenAsync (CancellationToken cancellationToken = default);
	}

/// <summary>Configures the REST endpoint, authentication, logging and request timeout.</summary>
public sealed class EaseeClientOptions
	{
	/// <summary>The absolute HTTPS REST base URI. Defaults to https://api.easee.com. HTTP is accepted only for loopback testing.</summary>
	/// <value>The absolute HTTPS REST base URI. Defaults to https://api.easee.com. HTTP is accepted only for loopback testing.</value>
	public Uri BaseUri { get; set; } = new ("https://api.easee.com");
	/// <summary>The caller-owned token provider; null permits only unauthenticated operations.</summary>
	/// <value>The caller-owned token provider; null permits only unauthenticated operations.</value>
	public IEaseeTokenProvider? TokenProvider
		{
		get; set;
		}
	/// <summary>The caller-owned logger. Defaults to a logger that discards messages.</summary>
	/// <value>The caller-owned logger. Defaults to a logger that discards messages.</value>
	public ILogger Logger { get; set; } = NullLogger.Instance;
	/// <summary>The positive timeout for token acquisition and the HTTP request; defaults to 60 seconds. Stream consumption is caller-controlled.</summary>
	/// <value>The positive timeout for token acquisition and the HTTP request; defaults to 60 seconds. Stream consumption is caller-controlled.</value>
	public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds (60);
	}

/// <summary>Typed REST client. Caller-supplied HttpClient and logger remain owned by the caller. Requests are never automatically replayed.</summary>
public sealed partial class EaseeClient : IDisposable
	{
	private readonly HttpClient _http;
	private readonly bool _ownsHttp;
	private readonly Uri _baseUri;
	private readonly IEaseeTokenProvider? _tokens;
	private readonly ILogger _logger;
	private readonly TimeSpan _timeout;
	/// <summary>Creates a typed REST client with explicit ownership of its dependencies.</summary>
	/// <exception cref="ArgumentException">The base URI is not HTTPS or loopback HTTP.</exception>
	/// <exception cref="ArgumentOutOfRangeException">The request timeout is not positive.</exception>
	/// <param name="options">The client configuration, or null for defaults.</param>
	/// <param name="httpClient">A caller-owned HTTP client, or null to create a client owned by this instance. Configure caller-supplied redirect behavior appropriately.</param>
	public EaseeClient (EaseeClientOptions? options = null, HttpClient? httpClient = null)
		{
		options ??= new ();
		if (!options.BaseUri.IsAbsoluteUri || (options.BaseUri.Scheme != "https" && !(options.BaseUri.Scheme == "http" && options.BaseUri.IsLoopback)))
			throw new ArgumentException ("BaseUri must use HTTPS.", nameof (options));
		if (options.RequestTimeout <= TimeSpan.Zero)
			throw new ArgumentOutOfRangeException (nameof (options));
		_baseUri = options.BaseUri;
		_tokens = options.TokenProvider;
		_logger = options.Logger ?? NullLogger.Instance;
		_timeout = options.RequestTimeout;
		_ownsHttp = httpClient is null;
		_http = httpClient ?? new HttpClient (new HttpClientHandler { AllowAutoRedirect = false });
		}
	/// <summary>Disposes an internally created HTTP client. Caller-supplied dependencies remain caller-owned.</summary>
	public void Dispose ()
		{
		if (_ownsHttp)
			_http.Dispose ();
		}
	internal static string Format (object value) => value switch
		{
			DateTimeOffset date => date.ToString ("O", CultureInfo.InvariantCulture),
			DateTime date => date.ToString ("O", CultureInfo.InvariantCulture),
			bool boolean => boolean ? "true" : "false",
			Enum e when e.GetType ().GetCustomAttributes (typeof (System.Text.Json.Serialization.JsonConverterAttribute), false).Length != 0 => JsonSerializer.Serialize (e, e.GetType (), EaseeJson.Options).Trim ('"'),
			Enum e => Convert.ToInt64 (e, CultureInfo.InvariantCulture).ToString (CultureInfo.InvariantCulture),
			IFormattable f => f.ToString (null, CultureInfo.InvariantCulture),
			_ => value.ToString () ?? ""
			};
	internal static string Escape (object value)
		{
		string text = Format (value);
		if (text is "." or "..")
			throw new ArgumentException ("Dot segments cannot identify an Easee resource.");
		return Uri.EscapeDataString (text);
		}
	private async Task<HttpRequestMessage> CreateRequestAsync (string verb, string path, List<KeyValuePair<string, object?>> query, object? body, bool authenticate, EaseeRequestOptions? options, CancellationToken cancellationToken)
		{
		var pairs = new List<string> ();
		foreach (KeyValuePair<string, object?> pair in query)
			{
			if (pair.Value is null)
				continue;
			if (pair.Value is System.Collections.IEnumerable list && pair.Value is not string)
				{
				foreach (object item in list)
					pairs.Add (Uri.EscapeDataString (pair.Key) + "=" + Uri.EscapeDataString (Format (item)));
				}
			else
				pairs.Add (Uri.EscapeDataString (pair.Key) + "=" + Uri.EscapeDataString (Format (pair.Value)));
			}
		string uri = path + (pairs.Count == 0 ? "" : "?" + string.Join ("&", pairs));
		string? token = authenticate ? await (_tokens ?? throw new InvalidOperationException ("An Easee token provider is required.")).GetAccessTokenAsync (cancellationToken).ConfigureAwait (false) : null;
		var request = new HttpRequestMessage (new HttpMethod (verb), new Uri (_baseUri, uri));
		try
			{
			request.Headers.Accept.Add (new MediaTypeWithQualityHeaderValue ("application/json"));
			if (token is not null)
				request.Headers.Authorization = new AuthenticationHeaderValue ("Bearer", token);
			if (options is not null)
				foreach (KeyValuePair<string, string> header in options.Headers)
					{
					if (header.Key.Equals ("Authorization", StringComparison.OrdinalIgnoreCase) || header.Key.Equals ("Host", StringComparison.OrdinalIgnoreCase))
						throw new ArgumentException ("This header is managed by the client.", nameof (options));
					request.Headers.Add (header.Key, header.Value);
					}
			if (body is not null)
				request.Content = new StringContent (JsonSerializer.Serialize (body, body.GetType (), EaseeJson.Options), Encoding.UTF8, "application/json");
			return request;
			}
		catch { request.Dispose (); throw; }
		}
	internal async Task<EaseeResponse<T>> SendAsync<T> (string operation, string verb, string path, List<KeyValuePair<string, object?>> query, object? body, bool authenticate, EaseeRequestOptions? options, CancellationToken cancellationToken)
		{
		using var timeout = CancellationTokenSource.CreateLinkedTokenSource (cancellationToken);
		timeout.CancelAfter (_timeout);
		using HttpRequestMessage request = await CreateRequestAsync (verb, path, query, body, authenticate, options, timeout.Token).ConfigureAwait (false);
		using HttpResponseMessage response = await _http.SendAsync (request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait (false);
		_logger.LogDebug ("Easee {Operation} returned HTTP {Status}", operation, (int)response.StatusCode);
		await EnsureSuccessAsync (operation, response, timeout.Token).ConfigureAwait (false);
		T? value = default;
		if (typeof (T) != typeof (EaseeNoContent) && response.StatusCode != HttpStatusCode.NoContent)
			{
			using Stream stream = await response.Content.ReadAsStreamAsync ().ConfigureAwait (false);
			value = await JsonSerializer.DeserializeAsync<T> (stream, EaseeJson.Options, timeout.Token).ConfigureAwait (false);
			}
		return new (response.StatusCode, value, GetHeaders (response));
		}
	internal async Task<EaseeStreamResponse> SendStreamAsync (string operation, string verb, string path, List<KeyValuePair<string, object?>> query, object? body, bool authenticate, EaseeRequestOptions? options, CancellationToken cancellationToken)
		{
		using var timeout = CancellationTokenSource.CreateLinkedTokenSource (cancellationToken);
		timeout.CancelAfter (_timeout);
		using HttpRequestMessage request = await CreateRequestAsync (verb, path, query, body, authenticate, options, timeout.Token).ConfigureAwait (false);
		request.Headers.Accept.Clear ();
		request.Headers.Accept.Add (new MediaTypeWithQualityHeaderValue ("*/*"));
		HttpResponseMessage response = await _http.SendAsync (request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait (false);
		try
			{
			await EnsureSuccessAsync (operation, response, timeout.Token).ConfigureAwait (false);
			return new (response, response.Content is null ? Stream.Null : await response.Content.ReadAsStreamAsync ().ConfigureAwait (false));
			}
		catch { response.Dispose (); throw; }
		}
	private static async Task EnsureSuccessAsync (string operation, HttpResponseMessage response, CancellationToken cancellationToken)
		{
		if (response.IsSuccessStatusCode)
			return;
		EaseeProblem? problem = null;
		object? errorResponse = null;
		try
			{
			if (response.Content is not null)
				{
				using Stream stream = await response.Content.ReadAsStreamAsync ().ConfigureAwait (false);
				Type bodyType = EaseeErrorContracts.BodyType (operation, (int)response.StatusCode) ?? typeof (EaseeProblem);
				errorResponse = await JsonSerializer.DeserializeAsync (stream, bodyType, EaseeJson.Options, cancellationToken).ConfigureAwait (false);
				problem = errorResponse as EaseeProblem ?? (errorResponse is IEaseeProblemDetails details ? EaseeProblem.From (details) : null);
				}
			}
		catch (JsonException) { }
		throw new EaseeApiException (operation, response.StatusCode, problem, GetHeaders (response), errorResponse);
		}
	internal static IReadOnlyDictionary<string, string[]> GetHeaders (HttpResponseMessage response)
		{
		var headers = response.Headers.ToDictionary (x => x.Key, x => x.Value.ToArray (), StringComparer.OrdinalIgnoreCase);
		if (response.Content is not null)
			foreach (KeyValuePair<string, IEnumerable<string>> header in response.Content.Headers)
				headers[header.Key] = header.Value.ToArray ();
		return headers;
		}
	}