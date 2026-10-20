using System.Text.Json.Serialization;

using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace EaseeChargerClient;

/// <summary>Represents one real-time ProductUpdate message from the Easee SignalR hub.</summary>
public sealed class EaseeProductUpdate
	{
	/// <summary>The charger identifier (serial number), or null when omitted by the hub.</summary>
	/// <value>The charger identifier (serial number), or null when omitted by the hub.</value>
	[JsonPropertyName ("mid")]
	public string? DeviceId
		{
		get; set;
		}
	/// <summary>The underlying observation data type code supplied by Easee.</summary>
	/// <value>The underlying observation data type code supplied by Easee.</value>
	[JsonPropertyName ("dataType")]
	public int DataType
		{
		get; set;
		}
	/// <summary>The Easee observation identifier carried by this update.</summary>
	/// <value>The Easee observation identifier carried by this update.</value>
	[JsonPropertyName ("id")]
	public int ObservationId
		{
		get; set;
		}
	/// <summary>The timestamp supplied by the hub for this observation.</summary>
	/// <value>The timestamp supplied by the hub for this observation.</value>
	[JsonPropertyName ("timestamp")]
	public DateTimeOffset Timestamp
		{
		get; set;
		}
	/// <summary>Easee transmits observation values as strings, regardless of the underlying data type.</summary>
	/// <value>The string representation transmitted by Easee, or null when absent; interpret it using DataType.</value>
	[JsonPropertyName ("value")]
	public string? Value
		{
		get; set;
		}
	}

internal interface IEaseeProductConnection : IAsyncDisposable
	{
	event Func<Task>? Reconnected;
	event Action? Closed;
	Task StartAsync (CancellationToken cancellationToken);
	Task SubscribeAsync (string serial, CancellationToken cancellationToken);
	Task StopAsync (CancellationToken cancellationToken);
	}

internal sealed class EaseeProductConnection : IEaseeProductConnection
	{
	private readonly HubConnection _hub;
	public EaseeProductConnection (IEaseeTokenProvider tokens, Func<EaseeProductUpdate, Task> receive, Uri? hubUri)
		{
		Uri uri = hubUri ?? new ("https://streams.easee.com/hubs/products");
		if (uri.Scheme != "https")
			throw new ArgumentException ("The streaming hub must use HTTPS.", nameof (hubUri));
		_hub = new HubConnectionBuilder ().WithUrl (uri, options => options.AccessTokenProvider = async () => await tokens.GetAccessTokenAsync ().ConfigureAwait (false)).WithAutomaticReconnect ().AddJsonProtocol (options => options.PayloadSerializerOptions.PropertyNameCaseInsensitive = false).Build ();
		_hub.On ("ProductUpdate", receive);
		_hub.Reconnected += async _ =>
		{
			if (Reconnected is not null)
				foreach (Func<Task> handler in Reconnected.GetInvocationList ())
					await handler ().ConfigureAwait (false);
		};
		_hub.Closed += _ => { Closed?.Invoke (); return Task.CompletedTask; };
		}
	public event Func<Task>? Reconnected;
	public event Action? Closed;
	public Task StartAsync (CancellationToken cancellationToken) => _hub.StartAsync (cancellationToken);
	public Task SubscribeAsync (string serial, CancellationToken cancellationToken) => _hub.InvokeAsync ("SubscribeWithCurrentState", serial, true, cancellationToken);
	public Task StopAsync (CancellationToken cancellationToken) => _hub.StopAsync (cancellationToken);
	public ValueTask DisposeAsync () => _hub.DisposeAsync ();
	}

/// <summary>Receives Easee ProductUpdate messages and restores subscriptions after reconnection.</summary>
public sealed class EaseeRealtimeClient : IAsyncDisposable
	{
	private readonly IEaseeProductConnection _connection;
	private readonly HashSet<string> _products;
	private readonly SemaphoreSlim _gate = new (1, 1);
	private readonly ILogger _logger;
	private bool _started;
	/// <summary>Creates a SignalR receiver for the supplied charger subscriptions.</summary>
	/// <exception cref="ArgumentNullException">A required dependency is null.</exception>
	/// <exception cref="ArgumentException">A serial number is empty or the hub URI does not use HTTPS.</exception>
	/// <param name="tokens">The caller-owned token provider used for hub authentication.</param>
	/// <param name="serialNumbers">The initial charger serial numbers to subscribe to.</param>
	/// <param name="onUpdate">The asynchronous handler called for each received ProductUpdate.</param>
	/// <param name="logger">The caller-owned logger, or null to discard messages.</param>
	/// <param name="hubUri">The HTTPS SignalR hub URI, or null for the public Easee products hub.</param>
	public EaseeRealtimeClient (IEaseeTokenProvider tokens, IEnumerable<string> serialNumbers, Func<EaseeProductUpdate, Task> onUpdate, ILogger? logger = null, Uri? hubUri = null)
		: this (new EaseeProductConnection (tokens ?? throw new ArgumentNullException (nameof (tokens)), onUpdate ?? throw new ArgumentNullException (nameof (onUpdate)), hubUri), serialNumbers, logger) { }
	internal EaseeRealtimeClient (IEaseeProductConnection connection, IEnumerable<string> serialNumbers, ILogger? logger = null)
		{
		_connection = connection;
		_products = new (serialNumbers ?? throw new ArgumentNullException (nameof (serialNumbers)), StringComparer.OrdinalIgnoreCase);
		if (_products.Any (string.IsNullOrWhiteSpace))
			throw new ArgumentException ("Product serial numbers must be non-empty.", nameof (serialNumbers));
		_logger = logger ?? NullLogger.Instance;
		_connection.Reconnected += RestoreSubscriptionsAsync;
		_connection.Closed += OnClosed;
		}
	private void OnClosed () => _started = false;
	/// <summary>Connects to the hub and subscribes to the initial state and future updates.</summary>
	/// <returns>A task that completes after connection and subscription.</returns>
	/// <param name="cancellationToken">Cancels the asynchronous operation. Cancellation does not undo a request already accepted by the server.</param>
	public async Task StartAsync (CancellationToken cancellationToken = default)
		{
		await _gate.WaitAsync (cancellationToken).ConfigureAwait (false);
		try
			{
			if (_started)
				return;
			await _connection.StartAsync (cancellationToken).ConfigureAwait (false);
			try
				{
				foreach (string product in _products)
					await _connection.SubscribeAsync (product, cancellationToken).ConfigureAwait (false);
				_started = true;
				}
			catch { await _connection.StopAsync (CancellationToken.None).ConfigureAwait (false); throw; }
			}
		finally { _gate.Release (); }
		}
	/// <summary>Adds a charger subscription and subscribes immediately when already connected.</summary>
	/// <returns>A task that completes after the charger is registered or subscribed.</returns>
	/// <exception cref="ArgumentException">The charger serial number is empty.</exception>
	/// <exception cref="OperationCanceledException">The operation was cancelled.</exception>
	/// <param name="serialNumber">The non-empty charger serial number to add to the subscriptions.</param>
	/// <param name="cancellationToken">Cancels the asynchronous operation. Cancellation does not undo a request already accepted by the server.</param>
	public async Task AddProductAsync (string serialNumber, CancellationToken cancellationToken = default)
		{
		if (string.IsNullOrWhiteSpace (serialNumber))
			throw new ArgumentException ("A serial number is required.", nameof (serialNumber));
		await _gate.WaitAsync (cancellationToken).ConfigureAwait (false);
		try
			{
			if (_products.Contains (serialNumber))
				return;
			if (_started)
				await _connection.SubscribeAsync (serialNumber, cancellationToken).ConfigureAwait (false);
			_products.Add (serialNumber);
			}
		finally { _gate.Release (); }
		}
	private async Task RestoreSubscriptionsAsync ()
		{
		await _gate.WaitAsync ().ConfigureAwait (false);
		try
			{
			if (_started)
				foreach (string product in _products)
					await _connection.SubscribeAsync (product, CancellationToken.None).ConfigureAwait (false);
			}
		catch (Exception error) { _logger.LogError ("Easee streaming subscription recovery failed: {ErrorType}", error.GetType ().Name); throw; }
		finally { _gate.Release (); }
		}
	/// <summary>Stops the SignalR connection while retaining the configured subscription list.</summary>
	/// <returns>A task that completes after the connection stops.</returns>
	/// <param name="cancellationToken">Cancels the asynchronous operation. Cancellation does not undo a request already accepted by the server.</param>
	public async Task StopAsync (CancellationToken cancellationToken = default)
		{
		await _gate.WaitAsync (cancellationToken).ConfigureAwait (false);
		try
			{
			await _connection.StopAsync (cancellationToken).ConfigureAwait (false);
			_started = false;
			}
		finally { _gate.Release (); }
		}
	/// <summary>Stops and disposes the connection and subscription resources.</summary>
	/// <returns>An asynchronous disposal operation.</returns>
	public async ValueTask DisposeAsync ()
		{
		await StopAsync ().ConfigureAwait (false);
		_connection.Reconnected -= RestoreSubscriptionsAsync;
		_connection.Closed -= OnClosed;
		await _connection.DisposeAsync ().ConfigureAwait (false);
		_gate.Dispose ();
		}
	}