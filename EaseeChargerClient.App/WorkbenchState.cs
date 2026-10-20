using System.ComponentModel;
using System.Collections.ObjectModel;
using EaseeChargerClient.Models;
using System.Runtime.CompilerServices;

namespace EaseeChargerClient.App;

public sealed class WorkbenchState : INotifyPropertyChanged
	{
	private string _status = "Open a saved profile or sign in to get started.";
	private string _result = "Charger data and operation results will appear here.";
	private bool _busy;
	public string Status
		{
		get => _status; set
			{
			_status = value;
			Changed ();
			}
		}
	public string Result
		{
		get => _result; set
			{
			_result = value;
			Changed ();
			}
		}
	public bool Busy
		{
		get => _busy; set
			{
			_busy = value;
			Changed ();
			Changed (nameof (Idle));
			}
		}
	public bool Idle => !Busy;
	public ObservableCollection<SiteChoice> Sites { get; } = [];
	public void SetSites (IEnumerable<Easee_Cloud_API_BasicSiteDTO> sites)
		{
		Sites.Clear ();
		foreach (var site in sites.Where (x => x.Id.HasValue).GroupBy (x => x.Id).Select (x => x.First ()).OrderBy (x => x.Name))
			Sites.Add (new SiteChoice (site.Id!.Value.ToString (System.Globalization.CultureInfo.InvariantCulture), site.Name));
		}
	public ObservableCollection<ChargerChoice> Chargers { get; } = [];
	public void SetChargers (IEnumerable<Easee_Cloud_API_ChargerDTO2> chargers)
		{
		Chargers.Clear ();
		foreach (var charger in chargers.Where (x => !string.IsNullOrWhiteSpace (x.Id)).GroupBy (x => x.Id, StringComparer.OrdinalIgnoreCase).Select (x => x.First ()).OrderBy (x => x.Name))
			Chargers.Add (new ChargerChoice (charger.Id!, charger.Name));
		}
	public IReadOnlyList<EaseeOperation> Operations { get; } = EaseeOperations.All.Where (x => x.Id is not ("Account_Authenticate" or "Account_RefreshToken")).OrderBy (x => x.Category).ThenBy (x => x.Summary).ToArray ();
	public static bool RequiresWritePermission (EaseeOperation operation) => operation.HttpMethod is not ("GET" or "HEAD");
	public event PropertyChangedEventHandler? PropertyChanged;
	private void Changed ([CallerMemberName] string? name = null) => PropertyChanged?.Invoke (this, new (name));
	}
public sealed class ChargerChoice (string serialNumber, string? name)
	{
	public string SerialNumber { get; } = serialNumber;
	public string? Name { get; } = name;
	public string Label => string.IsNullOrWhiteSpace (Name) ? SerialNumber : Name + " — " + SerialNumber;
	}

public sealed class SiteChoice (string siteId, string? name)
	{
	public string SiteId { get; } = siteId;
	public string? Name { get; } = name;
	public string Label => string.IsNullOrWhiteSpace (Name) ? SiteId : Name + " — " + SiteId;
	}