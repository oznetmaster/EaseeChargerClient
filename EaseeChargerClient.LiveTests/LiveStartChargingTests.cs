using EaseeChargerClient.Models;
using EaseeChargerClient.Testing;

namespace EaseeChargerClient.LiveTests;

[TestFixture, Category ("Live"), Category ("LiveControl"), NonParallelizable, Explicit ("Requires a separately authorized single start command.")]
public sealed class LiveStartChargingTests
	{
	[Test]
	public async Task OneAuthorizedStartCommandIsAcceptedAndTheSelectedChargerRemainsObservable ()
		{
		if (Environment.GetEnvironmentVariable ("EASEE_ENABLE_SINGLE_START_TEST") != "1" || Environment.GetEnvironmentVariable ("EASEE_ENABLE_LIVE_TESTS") != "1")
			Assert.Ignore ("The single start-charging command has not been authorized for this run.");
		string directory = Environment.GetEnvironmentVariable ("EASEE_LIVE_TEST_DATA_DIRECTORY") ?? throw new InvalidOperationException ("Prepare a secure live-profile session first.");
		var settings = EaseeJson.Deserialize<LiveTestSettings> (File.ReadAllText (Path.Combine (directory, "LiveTestSettings.json"))) ?? throw new InvalidDataException ("Missing prepared live inputs.");
		if (string.IsNullOrWhiteSpace (settings.SiteId) || string.IsNullOrWhiteSpace (settings.ChargerId))
			throw new InvalidDataException ("A saved site and charger are required.");
		using var client = new EaseeClient (new ()
			{
			TokenProvider = new LiveAccessTokenProvider (settings)
			});
		using var timeout = new CancellationTokenSource (TimeSpan.FromSeconds (90));
		async Task<Easee_Cloud_API_ChargerStateDTO> State ()
			{
			var response = await client.SiteGetSiteStateAsync (settings.SiteId, cancellationToken: timeout.Token);
			return response.Value?.CircuitStates?.SelectMany (c => c.ChargerStates ?? []).SingleOrDefault (c => string.Equals (c.ChargerID, settings.ChargerId, StringComparison.OrdinalIgnoreCase))?.ChargerState ?? throw new InvalidDataException ("The selected charger is missing from the site state.");
			}
		var before = await State ();
		Assert.That (before.ChargerOpMode, Is.Not.Null.And.Not.EqualTo (Easee_Cloud_API_OpModeType.Error));
		var properties = await client.ResourcesGetObservationPropertiesAsync (cancellationToken: timeout.Token);
		int modeId = properties.Value!.Single (p => p.Name == "ChargerOpMode").ObservationId ?? throw new InvalidDataException ("The mode observation ID is missing.");
		var observed = new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);
		int observations = 0;
		await using var stream = new EaseeRealtimeClient (new LiveAccessTokenProvider (settings), [settings.ChargerId], update =>
		{
			if (string.Equals (update.DeviceId, settings.ChargerId, StringComparison.OrdinalIgnoreCase) && update.ObservationId == modeId)
				{
				Interlocked.Increment (ref observations);
				observed.TrySetResult (true);
				}
			return Task.CompletedTask;
		});
		await stream.StartAsync (timeout.Token);
		Assert.That (await Task.WhenAny (observed.Task, Task.Delay (TimeSpan.FromSeconds (20), timeout.Token)), Is.SameAs (observed.Task), "The selected charger's current mode was not received before sending a control.");
		// Exactly one POST. No retries, resume, stop, settings changes or schedule overrides.
		var command = await client.AuthorizeChargingStartChargingAsync (settings.ChargerId, cancellationToken: timeout.Token);
		Assert.That ((int)command.StatusCode, Is.InRange (200, 299));
		await Task.Delay (TimeSpan.FromSeconds (20), timeout.Token);
		var after = await State ();
		TestContext.Out.WriteLine ($"Single start HTTP {(int)command.StatusCode}; mode before={before.ChargerOpMode}; mode after={after.ChargerOpMode}; power after={after.TotalPower}; mode observations={observations}.");
		Assert.That (after.ChargerOpMode, Is.Not.Null.And.Not.EqualTo (Easee_Cloud_API_OpModeType.Error));
		// A full vehicle may stay Completed. A successful POST alone does not prove energy delivery.
		}
	}