using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace EaseeChargerClient.App.Tests;

[TestFixture]
public sealed class AppTests
	{
	[Test]
	public void AuthenticationEndpointsCannotExposeTokensInTheWorkbench ()
		{
		var state = new WorkbenchState ();
		Assert.That (state.Operations, Has.Count.EqualTo (166));
		Assert.That (state.Operations.Select (x => x.Id), Does.Not.Contain ("Account_Authenticate").And.Not.Contain ("Account_RefreshToken"));
		}
	[Test]
	public void BusyStateDisablesOperationsAndNotifiesBindings ()
		{
		var state = new WorkbenchState ();
		var changes = new List<string?> ();
		state.PropertyChanged += (_, args) => changes.Add (args.PropertyName);
		state.Busy = true;
		Assert.That (state.Idle, Is.False);
		Assert.That (changes, Is.EquivalentTo (new[] { "Busy", "Idle" }));
		}
	[Test, Apartment (ApartmentState.STA)]
	public void MainWindowLoadsAndRendersWithoutOpeningCredentials ()
		{
		var app = Application.Current ?? new App ();
		if (app.Resources.Count == 0)
			((App)app).InitializeComponent ();
		var window = new MainWindow ();
		var content = (FrameworkElement)window.Content;
		content.Measure (new Size (1240, 860));
		content.Arrange (new Rect (0, 0, 1240, 860));
		content.UpdateLayout ();
		Assert.That (window.FindName ("Password"), Is.InstanceOf<PasswordBox> ());
		Assert.That (window.Title, Is.EqualTo ("Easee Charger Client"));
		var bitmap = new RenderTargetBitmap (1240, 860, 96, 96, PixelFormats.Pbgra32);
		bitmap.Render (content);
		byte[] pixels = new byte[1240 * 860 * 4];
		bitmap.CopyPixels (pixels, 1240 * 4, 0);
		Assert.That (pixels.Count (x => x != 0), Is.GreaterThan (500000), "The window must render visible content.");
		var encoder = new PngBitmapEncoder ();
		encoder.Frames.Add (BitmapFrame.Create (bitmap));
		using (FileStream output = File.Create (Path.Combine (TestContext.CurrentContext.WorkDirectory, "desktop-preview.png")))
			encoder.Save (output);
		window.Close ();
		}
	}