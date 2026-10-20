namespace EaseeChargerClient.Hosting.Tests;

[TestFixture]
public sealed class ProfileTests
	{
	private string _root = null!;
	[SetUp]
	public void SetUp ()
		{
		_root = Path.Combine (TestContext.CurrentContext.WorkDirectory, "private-test-" + Guid.NewGuid ().ToString ("N"));
		}
	[TearDown]
	public void TearDown ()
		{
		if (Directory.Exists (_root))
			Directory.Delete (_root, true);
		}
	[Test]
	public void ProfileIsEncryptedAndReopensWithTheLatestRotatedCredentials ()
		{
		using (var store = new SecureProfileStore ("test", _root))
			{
			store.Save (new ()
				{
				ChargerId = "PRIVATE-CHARGER",
				Token = new ()
					{
					AccessToken = "FIRST-ACCESS",
					RefreshToken = "FIRST-REFRESH"
					}
				});
			store.Save (new ()
				{
				ChargerId = "PRIVATE-CHARGER",
				Token = new ()
					{
					AccessToken = "SECOND-ACCESS",
					RefreshToken = "SECOND-REFRESH"
					}
				});
			}
		string bytes = System.Text.Encoding.UTF8.GetString (File.ReadAllBytes (Path.Combine (_root, "test", "profile.dpapi")));
		Assert.That (bytes, Does.Not.Contain ("PRIVATE-CHARGER").And.Not.Contain ("SECOND-ACCESS").And.Not.Contain ("SECOND-REFRESH"));
		using var reopened = new SecureProfileStore ("test", _root);
		Assert.That (reopened.Load ().Token!.RefreshToken, Is.EqualTo ("SECOND-REFRESH"));
		}
	[Test]
	public async Task ConcurrentSavesRemainAtomicAndDisposeReleasesOwnership ()
		{
		var store = new SecureProfileStore ("concurrent", _root);
		try
			{
			await Task.WhenAll (Enumerable.Range (0, 16).Select (i => Task.Run (() => store.Save (new () { SiteId = i.ToString () }))));
			Assert.That (int.Parse (store.Load ().SiteId!), Is.InRange (0, 15));
			}
		finally { store.Dispose (); }
		Assert.Throws<ObjectDisposedException> (() => store.Save (new ()));
		using var next = new SecureProfileStore ("concurrent", _root);
		Assert.That (next.Load ().SiteId, Is.Not.Null);
		}
	[Test]
	public void ASecondOwnerCannotOpenAnActiveTokenChain ()
		{
		using var store = new SecureProfileStore ("test", _root);
		Assert.Throws<IOException> (() => { using var second = new SecureProfileStore ("test", _root); });
		}
	[TestCase ("../escape")]
	[TestCase ("a/b")]
	[TestCase ("")]
	public void InvalidProfileNamesCannotEscapeTheProfileRoot (string name) => Assert.Throws<ArgumentException> (() => { using var store = new SecureProfileStore (name, _root); });
	[Test]
	public void EncryptedProfileTamperingFailsClosed ()
		{
		using var store = new SecureProfileStore ("test", _root);
		store.Save (new ());
		string file = Path.Combine (_root, "test", "profile.dpapi");
		byte[] bytes = File.ReadAllBytes (file);
		bytes[bytes.Length / 2] ^= 0xff;
		File.WriteAllBytes (file, bytes);
		Assert.Throws<System.Security.Cryptography.CryptographicException> (() => store.Load ());
		}
	[Test]
	public async Task OperationArgumentsRejectUnknownNamesBeforeHttpExecution ()
		{
		using var client = new EaseeClient ();
		await Assert.ThrowsAsync<ArgumentException> (async () => await OperationRunner.InvokeAsync (client, OperationRunner.Find ("Charger_GetChargerById"), new Dictionary<string, string> { ["typo"] = "value" }, null));
		}
	}