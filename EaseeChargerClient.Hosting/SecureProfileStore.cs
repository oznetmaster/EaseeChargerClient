using System.Security.Cryptography;
using System.Text;

namespace EaseeChargerClient.Hosting;

/// <summary>Holds private account tokens and a saved site and charger selection.</summary>
public sealed class EaseeProfile
	{
	/// <summary>The saved site identifier, or null when no site has been selected.</summary>
	/// <value>The saved site identifier, or null when no site has been selected.</value>
	public string? SiteId
		{
		get; set;
		}
	/// <summary>The saved charger serial number, or null when no charger has been selected.</summary>
	/// <value>The saved charger serial number, or null when no charger has been selected.</value>
	public string? ChargerId
		{
		get; set;
		}
	/// <summary>The private rotating token pair, or null before sign-in.</summary>
	/// <value>The private rotating token pair, or null before sign-in.</value>
	public EaseeToken? Token
		{
		get; set;
		}
	/// <summary>Returns a description that does not expose private profile fields.</summary>
	/// <returns>A fixed private-profile description.</returns>
	public override string ToString () => "Easee profile (private)";
	}

/// <summary>Current-user Windows DPAPI encryption, atomic saves and an exclusive lock for the entire profile lifetime.</summary>
public sealed class SecureProfileStore : IDisposable
	{
	private readonly FileStream _lease;
	private readonly string _path;
	private readonly object _sync = new ();
	private bool _disposed;
	private static readonly byte[] ENTROPY = Encoding.UTF8.GetBytes ("EaseeChargerClient.Profiles.v1");
	/// <summary>Acquires exclusive ownership of a Windows profile for the store lifetime.</summary>
	/// <exception cref="ArgumentException">The profile name is invalid.</exception>
	/// <exception cref="IOException">Another process holds the profile lease, or the profile directory cannot be accessed.</exception>
	/// <param name="profileName">The private Windows profile name, containing at most 64 letters, digits, hyphens or underscores.</param>
	public SecureProfileStore (string profileName) : this (profileName, Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.LocalApplicationData), "EaseeChargerClient", "Profiles")) { }
	internal SecureProfileStore (string profileName, string root)
		{
		if (string.IsNullOrWhiteSpace (profileName) || profileName.Length > 64 || profileName.Any (c => !(char.IsLetterOrDigit (c) || c is '-' or '_')))
			throw new ArgumentException ("Profile names may contain letters, digits, hyphens and underscores.", nameof (profileName));
		string directory = Path.Combine (root, profileName);
		Directory.CreateDirectory (directory);
		_path = Path.Combine (directory, "profile.dpapi");
		_lease = new FileStream (Path.Combine (directory, "profile.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
		}
	/// <summary>Decrypts the private profile for the current Windows user.</summary>
	/// <returns>The saved profile, or an empty profile when no encrypted file exists.</returns>
	/// <exception cref="System.Security.Cryptography.CryptographicException">The profile cannot be decrypted by the current user.</exception>
	/// <exception cref="InvalidDataException">The encrypted profile has no valid model.</exception>
	/// <exception cref="ObjectDisposedException">The store has been disposed.</exception>
	public EaseeProfile Load ()
		{
		lock (_sync)
			{
			CheckDisposed ();
			return LoadCore ();
			}
		}
	private EaseeProfile LoadCore ()
		{
		if (!File.Exists (_path))
			return new ();
		byte[] clear = ProtectedData.Unprotect (File.ReadAllBytes (_path), ENTROPY, DataProtectionScope.CurrentUser);
		try
			{
			return EaseeJson.Deserialize<EaseeProfile> (Encoding.UTF8.GetString (clear)) ?? throw new InvalidDataException ("Invalid credential profile.");
			}
		finally { Array.Clear (clear, 0, clear.Length); }
		}
	/// <summary>Encrypts and atomically saves the complete profile for the current Windows user.</summary>
	/// <exception cref="IOException">The profile could not be durably saved.</exception>
	/// <exception cref="ObjectDisposedException">The store has been disposed.</exception>
	/// <param name="profile">The private profile to encrypt and atomically save.</param>
	public void Save (EaseeProfile profile)
		{
		lock (_sync)
			{
			CheckDisposed ();
			SaveCore (profile);
			}
		}
	private void SaveCore (EaseeProfile profile)
		{
		byte[] clear = Encoding.UTF8.GetBytes (EaseeJson.Serialize (profile));
		byte[] encrypted;
		try
			{
			encrypted = ProtectedData.Protect (clear, ENTROPY, DataProtectionScope.CurrentUser);
			}
		finally { Array.Clear (clear, 0, clear.Length); }
		string temporary = _path + "." + Guid.NewGuid ().ToString ("N") + ".tmp";
		try
			{
			using (var file = new FileStream (temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
				{
				file.Write (encrypted, 0, encrypted.Length);
				file.Flush (true);
				}
			if (File.Exists (_path))
				ReplaceWithBoundedSharingRetry (temporary);
			else
				File.Move (temporary, _path);
			}
		finally { if (File.Exists (temporary)) File.Delete (temporary); }
		}
	private void ReplaceWithBoundedSharingRetry (string temporary)
		{
		for (int attempt = 0; ; attempt++)
			{
			try
				{
				File.Replace (temporary, _path, null);
				return;
				}
			catch (IOException error) when (attempt < 4 && ((error.HResult & 0xffff) is 32 or 33 or 1175) && File.Exists (temporary) && File.Exists (_path))
				{
				Thread.Sleep (50 << attempt);
				}
			}
		}
	private void CheckDisposed ()
		{
		if (_disposed)
			throw new ObjectDisposedException (nameof (SecureProfileStore));
		}
	/// <summary>Releases the exclusive profile lease.</summary>
	public void Dispose ()
		{
		lock (_sync)
			{
			if (_disposed)
				return;
			_disposed = true;
			_lease.Dispose ();
			}
		}
	}

/// <summary>Owns one private profile and its session. Dispose before another process uses this profile.</summary>
public sealed class EaseeProfileSession : IDisposable
	{
	private readonly SecureProfileStore _store;
	private readonly EaseeClient _authentication;
	/// <summary>The current in-memory private profile. Use SaveSelection to durably update its resource selection.</summary>
	/// <value>The current in-memory private profile. Use SaveSelection to durably update its resource selection.</value>
	public EaseeProfile Profile
		{
		get;
		}
	/// <summary>The profile-owned session that serializes and durably saves token rotation.</summary>
	/// <value>The profile-owned session that serializes and durably saves token rotation.</value>
	public EaseeSession Session
		{
		get;
		}
	/// <summary>The profile-owned authenticated REST client.</summary>
	/// <value>The profile-owned authenticated REST client.</value>
	public EaseeClient Client
		{
		get;
		}
	/// <summary>Opens an exclusively owned profile and creates its token session and REST client.</summary>
	/// <param name="profileName">The private Windows profile name, containing at most 64 letters, digits, hyphens or underscores.</param>
	public EaseeProfileSession (string profileName)
		{
		_store = new (profileName);
		try
			{
			Profile = _store.Load ();
			_authentication = new ();
			Session = new (_authentication, (token, _) => { Profile.Token = token; _store.Save (Profile); return Task.CompletedTask; }, Profile.Token);
			Client = new (new ()
				{
				TokenProvider = Session
				});
			}
		catch { _store.Dispose (); throw; }
		}
	/// <summary>Durably saves the selected site and charger identifiers.</summary>
	/// <param name="siteId">The selected site identifier, or null to clear the selection.</param>
	/// <param name="chargerId">The selected charger serial number, or null to clear the selection.</param>
	public void SaveSelection (string? siteId, string? chargerId)
		{
		Profile.SiteId = siteId;
		Profile.ChargerId = chargerId;
		_store.Save (Profile);
		}
	/// <summary>Disposes the owned clients and session and releases the private profile lease.</summary>
	public void Dispose ()
		{
		Client.Dispose ();
		Session.Dispose ();
		_authentication.Dispose ();
		_store.Dispose ();
		}
	}