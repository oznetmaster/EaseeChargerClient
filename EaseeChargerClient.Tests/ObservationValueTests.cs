using System.Text.Json;

using EaseeChargerClient.Models;

namespace EaseeChargerClient.Tests;

[TestFixture]
public sealed class ObservationValueTests
	{
	[Test]
	public void DiscriminatorMayFollowValueAndNumericValuesStayTypedWithDefaultSerialization ()
		{
		const string json = """
            {"observations":[{"value":4,"id":109,"timestamp":"2026-10-20T19:00:00Z","dataType":4},{"id":120,"timestamp":"2026-10-20T19:00:00Z","dataType":3,"value":0.125}]}
            """;
		var result = JsonSerializer.Deserialize<Current_Device_State_API_MultiObservationResponse> (json)!;
		Assert.That (result.Observations![0], Is.TypeOf<EaseeIntegerObservation> ());
		Assert.That (((EaseeIntegerObservation)result.Observations[0]).Value, Is.EqualTo (4));
		Assert.That (((EaseeDoubleObservation)result.Observations[1]).Value, Is.EqualTo (0.125));
		var copy = JsonSerializer.Deserialize<Current_Device_State_API_MultiObservationResponse> (JsonSerializer.Serialize (result))!;
		Assert.That (((EaseeIntegerObservation)copy.Observations![0]).Value, Is.EqualTo (4));
		}
	[TestCase (2, "true", typeof (EaseeBooleanObservation))]
	[TestCase (3, "0.25", typeof (EaseeDoubleObservation))]
	[TestCase (4, "9223372036854775807", typeof (EaseeIntegerObservation))]
	[TestCase (6, "\"a value\"", typeof (EaseeStringObservation))]
	[TestCase (2, "null", typeof (EaseeBooleanObservation))]
	[TestCase (3, "null", typeof (EaseeDoubleObservation))]
	[TestCase (4, "null", typeof (EaseeIntegerObservation))]
	[TestCase (6, "null", typeof (EaseeStringObservation))]
	[TestCase (5, "null", typeof (EaseePositionObservation))]
	[TestCase (5, "{}", typeof (EaseePositionObservation))]
	public void ConcreteValueAndNullUseTheDeclaredType (int discriminator, string value, Type expected)
		{
		var item = JsonSerializer.Deserialize<Current_Device_State_API_Observation> ($"{{\"value\":{value},\"dataType\":{discriminator}}}");
		Assert.That (item, Is.TypeOf (expected));
		Assert.That ((int)item!.DataType!, Is.EqualTo (discriminator));
		Assert.That (item.Id, Is.Null);
		Assert.That (item.Timestamp, Is.Null);
		}
	[Test]
	public void AnUnknownDiscriminatorIsRejectedInsteadOfLosingItsValue () => Assert.Throws<JsonException> (() => JsonSerializer.Deserialize<Current_Device_State_API_Observation> ("{\"dataType\":999,\"value\":4}"));
	}