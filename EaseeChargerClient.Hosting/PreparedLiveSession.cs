using System.Text;

using EaseeChargerClient.Testing;

namespace EaseeChargerClient.Hosting;

/// <summary>Holds the desktop profile lease until a blocking local or remote test command finishes.</summary>
public sealed class PreparedLiveSession : IDisposable
	{
	private readonly EaseeProfileSession _profile;
	private readonly string _file;
	/// <summary>The private temporary directory containing short-lived access-only test inputs. Deleted on disposal.</summary>
	/// <value>The private temporary directory containing short-lived access-only test inputs. Deleted on disposal.</value>
	public string InputDirectory
		{
		get;
		}
	private PreparedLiveSession (EaseeProfileSession profile, string directory)
		{
		_profile = profile;
		InputDirectory = directory;
		_file = Path.Combine (directory, "LiveTestSettings.json");
		}
	/// <summary>Acquires a profile lease and writes private, short-lived access-only test inputs.</summary>
	/// <returns>A prepared input session. Dispose it after the blocking test process exits.</returns>
	/// <exception cref="InvalidOperationException">A site and charger selection or usable account session is missing.</exception>
	/// <exception cref="IOException">The profile lease or private input file cannot be acquired.</exception>
	/// <param name="profileName">The private Windows profile name, containing at most 64 letters, digits, hyphens or underscores.</param>
	/// <param name="cancellationToken">Cancels the asynchronous operation. Cancellation does not undo a request already accepted by the server.</param>
	public static async Task<PreparedLiveSession> CreateAsync (string profileName, CancellationToken cancellationToken = default)
		{
		var profile = new EaseeProfileSession (profileName);
		PreparedLiveSession? prepared = null;
		try
			{
			if (string.IsNullOrWhiteSpace (profile.Profile.SiteId) || string.IsNullOrWhiteSpace (profile.Profile.ChargerId))
				throw new InvalidOperationException ("Save the site and charger selection in the profile before running live tests.");
			string access = await profile.Session.GetAccessTokenAsync (TimeSpan.FromMinutes (15), cancellationToken).ConfigureAwait (false);
			var inputs = new LiveTestSettings
				{
				AccessToken = access,
				ExpiresAt = profile.Profile.Token!.ExpiresAt,
				SiteId = profile.Profile.SiteId!,
				ChargerId = profile.Profile.ChargerId!,
				Enabled = false
				};
			string directory = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.LocalApplicationData), "EaseeChargerClient", "LiveInputs", Guid.NewGuid ().ToString ("N"));
			Directory.CreateDirectory (directory);
			prepared = new PreparedLiveSession (profile, directory);
			byte[] bytes = Encoding.UTF8.GetBytes (EaseeJson.Serialize (inputs));
			try
				{
				using var file = new FileStream (prepared._file, FileMode.CreateNew, FileAccess.Write, FileShare.None);
				file.Write (bytes, 0, bytes.Length);
				file.Flush (true);
				}
			finally { Array.Clear (bytes, 0, bytes.Length); }
			return prepared;
			}
		catch
			{
			if (prepared is not null)
				prepared.Dispose ();
			else
				profile.Dispose ();
			throw;
			}
		}
	/// <summary>Deletes the temporary input file and releases the profile lease.</summary>
	public void Dispose ()
		{
		try
			{
			if (File.Exists (_file))
				File.Delete (_file);
			if (Directory.Exists (InputDirectory))
				Directory.Delete (InputDirectory);
			}
		finally { _profile.Dispose (); }
		}
	}