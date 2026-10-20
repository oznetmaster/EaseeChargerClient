using System.Text.Json;
using System.Text.Json.Serialization;

namespace EaseeChargerClient.Models;

/// <summary>Common REST observation fields. Concrete value types follow the published dataType discriminator.</summary>
[JsonConverter (typeof (EaseeObservationConverter))]
public abstract class Current_Device_State_API_Observation
	{
	/// <summary>The observation identifier, or null when absent.</summary>
	/// <value>The observation identifier, or null when absent.</value>
	[JsonPropertyName ("id")]
	public int? Id
		{
		get; set;
		}
	/// <summary>The observation timestamp, or null when absent.</summary>
	/// <value>The observation timestamp, or null when absent.</value>
	[JsonPropertyName ("timestamp")]
	public DateTimeOffset? Timestamp
		{
		get; set;
		}
	/// <summary>The nullable wire discriminator calculated from the concrete observation model.</summary>
	/// <value>The nullable wire discriminator calculated from the concrete observation model.</value>
	[JsonPropertyName ("dataType")]
	public abstract Current_Device_State_API_IotDataType? DataType
		{
		get;
		}
	}

/// <summary>Contains a nullable Boolean REST observation value.</summary>
public sealed class EaseeBooleanObservation : Current_Device_State_API_Observation
	{
	/// <summary>The discriminator calculated from this concrete observation type.</summary>
	/// <value>The discriminator calculated from this concrete observation type.</value>
	[JsonPropertyName ("dataType")] public override Current_Device_State_API_IotDataType? DataType => Current_Device_State_API_IotDataType.Boolean;
	/// <summary>The nullable typed observation value; null represents an absent or explicitly null value.</summary>
	/// <value>The nullable typed observation value; null represents an absent or explicitly null value.</value>
	[JsonPropertyName ("value")]
	public bool? Value
		{
		get; set;
		}
	}
/// <summary>Contains a nullable floating-point REST observation value.</summary>
public sealed class EaseeDoubleObservation : Current_Device_State_API_Observation
	{
	/// <summary>The discriminator calculated from this concrete observation type.</summary>
	/// <value>The discriminator calculated from this concrete observation type.</value>
	[JsonPropertyName ("dataType")] public override Current_Device_State_API_IotDataType? DataType => Current_Device_State_API_IotDataType.Double;
	/// <summary>The nullable typed observation value; null represents an absent or explicitly null value.</summary>
	/// <value>The nullable typed observation value; null represents an absent or explicitly null value.</value>
	[JsonPropertyName ("value")]
	public double? Value
		{
		get; set;
		}
	}
/// <summary>Contains a nullable integer REST observation value.</summary>
public sealed class EaseeIntegerObservation : Current_Device_State_API_Observation
	{
	/// <summary>The discriminator calculated from this concrete observation type.</summary>
	/// <value>The discriminator calculated from this concrete observation type.</value>
	[JsonPropertyName ("dataType")] public override Current_Device_State_API_IotDataType? DataType => Current_Device_State_API_IotDataType.Integer;
	/// <summary>The nullable typed observation value; null represents an absent or explicitly null value.</summary>
	/// <value>The nullable typed observation value; null represents an absent or explicitly null value.</value>
	[JsonPropertyName ("value")]
	public long? Value
		{
		get; set;
		}
	}
/// <summary>Contains a nullable string REST observation value.</summary>
public sealed class EaseeStringObservation : Current_Device_State_API_Observation
	{
	/// <summary>The discriminator calculated from this concrete observation type.</summary>
	/// <value>The discriminator calculated from this concrete observation type.</value>
	[JsonPropertyName ("dataType")] public override Current_Device_State_API_IotDataType? DataType => Current_Device_State_API_IotDataType.String;
	/// <summary>The nullable typed observation value; null represents an absent or explicitly null value.</summary>
	/// <value>The nullable typed observation value; null represents an absent or explicitly null value.</value>
	[JsonPropertyName ("value")]
	public string? Value
		{
		get; set;
		}
	}

/// <summary>Nullable upstream Position shape. Compatibility with an actual Easee Position payload is not yet verified.</summary>
public sealed class EaseePosition
	{
	/// <summary>The nullable upstream latitude in degrees. The Easee Position wire contract remains unverified.</summary>
	/// <value>The nullable upstream latitude in degrees. The Easee Position wire contract remains unverified.</value>
	[JsonPropertyName ("latitude")]
	public double? Latitude
		{
		get; set;
		}
	/// <summary>The nullable upstream longitude in degrees. The Easee Position wire contract remains unverified.</summary>
	/// <value>The nullable upstream longitude in degrees. The Easee Position wire contract remains unverified.</value>
	[JsonPropertyName ("longitude")]
	public double? Longitude
		{
		get; set;
		}
	/// <summary>The nullable upstream altitude. Easee has not published the unit or verified this field contract.</summary>
	/// <value>The nullable upstream altitude. Easee has not published the unit or verified this field contract.</value>
	[JsonPropertyName ("altitude")]
	public double? Altitude
		{
		get; set;
		}
	/// <summary>The nullable upstream dilution of precision. The Easee Position wire contract remains unverified.</summary>
	/// <value>The nullable upstream dilution of precision. The Easee Position wire contract remains unverified.</value>
	[JsonPropertyName ("dop")]
	public double? Dop
		{
		get; set;
		}
	}
/// <summary>Position is typed from the upstream IoT model; the Easee wire shape remains unverified.</summary>
public sealed class EaseePositionObservation : Current_Device_State_API_Observation
	{
	/// <summary>The discriminator calculated from this concrete observation type.</summary>
	/// <value>The discriminator calculated from this concrete observation type.</value>
	[JsonPropertyName ("dataType")] public override Current_Device_State_API_IotDataType? DataType => Current_Device_State_API_IotDataType.Position;
	/// <summary>The nullable typed observation value; null represents an absent or explicitly null value.</summary>
	/// <value>The nullable typed observation value; null represents an absent or explicitly null value.</value>
	[JsonPropertyName ("value")]
	public EaseePosition? Value
		{
		get; set;
		}
	}

internal sealed class EaseeObservationHeader
	{
	[JsonPropertyName ("dataType")]
	public Current_Device_State_API_IotDataType? DataType
		{
		get; set;
		}
	}

/// <summary>Attribute-selected polymorphism using a typed discriminator model, without a JSON DOM or arbitrary value container.</summary>
internal sealed class EaseeObservationConverter : JsonConverter<Current_Device_State_API_Observation>
	{
	public override Current_Device_State_API_Observation? Read (ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
		var headerReader = reader;
		var header = JsonSerializer.Deserialize<EaseeObservationHeader> (ref headerReader, options) ?? throw new JsonException ("Missing observation header.");
		return header.DataType switch
			{
				Current_Device_State_API_IotDataType.Boolean => JsonSerializer.Deserialize<EaseeBooleanObservation> (ref reader, options),
				Current_Device_State_API_IotDataType.Double => JsonSerializer.Deserialize<EaseeDoubleObservation> (ref reader, options),
				Current_Device_State_API_IotDataType.Integer => JsonSerializer.Deserialize<EaseeIntegerObservation> (ref reader, options),
				Current_Device_State_API_IotDataType.Position => JsonSerializer.Deserialize<EaseePositionObservation> (ref reader, options),
				Current_Device_State_API_IotDataType.String => JsonSerializer.Deserialize<EaseeStringObservation> (ref reader, options),
				_ => throw new JsonException ("The observation dataType has no established typed value contract.")
				};
		}
	public override void Write (Utf8JsonWriter writer, Current_Device_State_API_Observation value, JsonSerializerOptions options)
		{
		switch (value)
			{
			case EaseeBooleanObservation item:
				JsonSerializer.Serialize (writer, item, options);
				break;
			case EaseeDoubleObservation item:
				JsonSerializer.Serialize (writer, item, options);
				break;
			case EaseeIntegerObservation item:
				JsonSerializer.Serialize (writer, item, options);
				break;
			case EaseePositionObservation item:
				JsonSerializer.Serialize (writer, item, options);
				break;
			case EaseeStringObservation item:
				JsonSerializer.Serialize (writer, item, options);
				break;
			default:
				throw new JsonException ("The observation has no established typed value contract.");
			}
		}
	}