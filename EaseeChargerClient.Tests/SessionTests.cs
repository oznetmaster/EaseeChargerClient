using System.Net.Http;

namespace EaseeChargerClient.Tests;

[TestFixture]
public sealed class SessionTests
	{
	[Test]
	public async Task ConcurrentCallersRotateOnceAndPersistBeforeReceivingTheToken ()
		{
		bool saved = false;
		using var handler = new FakeHttp (async (request, _) => { Assert.That (await request.Content!.ReadAsStringAsync (), Is.EqualTo ("{\"refreshToken\":\"old-refresh\"}")); await Task.Delay (20); return FakeHttp.Json ("{\"accessToken\":\"new-access\",\"refreshToken\":\"new-refresh\",\"expiresIn\":3600}"); });
		using var http = new HttpClient (handler);
		using var auth = new EaseeClient (httpClient: http);
		using var session = new EaseeSession (auth, (token, _) => { if (!token.RequiresReauthentication) { Assert.That (token.RefreshToken, Is.EqualTo ("new-refresh")); saved = true; } return Task.CompletedTask; }, new ()
			{
			AccessToken = "old-access",
			RefreshToken = "old-refresh",
			ExpiresAt = DateTimeOffset.UtcNow
			});
		string[] values = await Task.WhenAll (Enumerable.Range (0, 12).Select (_ => session.GetAccessTokenAsync ()));
		Assert.That (values, Is.All.EqualTo ("new-access"));
		Assert.That (saved, Is.True);
		Assert.That (handler.Calls, Is.EqualTo (1));
		}
	[Test]
	public async Task FailedPersistenceBlocksTheTokenChainUntilSignIn ()
		{
		using var handler = new FakeHttp ((_, _) => Task.FromResult (FakeHttp.Json ("{\"accessToken\":\"new\",\"refreshToken\":\"new\",\"expiresIn\":3600}")));
		using var http = new HttpClient (handler);
		using var auth = new EaseeClient (httpClient: http);
		using var session = new EaseeSession (auth, (_, _) => throw new IOException ("Disk failure"), new ()
			{
			RefreshToken = "old",
			ExpiresAt = DateTimeOffset.UtcNow
			});
		await Assert.ThrowsAsync<IOException> (async () => await session.GetAccessTokenAsync ());
		await Assert.ThrowsAsync<InvalidOperationException> (async () => await session.GetAccessTokenAsync ());
		Assert.That (handler.Calls, Is.Zero);
		}
	[Test]
	public async Task AmbiguousRefreshFailureIsNotRetriedWithASingleUseToken ()
		{
		using var handler = new FakeHttp ((_, _) => throw new HttpRequestException ("Connection lost"));
		using var http = new HttpClient (handler);
		using var auth = new EaseeClient (httpClient: http);
		using var session = new EaseeSession (auth, (_, _) => Task.CompletedTask, new ()
			{
			RefreshToken = "old",
			ExpiresAt = DateTimeOffset.UtcNow
			});
		await Assert.ThrowsAsync<HttpRequestException> (async () => await session.GetAccessTokenAsync ());
		await Assert.ThrowsAsync<InvalidOperationException> (async () => await session.GetAccessTokenAsync ());
		Assert.That (handler.Calls, Is.EqualTo (1));
		}
	[TestCase ("null")]
	[TestCase ("{}")]
	[TestCase ("{\"accessToken\":\"a\",\"refreshToken\":\"r\",\"expiresIn\":0}")]
	public async Task MalformedAuthenticationIsRejected (string response)
		{
		using var handler = new FakeHttp ((_, _) => Task.FromResult (FakeHttp.Json (response)));
		using var http = new HttpClient (handler);
		using var auth = new EaseeClient (httpClient: http);
		using var session = new EaseeSession (auth, (_, _) => throw new AssertionException ("Invalid credentials must not be saved."));
		await Assert.ThrowsAsync<InvalidDataException> (async () => await session.SignInAsync ("test", "test"));
		}
	}