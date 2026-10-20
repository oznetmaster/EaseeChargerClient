using System.Net;
using System.Text.Json;

using EaseeChargerClient.Hosting;
using EaseeChargerClient.Testing;

namespace EaseeChargerClient.LiveTests;

[TestFixture, Category ("Live"), NonParallelizable]
public sealed class LiveReadTests
	{
	private EaseeClient _client = null!;
	private LiveAccessTokenProvider _tokens = null!;
	private LiveTestSettings _settings = null!;
	[SetUp]
	public void SetUp ()
		{
		bool enabled = TestContext.Parameters.Get ("EnableLiveTests", Environment.GetEnvironmentVariable ("EASEE_ENABLE_LIVE_TESTS") == "1");
		if (!enabled)
			Assert.Ignore ("Live tests are disabled.");
		string directory = TestContext.Parameters.Get ("TestDataDirectory", Environment.GetEnvironmentVariable ("EASEE_LIVE_TEST_DATA_DIRECTORY") ?? AppDomain.CurrentDomain.BaseDirectory);
		string path = Path.Combine (directory, "LiveTestSettings.json");
		if (!File.Exists (path))
			throw new InvalidOperationException ("Prepared live-test inputs are missing. Use the desktop live-profile wrapper.");
		_settings = EaseeJson.Deserialize<LiveTestSettings> (File.ReadAllText (path)) ?? throw new InvalidDataException ("Invalid prepared live-test inputs.");
		if (string.IsNullOrWhiteSpace (_settings.SiteId) || string.IsNullOrWhiteSpace (_settings.ChargerId))
			throw new InvalidDataException ("The prepared site and charger selections are required.");
		_tokens = new LiveAccessTokenProvider (_settings);
		_client = new EaseeClient (new ()
			{
			TokenProvider = _tokens
			});
		}
	[TearDown] public void TearDown () => _client?.Dispose ();
	private string Charger => _settings.ChargerId;
	private string Site => _settings.SiteId;
	[Test]
	public async Task AccountIsAuthorized ()
		{
		var result = await _client.AccountGetProfileAsync ();
		Assert.That (result.StatusCode, Is.EqualTo (HttpStatusCode.OK));
		Assert.That (result.Value, Is.Not.Null);
		}
	[Test]
	public async Task SelectedChargerIsAccessible ()
		{
		var result = await _client.ChargerGetChargerByIdAsync (Charger);
		Assert.That (result.StatusCode, Is.EqualTo (HttpStatusCode.OK));
		Assert.That (result.Value?.Id, Is.EqualTo (Charger).IgnoreCase);
		var properties = await _client.ResourcesGetObservationPropertiesAsync ();
		int id = properties.Value!.Single (p => p.Name == "ChargerOpMode").ObservationId ?? throw new InvalidDataException ("The mode observation ID is missing.");
		var observations = await _client.GetobservationsAsync (Charger, id.ToString (System.Globalization.CultureInfo.InvariantCulture));
		var mode = observations.Value!.Observations!.Single (o => o.Id == id);
		Assert.That (mode, Is.TypeOf<EaseeChargerClient.Models.EaseeIntegerObservation> ());
		Assert.That (((EaseeChargerClient.Models.EaseeIntegerObservation)mode).Value, Is.Not.Null);
		}
	[Test]
	public async Task SelectedSiteIsAccessible ()
		{
		var result = await _client.SiteGetSiteByIdAsync (Site);
		Assert.That (result.StatusCode, Is.EqualTo (HttpStatusCode.OK));
		Assert.That (result.Value, Is.Not.Null);
		}
	[Test]
	public async Task SchedulesAreReadable ()
		{
		var result = await _client.GetChargersSchedulesAsync (Charger);
		Assert.That (result.StatusCode, Is.EqualTo (HttpStatusCode.OK));
		Assert.That (result.Value, Is.Not.Null);
		}
	[Test]
	public async Task OngoingSessionIsReadable ()
		{
		var result = await _client.ChargersGetOngoingSessionDetailsAsync (Charger);
		Assert.That ((int)result.StatusCode, Is.InRange (200, 299));
		}
	[Test]
	public async Task StreamSuppliesSelectedProductsCurrentState ()
		{
		var received = new TaskCompletionSource<EaseeProductUpdate> (TaskCreationOptions.RunContinuationsAsynchronously);
		await using var stream = new EaseeRealtimeClient (_tokens, [Charger], update => { if (string.Equals (update.DeviceId, Charger, StringComparison.OrdinalIgnoreCase)) received.TrySetResult (update); return Task.CompletedTask; });
		using var timeout = new CancellationTokenSource (TimeSpan.FromSeconds (60));
		await stream.StartAsync (timeout.Token);
		Task winner = await Task.WhenAny (received.Task, Task.Delay (TimeSpan.FromSeconds (45), timeout.Token));
		Assert.That (winner, Is.SameAs (received.Task), "No selected-product observation arrived within 45 seconds.");
		Assert.That ((await received.Task).Timestamp, Is.GreaterThan (DateTimeOffset.MinValue));
		}
	/// <summary>Additional read scenarios are private because operator roles and resource IDs differ by account.</summary>
	[Test]
	public async Task PrivatelyConfiguredReadScenarios ()
		{
		string? path = Environment.GetEnvironmentVariable ("EASEE_LIVE_SCENARIOS");
		if (string.IsNullOrWhiteSpace (path))
			{
			Assert.Ignore ("No additional role-specific live scenarios configured.");
			return;
			}
		List<LiveScenario> scenarios = EaseeJson.Deserialize<List<LiveScenario>> (File.ReadAllText (path!)) ?? throw new InvalidDataException ("Invalid scenarios.");
		foreach (LiveScenario scenario in scenarios)
			{
			EaseeOperation operation = OperationRunner.Find (scenario.OperationId);
			Assert.That (operation.HttpMethod, Is.EqualTo ("GET"), "Read suites cannot execute modifying operations.");
			object? result = await OperationRunner.InvokeAsync (_client, operation, scenario.Arguments, null);
			if (result is IDisposable stream)
				stream.Dispose ();
			}
		}
	}

public sealed class LiveScenario
	{
	public string OperationId { get; set; } = "";
	public Dictionary<string, string> Arguments { get; set; } = [];
	}