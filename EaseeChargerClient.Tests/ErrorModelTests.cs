using System.Net;
using System.Net.Http;

using EaseeChargerClient.Models;

namespace EaseeChargerClient.Tests;

[TestFixture]
public sealed class ErrorModelTests
	{
	[Test]
	public async Task PublishedOcppFailureKeepsItsTypedErrorListAndCalculatedCommonSummary ()
		{
		using var handler = new FakeHttp ((_, _) => Task.FromResult (FakeHttp.Json ("{\"title\":\"Invalid\",\"status\":400,\"traceId\":\"trace\",\"errors\":[{\"name\":\"url\",\"reason\":\"Invalid value\",\"code\":\"bad-url\"}]}", HttpStatusCode.BadRequest)));
		using var http = new HttpClient (handler);
		using var client = new EaseeClient (new ()
			{
			TokenProvider = new TokenProvider ()
			}, http);
		var error = await Assert.ThrowsAsync<EaseeApiException> (async () => await client.GetChargerConnectionDetailsEndpointAsync (1, "test"));
		Assert.That (error!.ErrorResponse, Is.TypeOf<Easee_OCPP_Configuration_API_ProblemDetails> ());
		Assert.That (error.Problem!.ValidationFailures![0].Code, Is.EqualTo ("bad-url"));
		Assert.That (error.Problem.TraceId, Is.EqualTo ("trace"));
		}
	[Test]
	public async Task UnspecifiedServerFailureStillKeepsTypedTraceId ()
		{
		using var handler = new FakeHttp ((_, _) => Task.FromResult (FakeHttp.Json ("{\"title\":\"Server failure\",\"traceId\":\"trace\"}", HttpStatusCode.InternalServerError)));
		using var http = new HttpClient (handler);
		using var client = new EaseeClient (new ()
			{
			TokenProvider = new TokenProvider ()
			}, http);
		var error = await Assert.ThrowsAsync<EaseeApiException> (async () => await client.AccountGetProfileAsync ());
		Assert.That (error!.Problem!.TraceId, Is.EqualTo ("trace"));
		}
	}