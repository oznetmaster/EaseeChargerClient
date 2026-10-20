namespace EaseeChargerClient.Tests;

[TestFixture]
public sealed class RealtimeTests
	{
	[Test]
	public async Task ReconnectionRestoresAllUniqueSubscriptionsAndStopPreventsResubscription ()
		{
		var connection = new FakeConnection ();
		await using var client = new EaseeRealtimeClient (connection, ["A", "a", "B"]);
		await client.StartAsync ();
		await client.AddProductAsync ("C");
		await connection.ReconnectAsync ();
		Assert.That (connection.Products, Is.EqualTo (new[] { "A", "B", "C", "A", "B", "C" }));
		await client.StopAsync ();
		await connection.ReconnectAsync ();
		Assert.That (connection.Products, Has.Count.EqualTo (6));
		}
	[Test]
	public async Task FailedInitialSubscriptionStopsTheConnectionAndAllowsAnExplicitRestart ()
		{
		var connection = new FakeConnection { FailSubscribe = true };
		await using var client = new EaseeRealtimeClient (connection, ["A"]);
		await Assert.ThrowsAsync<IOException> (async () => await client.StartAsync ());
		Assert.That (connection.Stops, Is.EqualTo (1));
		connection.FailSubscribe = false;
		await client.StartAsync ();
		Assert.That (connection.Products, Is.EqualTo (new[] { "A" }));
		}
	[Test]
	public async Task AClosedConnectionCanBeExplicitlyStartedAgain ()
		{
		var connection = new FakeConnection ();
		await using var client = new EaseeRealtimeClient (connection, ["A"]);
		await client.StartAsync ();
		connection.Close ();
		await client.StartAsync ();
		Assert.That (connection.Products, Is.EqualTo (new[] { "A", "A" }));
		}
	[Test]
	public void ObservationWireNamesAndStringValuesMatchTheStreamingContract ()
		{
		var update = EaseeJson.Deserialize<EaseeProductUpdate> ("{\"mid\":\"TEST\",\"dataType\":3,\"id\":21,\"timestamp\":\"2026-10-20T12:00:00Z\",\"value\":\"0.0\"}")!;
		Assert.That (update.DeviceId, Is.EqualTo ("TEST"));
		Assert.That (update.ObservationId, Is.EqualTo (21));
		Assert.That (update.Value, Is.EqualTo ("0.0"));
		}
	private sealed class FakeConnection : IEaseeProductConnection
		{
		public event Func<Task>? Reconnected;
		public event Action? Closed;
		public List<string> Products { get; } = [];
		public int Stops
			{
			get; private set;
			}
		public bool FailSubscribe
			{
			get; set;
			}
		public Task StartAsync (CancellationToken cancellationToken) => Task.CompletedTask;
		public Task SubscribeAsync (string serial, CancellationToken cancellationToken)
			{
			if (FailSubscribe)
				throw new IOException ();
			Products.Add (serial);
			return Task.CompletedTask;
			}
		public Task StopAsync (CancellationToken cancellationToken)
			{
			Stops++;
			return Task.CompletedTask;
			}
		public Task ReconnectAsync () => Reconnected?.Invoke () ?? Task.CompletedTask;
		public void Close () => Closed?.Invoke ();
		public ValueTask DisposeAsync () => default;
		}
	}