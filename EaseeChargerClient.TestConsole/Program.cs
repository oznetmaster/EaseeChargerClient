using System.Globalization;
using System.Text;
using System.Text.Json;

using EaseeChargerClient.Hosting;

namespace EaseeChargerClient.TestConsole;

public static class Program
	{
	public static async Task<int> Main (string[] args)
		{
		using var cancel = new CancellationTokenSource ();
		Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancel.Cancel (); };
		try
			{
			if (args.Length != 0)
				return await ExecuteAsync (args, cancel.Token);
			Console.WriteLine ("Easee charger workbench. Type help for commands; exit to close. Passwords are entered only by login.");
			while (!cancel.IsCancellationRequested)
				{
				Console.Write ("easee> ");
				string? line = Console.ReadLine ();
				if (line is null || line.Trim ().Equals ("exit", StringComparison.OrdinalIgnoreCase))
					break;
				try
					{
					await ExecuteAsync (Tokenize (line).ToArray (), cancel.Token);
					}
				catch (Exception error) { WriteError (error); }
				}
			return 0;
			}
		catch (OperationCanceledException) { Console.Error.WriteLine ("Cancelled."); return 130; }
		catch (Exception error) { WriteError (error); return 1; }
		}
	private static void WriteError (Exception error) => Console.Error.WriteLine (error is EaseeApiException api ? $"{api.OperationId}: HTTP {(int)api.StatusCode}." : error is ArgumentException or InvalidOperationException ? error.Message : error.GetType ().Name + ": operation failed. Credentials were not printed.");
	internal static async Task<int> ExecuteAsync (string[] args, CancellationToken cancellationToken)
		{
		if (args.Length == 0 || args[0] == "help")
			{
			Console.WriteLine ("""
login --profile NAME                         Masked account sign-in; saves encrypted tokens
select --profile NAME --site ID --charger ID Save a private device selection
operations [--category TEXT]                  List all supported operations
describe OPERATION                           Show parameter names and types
call OPERATION --profile NAME [--arg name=value] [--body-file FILE]
               [--header name=value] [--output FILE] [--allow-write]
stream --profile NAME [--seconds 60]          Receive selected charger's real-time data
status --profile NAME                        Show whether credentials and selection are configured
with-live-profile --profile NAME -- COMMAND  Supply temporary read-only inputs to a blocking test command

Use --arg repeatedly. Strings are literal; numbers, dates, enums and arrays use JSON syntax.
Each modifying operation requires --allow-write. Passwords and tokens are never accepted as options.
No arguments opens this interactive shell. Exit codes: 0 success, 1 failure, 130 cancelled.
""");
			return 0;
			}
		if (args[0] == "with-live-profile")
			return await LiveCommand.RunAsync (args, cancellationToken);
		string verb = args[0];
		var parsed = ParseOptions (args.Skip (verb is "call" or "describe" ? 2 : 1).ToArray ());
		if (verb == "operations")
			{
			string category = One (parsed, "category") ?? "";
			foreach (EaseeOperation op in EaseeOperations.All.Where (x => x.Category.IndexOf (category, StringComparison.OrdinalIgnoreCase) >= 0))
				Console.WriteLine ($"{op.Id,-55} {op.HttpMethod,-6} {op.Summary}");
			return 0;
			}
		if (verb == "describe")
			{
			Console.WriteLine (OperationRunner.Describe (OperationRunner.Find (args.ElementAtOrDefault (1) ?? "")));
			return 0;
			}
		if (verb is not ("login" or "select" or "status" or "call" or "stream"))
			throw new ArgumentException ("Unknown command. Use help.");
		using var profile = new EaseeProfileSession (One (parsed, "profile") ?? "default");
		if (verb == "status")
			{
			Console.WriteLine ($"Credentials: {(profile.Profile.Token is null ? "sign-in required" : "configured")}; site: {(profile.Profile.SiteId is null ? "not selected" : "selected")}; charger: {(profile.Profile.ChargerId is null ? "not selected" : "selected")}.");
			return 0;
			}
		if (verb == "login")
			{
			if (Console.IsInputRedirected)
				throw new InvalidOperationException ("Use an interactive terminal or the desktop app for secure sign-in.");
			Console.Write ("Easee email or phone: ");
			string username = Console.ReadLine () ?? "";
			Console.Write ("Password: ");
			string password = ReadPassword ();
			await profile.Session.SignInAsync (username, password, cancellationToken);
			Console.WriteLine ("Signed in. Tokens saved encrypted for this Windows user.");
			return 0;
			}
		if (verb == "select")
			{
			profile.SaveSelection (One (parsed, "site"), One (parsed, "charger"));
			Console.WriteLine ("Private selection saved.");
			return 0;
			}
		if (verb == "stream")
			{
			string charger = profile.Profile.ChargerId ?? throw new InvalidOperationException ("Select a charger first.");
			int seconds = int.Parse (One (parsed, "seconds") ?? "60", CultureInfo.InvariantCulture);
			if (seconds < 1)
				throw new ArgumentException ("Stream duration must be positive.");
			await using var stream = new EaseeRealtimeClient (profile.Session, [charger], update => { Console.WriteLine (EaseeJson.Serialize (update)); return Task.CompletedTask; });
			await stream.StartAsync (cancellationToken);
			await Task.Delay (TimeSpan.FromSeconds (seconds), cancellationToken);
			return 0;
			}
		EaseeOperation operation = OperationRunner.Find (args.ElementAtOrDefault (1) ?? "");
		ValidateOperation (operation, parsed.ContainsKey ("allow-write"));
		var parameters = Pairs (parsed, "arg");
		var method = typeof (EaseeClient).GetMethod (operation.MethodName)!;
		if (!parameters.ContainsKey ("chargerId") && method.GetParameters ().Any (x => x.Name == "chargerId") && profile.Profile.ChargerId is not null)
			parameters["chargerId"] = profile.Profile.ChargerId;
		if (!parameters.ContainsKey ("siteId") && method.GetParameters ().Any (x => x.Name == "siteId") && profile.Profile.SiteId is not null)
			parameters["siteId"] = profile.Profile.SiteId;
		string? file = One (parsed, "body-file");
		var requestOptions = new EaseeRequestOptions ();
		foreach (KeyValuePair<string, string> h in Pairs (parsed, "header"))
			requestOptions.Headers.Add (h.Key, h.Value);
		object? result = await OperationRunner.InvokeAsync (profile.Client, operation, parameters, file is null ? null : File.ReadAllText (file), requestOptions, cancellationToken);
		if (result is EaseeStreamResponse binary)
			{
			using (binary)
				{
				string output = One (parsed, "output") ?? throw new ArgumentException ("A streamed response requires --output FILE.");
				using var destination = new FileStream (output, FileMode.CreateNew, FileAccess.Write);
				await binary.Content.CopyToAsync (destination, 81920, cancellationToken);
				Console.WriteLine ("Response saved.");
				}
			}
		else
			Console.WriteLine (JsonSerializer.Serialize (result, new JsonSerializerOptions { WriteIndented = true }));
		return 0;
		}
	internal static void ValidateOperation (EaseeOperation operation, bool allowWrite)
		{
		if (operation.Id is "Account_Authenticate" or "Account_RefreshToken")
			throw new InvalidOperationException ("Use login; token exchange results are never displayed.");
		if (operation.HttpMethod is not ("GET" or "HEAD") && !allowWrite)
			throw new InvalidOperationException ("This operation changes remote state. Supply --allow-write to execute it.");
		}
	internal static Dictionary<string, List<string>> ParseOptions (string[] args)
		{
		var result = new Dictionary<string, List<string>> (StringComparer.Ordinal);
		var permitted = new HashSet<string> { "profile", "site", "charger", "category", "arg", "body-file", "header", "output", "allow-write", "seconds" };
		for (int i = 0; i < args.Length; i++)
			{
			if (!args[i].StartsWith ("--", StringComparison.Ordinal) || !permitted.Contains (args[i].Substring (2)))
				throw new ArgumentException ("Unknown option.");
			string key = args[i].Substring (2);
			string value = key == "allow-write" ? "true" : ++i < args.Length ? args[i] : throw new ArgumentException ("An option value is missing.");
			if (!result.TryGetValue (key, out List<string>? list))
				result[key] = list = [];
			if (list.Count > 0 && key is not ("arg" or "header"))
				throw new ArgumentException ("Duplicate option: " + key);
			list.Add (value);
			}
		return result;
		}
	private static string? One (Dictionary<string, List<string>> options, string name) => options.TryGetValue (name, out List<string>? values) ? values.Single () : null;
	private static Dictionary<string, string> Pairs (Dictionary<string, List<string>> options, string name)
		{
		var result = new Dictionary<string, string> ();
		if (options.TryGetValue (name, out List<string>? values))
			foreach (string value in values)
				{
				int index = value.IndexOf ('=');
				if (index < 1)
					throw new ArgumentException ("Expected name=value.");
				result.Add (value.Substring (0, index), value.Substring (index + 1));
				}
		return result;
		}
	private static string ReadPassword ()
		{
		var value = new StringBuilder ();
		while (true)
			{
			ConsoleKeyInfo key = Console.ReadKey (true);
			if (key.Key == ConsoleKey.Enter)
				{
				Console.WriteLine ();
				return value.ToString ();
				}
			if (key.Key == ConsoleKey.Backspace)
				{
				if (value.Length > 0)
					value.Length--;
				}
			else if (!char.IsControl (key.KeyChar))
				value.Append (key.KeyChar);
			}
		}
	internal static IEnumerable<string> Tokenize (string line)
		{
		var token = new StringBuilder ();
		char quote = '\0';
		bool active = false;
		foreach (char c in line)
			{
			if (quote != '\0')
				{
				if (c == quote)
					quote = '\0';
				else
					token.Append (c);
				active = true;
				}
			else if (c is '\'' or '"')
				{
				quote = c;
				active = true;
				}
			else if (char.IsWhiteSpace (c))
				{
				if (active)
					{
					yield return token.ToString ();
					token.Clear ();
					active = false;
					}
				}
			else
				{
				token.Append (c);
				active = true;
				}
			}
		if (quote != '\0')
			throw new ArgumentException ("Unclosed quote.");
		if (active)
			yield return token.ToString ();
		}
	}