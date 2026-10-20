using System.Text.Json.Serialization;

namespace EaseeChargerClient.Testing;

/// <summary>Short-lived, read-only test inputs. Never contains a password or refresh token.</summary>
public sealed class LiveTestSettings
	{
	/// <summary>The explicit live-test permission flag. Prepared inputs default to false; the test runner supplies its separate opt-in.</summary>
	/// <value>The explicit live-test permission flag. Prepared inputs default to false; the test runner supplies its separate opt-in.</value>
	public bool Enabled
		{
		get; set;
		}
	/// <summary>A short-lived access token. Contains no password or refresh token; keep the input file private.</summary>
	/// <value>A short-lived access token. Contains no password or refresh token; keep the input file private.</value>
	public string AccessToken { get; set; } = "";
	/// <summary>The access-token expiry as a UTC-capable timestamp.</summary>
	/// <value>The access-token expiry as a UTC-capable timestamp.</value>
	public DateTimeOffset ExpiresAt
		{
		get; set;
		}
	/// <summary>The selected site identifier supplied to read-only live tests.</summary>
	/// <value>The selected site identifier supplied to read-only live tests.</value>
	public string SiteId { get; set; } = "";
	/// <summary>The selected charger serial number supplied to read-only live tests.</summary>
	/// <value>The selected charger serial number supplied to read-only live tests.</value>
	public string ChargerId { get; set; } = "";
	/// <summary>Returns a description that does not expose private test inputs.</summary>
	/// <returns>A fixed private-input description.</returns>
	public override string ToString () => "Live test settings (private)";
	}

internal sealed class LiveAccessTokenProvider (LiveTestSettings settings) : IEaseeTokenProvider
	{
	public Task<string> GetAccessTokenAsync (CancellationToken cancellationToken = default)
		{
		cancellationToken.ThrowIfCancellationRequested ();
		if (string.IsNullOrWhiteSpace (settings.AccessToken) || settings.ExpiresAt <= DateTimeOffset.UtcNow)
			throw new InvalidOperationException ("The live-test access token has expired. Prepare a new test session on the desktop.");
		return Task.FromResult (settings.AccessToken);
		}
	public override string ToString () => "Live test token provider (redacted)";
	}