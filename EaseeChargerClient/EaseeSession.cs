using EaseeChargerClient.Models;

namespace EaseeChargerClient;

/// <summary>A single owner of a rotating token chain. PersistAsync must durably save each new token before it may be used.</summary>
public sealed class EaseeSession : IEaseeTokenProvider, IDisposable
	{
	private readonly EaseeClient _authentication;
	private readonly SemaphoreSlim _gate = new (1, 1);
	private readonly Func<EaseeToken, CancellationToken, Task> _persist;
	private EaseeToken? _token;
	private bool _requiresLogin;
	/// <summary>Creates a single owner for a rotating token chain and its durable persistence callback.</summary>
	/// <exception cref="ArgumentNullException">The authentication client or persistence callback is null.</exception>
	/// <param name="authenticationClient">The caller-owned client used for sign-in and refresh; it must allow unauthenticated account operations.</param>
	/// <param name="persistAsync">A callback that must durably save every supplied token state before completing. A save failure blocks further refresh use.</param>
	/// <param name="initialToken">The previously persisted token state, or null before sign-in.</param>
	public EaseeSession (EaseeClient authenticationClient, Func<EaseeToken, CancellationToken, Task> persistAsync, EaseeToken? initialToken = null)
		{
		_authentication = authenticationClient ?? throw new ArgumentNullException (nameof (authenticationClient));
		_persist = persistAsync ?? throw new ArgumentNullException (nameof (persistAsync));
		_token = initialToken;
		_requiresLogin = initialToken?.RequiresReauthentication == true;
		}
	/// <summary>Signs in and durably persists the new token pair before making it available.</summary>
	/// <returns>A task that completes after the token pair has been saved.</returns>
	/// <exception cref="ArgumentException">The account or password is empty.</exception>
	/// <exception cref="EaseeApiException">Easee rejected the sign-in.</exception>
	/// <exception cref="InvalidDataException">Easee returned an invalid token pair.</exception>
	/// <exception cref="OperationCanceledException">The operation was cancelled.</exception>
	/// <param name="username">The Easee account email address or phone number.</param>
	/// <param name="password">The account password. The library does not persist it.</param>
	/// <param name="cancellationToken">Cancels the asynchronous operation. Cancellation does not undo a request already accepted by the server.</param>
	public async Task SignInAsync (string username, string password, CancellationToken cancellationToken = default)
		{
		if (string.IsNullOrWhiteSpace (username))
			throw new ArgumentException ("An Easee account is required.", nameof (username));
		if (string.IsNullOrEmpty (password))
			throw new ArgumentException ("A password is required.", nameof (password));
		await _gate.WaitAsync (cancellationToken).ConfigureAwait (false);
		try
			{
			var response = await _authentication.AccountAuthenticateAsync (new Access_Control_API_Login { UserName = username, Password = password }, cancellationToken: cancellationToken).ConfigureAwait (false);
			await AcceptAsync (response.Value, cancellationToken).ConfigureAwait (false);
			}
		finally { _gate.Release (); }
		}
	/// <summary>Obtains an access token, serializing refresh exchanges and persisting replacements before use.</summary>
	/// <returns>A task yielding the current or newly issued access token. Easee controls its lifetime.</returns>
	/// <exception cref="InvalidOperationException">No usable token chain exists; a fresh sign-in is required.</exception>
	/// <exception cref="EaseeApiException">The refresh request was rejected.</exception>
	/// <exception cref="OperationCanceledException">The operation was cancelled.</exception>
	/// <param name="cancellationToken">Cancels the asynchronous operation. Cancellation does not undo a request already accepted by the server.</param>
	public Task<string> GetAccessTokenAsync (CancellationToken cancellationToken = default) => GetAccessTokenAsync (TimeSpan.FromMinutes (1), cancellationToken);
	/// <summary>Renews when the remaining token lifetime is below the requested preparation window.</summary>
	/// <returns>A task yielding the current or newly issued access token. Easee controls its lifetime.</returns>
	/// <exception cref="InvalidOperationException">No usable token chain exists; a fresh sign-in is required.</exception>
	/// <exception cref="ArgumentOutOfRangeException">The requested validity window is negative.</exception>
	/// <exception cref="EaseeApiException">The refresh request was rejected.</exception>
	/// <exception cref="OperationCanceledException">The operation was cancelled.</exception>
	/// <param name="minimumValidity">The nonnegative remaining lifetime at which a refresh is requested. The issued token lifetime is controlled by Easee.</param>
	/// <param name="cancellationToken">Cancels the asynchronous operation. Cancellation does not undo a request already accepted by the server.</param>
	public async Task<string> GetAccessTokenAsync (TimeSpan minimumValidity, CancellationToken cancellationToken = default)
		{
		if (minimumValidity < TimeSpan.Zero)
			throw new ArgumentOutOfRangeException (nameof (minimumValidity));
		await _gate.WaitAsync (cancellationToken).ConfigureAwait (false);
		try
			{
			if (_requiresLogin || _token is null)
				throw new InvalidOperationException ("Sign in to Easee before using the client.");
			if (_token.ExpiresAt > DateTimeOffset.UtcNow.Add (minimumValidity))
				return _token.AccessToken;
			// Refresh tokens are single use. An ambiguous exchange or failed persistence requires reauthentication.
			_requiresLogin = true;
			_token.RequiresReauthentication = true;
			await _persist (_token, cancellationToken).ConfigureAwait (false);
			var response = await _authentication.AccountRefreshTokenAsync (new Access_Control_API_RefreshTokenDTO { RefreshToken = _token.RefreshToken }, cancellationToken: cancellationToken).ConfigureAwait (false);
			await AcceptAsync (response.Value, cancellationToken).ConfigureAwait (false);
			return _token!.AccessToken;
			}
		finally { _gate.Release (); }
		}
	private async Task AcceptAsync (Access_Control_API_UserToken? response, CancellationToken cancellationToken)
		{
		_requiresLogin = true;
		if (response is null || string.IsNullOrWhiteSpace (response.AccessToken) || string.IsNullOrWhiteSpace (response.RefreshToken) || response.ExpiresIn is null or <= 0)
			throw new InvalidDataException ("Easee did not return a valid token pair.");
		var next = new EaseeToken { AccessToken = response.AccessToken!, RefreshToken = response.RefreshToken!, ExpiresAt = DateTimeOffset.UtcNow.AddSeconds (response.ExpiresIn.Value) };
		// Once issued, save even if the caller cancels to avoid losing the replacement refresh token.
		await _persist (next, CancellationToken.None).ConfigureAwait (false);
		_token = next;
		_requiresLogin = false;
		}
	/// <summary>Releases the session synchronization resources. Dispose only after its pending operations complete.</summary>
	public void Dispose () => _gate.Dispose ();
	}

/// <summary>Holds a rotating token pair and its durable refresh-exchange state. Treat all token fields as secrets.</summary>
public sealed class EaseeToken
	{
	/// <summary>The bearer access token. Never log or display this secret.</summary>
	/// <value>The bearer access token. Never log or display this secret.</value>
	public string AccessToken { get; set; } = "";
	/// <summary>The single-use refresh token. Persist replacements atomically and never share a token chain between owners.</summary>
	/// <value>The single-use refresh token. Persist replacements atomically and never share a token chain between owners.</value>
	public string RefreshToken { get; set; } = "";
	/// <summary>The access-token expiry as a UTC-capable timestamp.</summary>
	/// <value>The access-token expiry as a UTC-capable timestamp.</value>
	public DateTimeOffset ExpiresAt
		{
		get; set;
		}
	/// <summary>Persisted before a refresh exchange. An interrupted or ambiguous exchange requires a fresh sign-in.</summary>
	/// <value>True when an interrupted, ambiguous or rejected refresh exchange requires a fresh sign-in.</value>
	public bool RequiresReauthentication
		{
		get; set;
		}
	/// <summary>Returns a description that does not expose credentials.</summary>
	/// <returns>A fixed redacted description.</returns>
	public override string ToString () => "Easee token (redacted)";
	}