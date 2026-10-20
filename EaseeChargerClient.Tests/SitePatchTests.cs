using System.Text.Json;

using EaseeChargerClient.Models;

namespace EaseeChargerClient.Tests;

[TestFixture]
public sealed class SitePatchTests
	{
	[Test]
	public void AStringFollowedByANumberUsesItsTwoTypedSiteFields ()
		{
		const string wire = """
            [{"op":"replace","path":"/name","value":"test site"},{"value":2,"op":"replace","path":"/siteType"}]
            """;
		var operations = JsonSerializer.Deserialize<List<Easee_Cloud_API_Operation>> (wire)!;
		Assert.That (operations[0].Value!.ValueType, Is.EqualTo (typeof (string)));
		Assert.That (((EaseeSitePatchValue<string>)operations[0].Value!).Value, Is.EqualTo ("test site"));
		Assert.That (operations[1].Value!.ValueType, Is.EqualTo (typeof (Easee_Cloud_API_SiteType2?)));
		var roundTrip = JsonSerializer.Deserialize<List<Easee_Cloud_API_Operation>> (JsonSerializer.Serialize (operations))!;
		Assert.That (roundTrip[1].Value!.ValueType, Is.EqualTo (operations[1].Value!.ValueType));
		}
	[Test]
	public void AStructuredRootValueUsesTheSiteModelAndMissingFieldsStayNull ()
		{
		var operation = JsonSerializer.Deserialize<Easee_Cloud_API_Operation> ("{\"op\":\"replace\",\"path\":\"\",\"value\":{\"name\":\"example\"}}")!;
		var value = (EaseeSitePatchValue<Easee_Cloud_API_SiteDTO2>)operation.Value!;
		Assert.That (value.Value!.Name, Is.EqualTo ("example"));
		Assert.That (value.Value.SiteType, Is.Null);
		}
	[Test]
	public void ExplicitNullSurvivesButRemoveHasNoValueMember ()
		{
		var set = new Easee_Cloud_API_Operation { Op = "replace", Path = "/name", Value = EaseeSitePatchValue.From<string> (null) };
		Assert.That (JsonSerializer.Serialize (set), Does.Contain ("\"value\":null"));
		Assert.That (((EaseeSitePatchValue<string>)JsonSerializer.Deserialize<Easee_Cloud_API_Operation> (JsonSerializer.Serialize (set))!.Value!).Value, Is.Null);
		Assert.That (JsonSerializer.Serialize (new Easee_Cloud_API_Operation { Op = "remove", Path = "/name" }), Does.Not.Contain ("value"));
		}
	[TestCase ("/useDynamicMaster", "false", typeof (bool?))]
	[TestCase ("/circuits/0/ratedCurrent", "0", typeof (double?))]
	public void BooleanAndNestedNumericValuesUseThePublishedTargetType (string path, string value, Type expected)
		{
		var operation = JsonSerializer.Deserialize<Easee_Cloud_API_Operation> ($"{{\"op\":\"replace\",\"path\":\"{path}\",\"value\":{value}}}")!;
		Assert.That (operation.Value!.ValueType, Is.EqualTo (expected));
		Assert.That (JsonSerializer.Serialize (operation), Does.Contain ($"\"value\":{value}"));
		}
	[Test]
	public void ArrayValuesUseThePublishedElementModel ()
		{
		var operation = JsonSerializer.Deserialize<Easee_Cloud_API_Operation> ("{\"op\":\"replace\",\"path\":\"/circuits\",\"value\":[]}")!;
		Assert.That (operation.Value!.ValueType, Is.EqualTo (typeof (List<Easee_Cloud_API_CircuitDTO2>)));
		Assert.That (JsonSerializer.Serialize (operation), Does.Contain ("\"value\":[]"));
		}
	[Test]
	public void APathOutsideTheKnownModelIsRejectedWithoutADomFallback () => Assert.Throws<JsonException> (() => JsonSerializer.Deserialize<Easee_Cloud_API_Operation> ("{\"op\":\"replace\",\"path\":\"/unknown-field\",\"value\":4}"));
	}