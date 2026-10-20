using EaseeChargerClient.Testing;

namespace EaseeChargerClient.Hosting.Tests;

[TestFixture]
public sealed class PreparedSessionTests
	{
	[Test]
	public async Task PreparedInputsContainNoRefreshCredentialAndRetainProfileOwnershipUntilDeleted ()
		{
		string name = "offline-" + Guid.NewGuid ().ToString ("N");
		string directory = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.LocalApplicationData), "EaseeChargerClient", "Profiles", name);
		try
			{
			using (var store = new SecureProfileStore (name))
				store.Save (new ()
					{
					SiteId = "site",
					ChargerId = "charger",
					Token = new ()
						{
						AccessToken = "OFFLINE-ACCESS",
						RefreshToken = "NEVER-EXPORT-REFRESH",
						ExpiresAt = DateTimeOffset.UtcNow.AddHours (1)
						}
					});
			string inputDirectory;
			using (var prepared = await PreparedLiveSession.CreateAsync (name))
				{
				inputDirectory = prepared.InputDirectory;
				string json = File.ReadAllText (Path.Combine (inputDirectory, "LiveTestSettings.json"));
				Assert.That (json, Does.Not.Contain ("NEVER-EXPORT-REFRESH").And.Not.Contain ("refreshToken"));
				var settings = EaseeJson.Deserialize<LiveTestSettings> (json)!;
				Assert.That (settings.AccessToken, Is.EqualTo ("OFFLINE-ACCESS"));
				Assert.That (settings.Enabled, Is.False);
				Assert.That (settings.ToString (), Does.Not.Contain ("OFFLINE-ACCESS"));
				Assert.Throws<IOException> (() => { using var competing = new SecureProfileStore (name); });
				}
			Assert.That (Directory.Exists (inputDirectory), Is.False);
			using var reopened = new SecureProfileStore (name);
			Assert.That (reopened.Load ().Token!.RefreshToken, Is.EqualTo ("NEVER-EXPORT-REFRESH"));
			}
		finally
			{
			// Only this test's randomly named profile is removed, without recursive deletion.
			if (Directory.Exists (directory))
				{
				foreach (string file in Directory.GetFiles (directory))
					File.Delete (file);
				Directory.Delete (directory);
				}
			}
		}
	[Test]
	public async Task ExpiredPreparedAccessCannotStartARequestOrRefresh ()
		{
		var provider = new LiveAccessTokenProvider (new ()
			{
			AccessToken = "expired",
			ExpiresAt = DateTimeOffset.UtcNow.AddMinutes (-1)
			});
		await Assert.ThrowsAsync<InvalidOperationException> (async () => await provider.GetAccessTokenAsync ());
		}
	}