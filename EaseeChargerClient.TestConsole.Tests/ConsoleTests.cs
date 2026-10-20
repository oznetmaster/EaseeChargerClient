namespace EaseeChargerClient.TestConsole.Tests;

[TestFixture]
public sealed class ConsoleTests
	{
	[Test]
	public void ModifyingCommandsRequireExplicitWriteSelection ()
		{
		Assert.Throws<InvalidOperationException> (() => Program.ValidateOperation (Hosting.OperationRunner.Find ("charger_stop_charging"), false));
		Assert.DoesNotThrow (() => Program.ValidateOperation (Hosting.OperationRunner.Find ("charger_stop_charging"), true));
		}
	[TestCase ("Account_Authenticate")]
	[TestCase ("Account_RefreshToken")]
	public void AuthenticationCannotPrintTokensThroughTheGenericInvoker (string id) => Assert.Throws<InvalidOperationException> (() => Program.ValidateOperation (Hosting.OperationRunner.Find (id), true));
	[TestCase ("--password")]
	[TestCase ("--token")]
	[TestCase ("--typo")]
	public void SecretAndUnknownOptionsAreRejected (string option) => Assert.Throws<ArgumentException> (() => Program.ParseOptions ([option, "private"]));
	[Test]
	public void InteractiveTokenizerPreservesPathsWithSpacesAndRepeatedArguments ()
		{
		string[] parsed = Program.Tokenize ("call Operation --body-file 'C:\\My Files\\request.json' --arg name=value").ToArray ();
		Assert.That (parsed[3], Is.EqualTo ("C:\\My Files\\request.json"));
		Assert.That (parsed[5], Is.EqualTo ("name=value"));
		}
	[Test]
	public void UnclosedInteractiveQuotesAreRejected () => Assert.Throws<ArgumentException> (() => Program.Tokenize ("call 'unfinished").ToArray ());
	[Test]
	public void DuplicateSingletonOptionsAreRejected () => Assert.Throws<ArgumentException> (() => Program.ParseOptions (["--profile", "one", "--profile", "two"]));
	[Test]
	public async Task HelpRunsWithoutOpeningAnyCredentialProfile () => Assert.That (await Program.ExecuteAsync (["help"], CancellationToken.None), Is.Zero);
	}