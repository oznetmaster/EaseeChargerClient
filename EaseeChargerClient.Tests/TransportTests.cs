using System.Net;
using System.Net.Http;
using System.Text.Json;

using EaseeChargerClient.Models;

using Microsoft.Extensions.Logging;

namespace EaseeChargerClient.Tests;

[TestFixture]
public sealed class TransportTests
	{
	[Test]
	public async Task UnauthorizedControlIsNotReplayedAndSecretsAreNotLogged ()
		{
		var log = new RecordingLogger ();
		using var handler = new FakeHttp ((_, _) => Task.FromResult (FakeHttp.Json ("{\"title\":\"Denied\",\"detail\":\"private\"}", HttpStatusCode.Unauthorized)));
		using var http = new HttpClient (handler);
		using var client = new EaseeClient (new ()
			{
			TokenProvider = new TokenProvider (),
			Logger = log
			}, http);
		EaseeApiException? error = await Assert.ThrowsAsync<EaseeApiException> (async () => await client.ChargerStopChargingAsync ("SECRET-SERIAL"));
		Assert.That (error!.StatusCode, Is.EqualTo (HttpStatusCode.Unauthorized));
		Assert.That (error.Problem?.Title, Is.EqualTo ("Denied"));
		Assert.That (handler.Calls, Is.EqualTo (1));
		Assert.That (string.Join (" ", log.Messages) + error.Message, Does.Not.Contain ("unit-test-token").And.Not.Contain ("SECRET-SERIAL").And.Not.Contain ("private"));
		await Task.CompletedTask;
		}
	[Test]
	public async Task RateLimitHeadersRemainAvailableWithoutAutomaticRetry ()
		{
		using var handler = new FakeHttp ((_, _) =>
		{
			var response = FakeHttp.Json ("not-json", (HttpStatusCode)429);
			response.Headers.TryAddWithoutValidation ("Retry-After", "60");
			return Task.FromResult (response);
		});
		using var http = new HttpClient (handler);
		using var client = new EaseeClient (new ()
			{
			TokenProvider = new TokenProvider ()
			}, http);
		EaseeApiException? error = await Assert.ThrowsAsync<EaseeApiException> (async () => await client.AccountGetProfileAsync ());
		Assert.That (error!.Headers["Retry-After"], Is.EqualTo (new[] { "60" }));
		Assert.That (handler.Calls, Is.EqualTo (1));
		await Task.CompletedTask;
		}
	[Test]
	public async Task CallerCancellationReachesTheHttpHandler ()
		{
		using var cancellation = new CancellationTokenSource ();
		using var handler = new FakeHttp (async (_, token) => { cancellation.Cancel (); await Task.Delay (Timeout.Infinite, token); return FakeHttp.Json ("{}"); });
		using var http = new HttpClient (handler);
		using var client = new EaseeClient (new ()
			{
			TokenProvider = new TokenProvider ()
			}, http);
		await Assert.ThatAsync (async () => await client.AccountGetProfileAsync (cancellationToken: cancellation.Token), Throws.InstanceOf<OperationCanceledException> ());
		await Task.CompletedTask;
		}
	[Test]
	public async Task RequestTimeoutCancelsAnUnresponsiveServer ()
		{
		using var handler = new FakeHttp (async (_, token) => { await Task.Delay (Timeout.Infinite, token); return FakeHttp.Json ("{}"); });
		using var http = new HttpClient (handler);
		using var client = new EaseeClient (new ()
			{
			TokenProvider = new TokenProvider (),
			RequestTimeout = TimeSpan.FromMilliseconds (30)
			}, http);
		await Assert.ThatAsync (async () => await client.AccountGetProfileAsync (), Throws.InstanceOf<OperationCanceledException> ());
		await Task.CompletedTask;
		}
	[Test]
	public async Task DisposingTheClientPreservesCallerOwnedHttpClient ()
		{
		using var handler = new FakeHttp ((_, _) => Task.FromResult (FakeHttp.Json ("{}")));
		using var http = new HttpClient (handler);
		new EaseeClient (httpClient: http).Dispose ();
		Assert.That (handler.Disposed, Is.False);
		using HttpResponseMessage response = await http.GetAsync ("https://example.test");
		Assert.That (response.StatusCode, Is.EqualTo (HttpStatusCode.OK));
		}
	[Test]
	public async Task AuthenticationNeverSendsAnExistingBearerToken ()
		{
		using var handler = new FakeHttp ((request, _) => { Assert.That (request.Headers.Authorization, Is.Null); return Task.FromResult (FakeHttp.Json ("{}")); });
		using var http = new HttpClient (handler);
		using var client = new EaseeClient (new ()
			{
			TokenProvider = new TokenProvider ()
			}, http);
		await client.AccountAuthenticateAsync (new ()
			{
			UserName = "test",
			Password = "private"
			});
		}
	[Test]
	public async Task NoContentScheduleResponseIsDistinctFromAnEmptyModel ()
		{
		using var handler = new FakeHttp ((_, _) => Task.FromResult (new HttpResponseMessage (HttpStatusCode.NoContent)));
		using var http = new HttpClient (handler);
		using var client = new EaseeClient (new ()
			{
			TokenProvider = new TokenProvider ()
			}, http);
		var response = await client.GetChargersSchedulesDailyAsync ("TEST");
		Assert.That (response.Value, Is.Null);
		Assert.That (response.StatusCode, Is.EqualTo (HttpStatusCode.NoContent));
		}
	[TestCase ("a/b ?#%+", "a%2Fb%20%3F%23%25%2B")]
	public async Task IdentifiersCannotChangeTheRoute (string input, string expected)
		{
		using var handler = new FakeHttp ((request, _) => { Assert.That (request.RequestUri!.AbsolutePath, Is.EqualTo ("/api/chargers/" + expected)); return Task.FromResult (FakeHttp.Json ("{}")); });
		using var http = new HttpClient (handler);
		using var client = new EaseeClient (new ()
			{
			TokenProvider = new TokenProvider ()
			}, http);
		await client.ChargerGetChargerByIdAsync (input);
		}
	[TestCase (".")]
	[TestCase ("..")]
	public void DotSegmentsAreRejected (string serial)
		{
		using var client = new EaseeClient ();
		Assert.Throws<ArgumentException> (() => client.ChargerGetChargerByIdAsync (serial));
		}
	[Test]
	public async Task FalseAndZeroAndEmptyQueryValuesAreNotDropped ()
		{
		using var handler = new FakeHttp ((request, _) => { Assert.That (request.RequestUri!.Query, Is.EqualTo ("?alwaysGetChargerAccessLevel=false")); return Task.FromResult (FakeHttp.Json ("{}")); });
		using var http = new HttpClient (handler);
		using var client = new EaseeClient (new ()
			{
			TokenProvider = new TokenProvider ()
			}, http);
		await client.ChargerGetChargerByIdAsync ("TEST", false);
		}
	[Test]
	public async Task WorkflowTraceHeaderIsForwardedAndContentRemainsStreamed ()
		{
		using var handler = new FakeHttp ((request, _) => { Assert.That (request.Headers.GetValues ("traceparent"), Is.EqualTo (new[] { "workflow-value" })); return Task.FromResult (FakeHttp.Json ("event: example\n\n")); });
		using var http = new HttpClient (handler);
		using var client = new EaseeClient (new ()
			{
			TokenProvider = new TokenProvider ()
			}, http);
		var options = new EaseeRequestOptions ();
		options.Headers.Add ("traceparent", "workflow-value");
		using EaseeStreamResponse response = await client.GetWorkflowStreamEndpointAsync ("OPERATOR", options);
		using var reader = new StreamReader (response.Content);
		Assert.That (await reader.ReadToEndAsync (), Is.EqualTo ("event: example\n\n"));
		}
	[Test]
	public void ArbitraryPublishedValuesRetainNestedTypesWithoutJsonLibraryObjects ()
		{
		EaseeValue value = EaseeValue.From (new
			{
			enabled = false,
			current = 0,
			child = new[] { 1, 2 }
			});
		Assert.That (value.Properties["enabled"].Boolean, Is.False);
		Assert.That (value.Properties["current"].Number, Is.Zero);
		Assert.That (value.Properties["child"].Items, Has.Count.EqualTo (2));
		}
	[Test]
	public void OptionalRequestFieldsAreOmittedWhileExplicitFalseAndZeroArePreserved ()
		{
		var fields = new Easee_Cloud_API_DynamicCircuitCurrentTtlDto { Phase1 = 0, Phase2 = 0, Phase3 = 0, TimeToLive = 0 };
		using JsonDocument json = JsonDocument.Parse (JsonSerializer.Serialize (fields));
		Assert.That (json.RootElement.GetProperty ("timeToLive").GetInt32 (), Is.Zero);
		Assert.That (JsonSerializer.Serialize (new Easee_Cloud_API_DynamicCircuitCurrentTtlDto ()), Is.EqualTo ("{}"));
		}
	[Test]
	public void StringEnumsUseThePublishedCasingEvenWithDefaultSerializerOptions ()
		{
		Assert.That (JsonSerializer.Serialize (Scheduled_charging_DayOfWeek.Monday), Is.EqualTo ("\"monday\""));
		Assert.That (JsonSerializer.Deserialize<Scheduled_charging_DayOfWeek> ("\"sunday\""), Is.EqualTo (Scheduled_charging_DayOfWeek.Sunday));
		Assert.Throws<JsonException> (() => JsonSerializer.Deserialize<Scheduled_charging_DayOfWeek> ("true"));
		}
	[Test]
	public void UnknownNumericEnumsAreRetained ()
		{
		var model = JsonSerializer.Deserialize<Easee_Cloud_API_ChargerStateDTO> ("{\"chargerOpMode\":1234}");
		Assert.That ((int?)model!.ChargerOpMode, Is.EqualTo (1234));
		}
	[Test]
	public void OpenSchemaExtensionFieldsRoundTripThroughTypedValues ()
		{
		var model = JsonSerializer.Deserialize<Access_Control_API_ProblemDetails> ("{\"title\":\"Test\",\"correlation\":{\"id\":4}}");
		Assert.That (model!.AdditionalProperties["correlation"].Properties["id"].Number, Is.EqualTo (4));
		Assert.That (JsonSerializer.Serialize (model), Does.Contain ("\"correlation\""));
		}
	private sealed class RecordingLogger : ILogger
		{
		public List<string> Messages { get; } = [];
		public IDisposable? BeginScope<TState> (TState state) where TState : notnull => null;
		public bool IsEnabled (LogLevel logLevel) => true;
		public void Log<TState> (LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => Messages.Add (formatter (state, exception));
		}
	}