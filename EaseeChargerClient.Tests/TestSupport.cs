using System.Net;
using System.Net.Http;

namespace EaseeChargerClient.Tests;

internal sealed class TokenProvider : IEaseeTokenProvider
	{
	public Task<string> GetAccessTokenAsync (CancellationToken cancellationToken = default) => Task.FromResult ("unit-test-token");
	}

internal sealed class FakeHttp (Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> action) : HttpMessageHandler
	{
	public int Calls
		{
		get; private set;
		}
	public bool Disposed
		{
		get; private set;
		}
	protected override Task<HttpResponseMessage> SendAsync (HttpRequestMessage request, CancellationToken cancellationToken)
		{
		Calls++;
		return action (request, cancellationToken);
		}
	protected override void Dispose (bool disposing)
		{
		Disposed = true;
		base.Dispose (disposing);
		}
	public static HttpResponseMessage Json (string json, HttpStatusCode code = HttpStatusCode.OK) => new (code) { Content = new StringContent (json, System.Text.Encoding.UTF8, "application/json") };
	}