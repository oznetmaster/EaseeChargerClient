using System.IO;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

using EaseeChargerClient.Hosting;

using Microsoft.Win32;

namespace EaseeChargerClient.App;

public partial class MainWindow : Window
	{
	private readonly WorkbenchState _state = new ();
	private readonly ObservableCollection<EaseeProductUpdate> _updates = [];
	private EaseeProfileSession? _profile;
	private EaseeRealtimeClient? _stream;
	private CancellationTokenSource? _operation;
	private string? _openProfileName;
	private TaskCompletionSource<bool>? _operationDone;
	private bool _closing;
	private bool _allowClose;
	private bool _updatingSiteChoices;
	public MainWindow ()
		{
		InitializeComponent ();
		DataContext = _state;
		Updates.ItemsSource = _updates;
		}
	private EaseeProfileSession Profile => _profile ?? throw new InvalidOperationException ("Open a saved profile or sign in first.");
	private void OpenProfile ()
		{
		if (_profile is not null)
			{
			if (_openProfileName != ProfileName.Text)
				throw new InvalidOperationException ("Restart the app before switching profiles.");
			return;
			}
		_profile = new (ProfileName.Text);
		_openProfileName = ProfileName.Text;
		SiteId.Text = _profile.Profile.SiteId ?? "";
		ChargerId.Text = _profile.Profile.ChargerId ?? "";
		_state.Status = _profile.Profile.Token is null ? "Profile opened. Sign in to connect." : "Saved profile opened. List your sites to select a charger.";
		SignInPanel.IsExpanded = _profile.Profile.Token is null;
		}
	private async Task PerformAsync (Func<CancellationToken, Task> action)
		{
		if (_state.Busy)
			return;
		_state.Busy = true;
		_operation = new ();
		_operationDone = new (TaskCreationOptions.RunContinuationsAsynchronously);
		try
			{
			await action (_operation.Token);
			}
		catch (OperationCanceledException) { _state.Status = "Operation cancelled."; }
		catch (Exception error) { _state.Status = error is EaseeApiException api ? $"Easee returned HTTP {(int)api.StatusCode} for {api.OperationId}." : error is ArgumentException or InvalidOperationException ? error.Message : error.GetType ().Name + ": operation failed. Sign in again if authorization has expired."; }
		finally { _operation.Dispose (); _operation = null; _state.Busy = false; _operationDone.TrySetResult (true); _operationDone = null; }
		}
	private async void OpenProfile_Click (object sender, RoutedEventArgs e) => await PerformAsync (_ => { OpenProfile (); return Task.CompletedTask; });
	private async void Login_Click (object sender, RoutedEventArgs e) => await PerformAsync (async token =>
	{
		OpenProfile ();
		string password = Password.Password;
		Password.Clear ();
		_state.Status = "Signing in…";
		await Profile.Session.SignInAsync (Username.Text, password, token);
		Username.Clear ();
		SignInPanel.IsExpanded = false;
		_state.Status = "Signed in. Tokens saved securely. List your sites to select a charger.";
	});
	private async void Save_Click (object sender, RoutedEventArgs e) => await PerformAsync (_ => { Profile.SaveSelection (SiteId.Text.Trim (), ChargerId.Text.Trim ()); _state.Status = "Private selection saved."; return Task.CompletedTask; });
	private async void Sites_Click (object sender, RoutedEventArgs e) => await PerformAsync (async token =>
	 {
		 _state.Status = "Loading sites…";
		 var response = await Profile.Client.SiteGetAllAsync (cancellationToken: token);
		 _updatingSiteChoices = true;
		 try
			 {
			 _state.SetSites (response.Value ?? []);
			 Sites.SelectedValue = SiteId.Text.Trim ();
			 if (_state.Sites.Count == 1)
				 Sites.SelectedIndex = 0;
			 }
		 finally { _updatingSiteChoices = false; }
		 _state.Result = JsonSerializer.Serialize (_state.Sites, new JsonSerializerOptions { WriteIndented = true });
		 if (Sites.SelectedItem is SiteChoice site)
			 await SelectSiteAsync (site, token);
		 else
			 _state.Status = _state.Sites.Count == 0 ? "No sites with an identifier were returned." : "Select a site to load its chargers.";
	 });
	private async void Site_Changed (object sender, SelectionChangedEventArgs e)
		{
		if (!_updatingSiteChoices && Sites.SelectedItem is SiteChoice site)
			await PerformAsync (token => SelectSiteAsync (site, token));
		}
	private async Task SelectSiteAsync (SiteChoice site, CancellationToken token)
		{
		bool changed = SiteId.Text.Trim () != site.SiteId;
		SiteId.Text = site.SiteId;
		if (changed)
			{
			_state.SetChargers ([]);
			ChargerId.Clear ();
			if (_stream is not null)
				{
				await _stream.DisposeAsync ();
				_stream = null;
				}
			}
		await LoadChargersAsync (token);
		}
	private async void Chargers_Click (object sender, RoutedEventArgs e) => await PerformAsync (LoadChargersAsync);
	private async Task LoadChargersAsync (CancellationToken token)
		{
		_state.Status = "Loading site chargers…";
		var response = await Profile.Client.SiteGetChargersAsync (SiteId.Text.Trim (), cancellationToken: token);
		string previous = ChargerId.Text.Trim ();
		_state.SetChargers (response.Value ?? []);
		if (!_state.Chargers.Any (x => x.SerialNumber == previous))
			ChargerId.Clear ();
		_state.Result = JsonSerializer.Serialize (_state.Chargers, new JsonSerializerOptions { WriteIndented = true });
		SiteChargers.SelectedValue = ChargerId.Text.Trim ();
		if (_state.Chargers.Count == 1)
			SiteChargers.SelectedIndex = 0;
		_state.Status = _state.Chargers.Count == 0 ? "No chargers with an identifier were returned for this site." : "Chargers loaded. Select a name — serial number, then Save selection.";
		}
	private void Charger_Changed (object sender, SelectionChangedEventArgs e)
		{
		if (SiteChargers.SelectedItem is ChargerChoice charger)
			ChargerId.Text = charger.SerialNumber;
		}
	private async void Details_Click (object sender, RoutedEventArgs e) => await PerformAsync (token => ReadAsync ("Charger_GetChargerDetails", token));
	private async void Schedules_Click (object sender, RoutedEventArgs e) => await PerformAsync (token => ReadAsync ("GetChargersSchedules", token));
	private Task ReadAsync (string id, CancellationToken token) => InvokeAsync (OperationRunner.Find (id), new Dictionary<string, string> (), null, null, token);
	private async Task InvokeAsync (EaseeOperation operation, Dictionary<string, string> args, string? body, EaseeRequestOptions? options, CancellationToken token)
		{
		var method = typeof (EaseeClient).GetMethod (operation.MethodName)!;
		foreach (string name in new[] { "siteId", "chargerId" })
			if (!args.ContainsKey (name) && method.GetParameters ().Any (x => x.Name == name))
				args[name] = name == "siteId" ? SiteId.Text.Trim () : ChargerId.Text.Trim ();
		_state.Status = "Running " + operation.Summary + "…";
		object? result = await OperationRunner.InvokeAsync (Profile.Client, operation, args, body, options, token);
		if (result is EaseeStreamResponse download)
			{
			using (download)
				{
				var dialog = new SaveFileDialog { Title = "Save Easee response", FileName = "easee-response.bin" };
				if (dialog.ShowDialog (this) == true)
					{
					using FileStream output = File.Create (dialog.FileName);
					await download.Content.CopyToAsync (output, token);
					}
				}
			}
		else
			_state.Result = JsonSerializer.Serialize (result, new JsonSerializerOptions { WriteIndented = true });
		_state.Status = operation.Summary + " complete. Open Results to inspect the response.";
		}
	private void Operation_Changed (object sender, SelectionChangedEventArgs e)
		{
		if (Operations.SelectedItem is not EaseeOperation operation)
			return;
		OperationDescription.Text = OperationRunner.Describe (operation);
		AllowWrite.IsChecked = false;
		Arguments.Clear ();
		Body.Clear ();
		}
	private async void Run_Click (object sender, RoutedEventArgs e) => await PerformAsync (async token =>
	{
		if (Operations.SelectedItem is not EaseeOperation operation)
			throw new InvalidOperationException ("Select an operation.");
		if (WorkbenchState.RequiresWritePermission (operation) && AllowWrite.IsChecked != true)
			throw new InvalidOperationException ("Select the permission checkbox to run this modifying operation.");
		var args = new Dictionary<string, string> ();
		foreach (string line in Arguments.Text.Split ('\n').Where (x => !string.IsNullOrWhiteSpace (x)))
			{
			int index = line.IndexOf ('=');
			if (index < 1)
				throw new ArgumentException ("Use name=value for each parameter.");
			args.Add (line.Substring (0, index).Trim (), line.Substring (index + 1).Trim ());
			}
		var options = new EaseeRequestOptions ();
		if (!string.IsNullOrWhiteSpace (Traceparent.Text))
			options.Headers.Add ("traceparent", Traceparent.Text.Trim ());
		await InvokeAsync (operation, args, string.IsNullOrWhiteSpace (Body.Text) ? null : Body.Text, options, token);
	});
	private async void Stream_Click (object sender, RoutedEventArgs e) => await PerformAsync (async token =>
	{
		if (_stream is not null)
			throw new InvalidOperationException ("Live updates are already running.");
		_stream = new (Profile.Session, [ChargerId.Text.Trim ()], async update => await Dispatcher.InvokeAsync (() => { _updates.Insert (0, update); while (_updates.Count > 500) _updates.RemoveAt (_updates.Count - 1); }));
		try
			{
			await _stream.StartAsync (token);
			}
		catch { await _stream.DisposeAsync (); _stream = null; throw; }
		_state.Status = "Live updates connected. Up to 500 recent observations are displayed.";
	});
	private async void StopStream_Click (object sender, RoutedEventArgs e) => await PerformAsync (async _ => { if (_stream is not null) { await _stream.DisposeAsync (); _stream = null; } _state.Status = "Live updates stopped."; });
	private void Cancel_Click (object sender, RoutedEventArgs e) => _operation?.Cancel ();
	protected override async void OnClosing (CancelEventArgs e)
		{
		if (_allowClose)
			{
			base.OnClosing (e);
			return;
			}
		e.Cancel = true;
		if (_closing)
			return;
		_closing = true;
		_operation?.Cancel ();
		if (_operationDone is not null)
			await _operationDone.Task;
		try
			{
			if (_stream is not null)
				await _stream.DisposeAsync ();
			}
		catch (Exception) { /* Closing still releases the encrypted profile lease. */ }
		finally { _profile?.Dispose (); _allowClose = true; Close (); }
		}
	}