using System.Net.Http;

namespace EaseeChargerClient.Tests;

[TestFixture]
public sealed class SessionRecoveryTests
	{
	[Test]
	public async Task AnInterruptedExchangeRemainsBlockedAfterReopeningThePersistedToken ()
		{
		EaseeToken? disk = null;
		using var handler = new FakeHttp ((_, _) => throw new HttpRequestException ());
		using var http = new HttpClient (handler);
		using var auth = new EaseeClient (httpClient: http);
		using (var session = new EaseeSession (auth, (token, _) => { disk = EaseeJson.Deserialize<EaseeToken> (EaseeJson.Serialize (token)); return Task.CompletedTask; }, new ()
			{
			RefreshToken = "single-use",
			ExpiresAt = DateTimeOffset.UtcNow
			}))
			await Assert.ThrowsAsync<HttpRequestException> (async () => await session.GetAccessTokenAsync ());
		Assert.That (disk?.RequiresReauthentication, Is.True);
		using var reopened = new EaseeSession (auth, (_, _) => Task.CompletedTask, disk);
		await Assert.ThrowsAsync<InvalidOperationException> (async () => await reopened.GetAccessTokenAsync ());
		Assert.That (handler.Calls, Is.EqualTo (1));
		}
	}