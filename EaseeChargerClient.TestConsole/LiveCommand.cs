using System.Diagnostics;
using System.Text;

using EaseeChargerClient.Hosting;

namespace EaseeChargerClient.TestConsole;

internal static class LiveCommand
	{
	public static async Task<int> RunAsync (string[] args, CancellationToken cancellationToken)
		{
		int separator = Array.IndexOf (args, "--");
		if (separator < 1 || separator + 1 >= args.Length)
			throw new ArgumentException ("Use with-live-profile --profile NAME -- COMMAND ARGUMENTS.");
		var options = Program.ParseOptions (args.Skip (1).Take (separator - 1).ToArray ());
		if (options.Count != 1 || !options.TryGetValue ("profile", out List<string>? profile))
			throw new ArgumentException ("Supply exactly one --profile name before --.");
		using var prepared = await PreparedLiveSession.CreateAsync (profile.Single (), cancellationToken);
		var start = new ProcessStartInfo (args[separator + 1])
			{
			UseShellExecute = false,
			Arguments = string.Join (" ", args.Skip (separator + 2).Select (Quote))
			};
		start.EnvironmentVariables["EASEE_ENABLE_LIVE_TESTS"] = "1";
		start.EnvironmentVariables["EASEE_LIVE_TEST_DATA_DIRECTORY"] = prepared.InputDirectory;
		using Process child = Process.Start (start) ?? throw new IOException ("The live-test command could not start.");
		// Retain profile ownership until the blocking child exits, including after Ctrl+C.
		await Task.Run (() => child.WaitForExit ());
		return child.ExitCode;
		}
	internal static string Quote (string argument)
		{
		var text = new StringBuilder ("\"");
		int slashes = 0;
		foreach (char character in argument)
			{
			if (character == '\\')
				{
				slashes++;
				continue;
				}
			if (character == '"')
				text.Append ('\\', slashes * 2 + 1).Append ('"');
			else
				text.Append ('\\', slashes).Append (character);
			slashes = 0;
			}
		return text.Append ('\\', slashes * 2).Append ('"').ToString ();
		}
	}