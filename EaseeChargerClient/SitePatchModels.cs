using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EaseeChargerClient.Models;

/// <summary>A JSON Patch operation whose value retains its concrete C# type.</summary>
[JsonConverter (typeof (EaseeSitePatchConverter))]
public sealed class Easee_Cloud_API_Operation
	{
	/// <summary>The target JSON Pointer within the published site model. An empty string addresses the complete site.</summary>
	/// <value>The target JSON Pointer within the published site model. An empty string addresses the complete site.</value>
	[JsonPropertyName ("path")]
	public string? Path
		{
		get; set;
		}
	/// <summary>The JSON Patch operation name: add, remove, replace, move, copy or test.</summary>
	/// <value>The JSON Patch operation name: add, remove, replace, move, copy or test.</value>
	[JsonPropertyName ("op")]
	public string? Op
		{
		get; set;
		}
	/// <summary>The source JSON Pointer for move or copy, or null when unused.</summary>
	/// <value>The source JSON Pointer for move or copy, or null when unused.</value>
	[JsonPropertyName ("from")]
	public string? From
		{
		get; set;
		}
	/// <summary>The typed operation value. Null omits the value member; wrap a typed null to send an explicit JSON null.</summary>
	/// <value>The typed operation value. Null omits the value member; wrap a typed null to send an explicit JSON null.</value>
	[JsonPropertyName ("value")]
	public EaseeSitePatchValue? Value
		{
		get; set;
		}
	}

/// <summary>A typed scalar, structured model, collection or explicit null; no JSON DOM is retained.</summary>
public abstract class EaseeSitePatchValue
	{
	/// <summary>The retained C# value type, including generic or nullable type information.</summary>
	/// <value>The retained C# value type, including generic or nullable type information.</value>
	public abstract Type ValueType
		{
		get;
		}
	/// <summary>Wraps a typed patch value without retaining a JSON DOM.</summary>
	/// <returns>A patch value retaining the supplied C# type, including an explicit null.</returns>
	/// <typeparam name="T">The concrete response or value model type.</typeparam>
	/// <param name="value">The typed value to retain or serialize; may be null when its type permits null.</param>
	public static EaseeSitePatchValue<T> From<T> (T? value) => new (value);
	internal abstract object Wire (Easee_Cloud_API_Operation operation);
	}

/// <summary>Retains a concrete C# value for a site JSON Patch operation.</summary>
/// <typeparam name="T">The concrete response or value model type.</typeparam>
/// <param name="value">The typed value to retain or serialize; may be null when its type permits null.</param>
public sealed class EaseeSitePatchValue<T> (T? value) : EaseeSitePatchValue
	{
	/// <summary>The retained scalar, model, collection or explicit null.</summary>
	/// <value>The retained scalar, model, collection or explicit null.</value>
	public T? Value { get; } = value;
	/// <summary>The retained C# value type, including generic or nullable type information.</summary>
	/// <value>The retained C# value type, including generic or nullable type information.</value>
	public override Type ValueType => typeof (T);
	internal override object Wire (Easee_Cloud_API_Operation operation) => new EaseeSitePatchWire<T>
		{
		Op = operation.Op,
		Path = operation.Path,
		From = operation.From,
		Value = Value
		};
	}

internal class EaseeSitePatchHeader
	{
	[JsonPropertyName ("path"), JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? Path
		{
		get; set;
		}
	[JsonPropertyName ("op"), JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? Op
		{
		get; set;
		}
	[JsonPropertyName ("from"), JsonIgnore (Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? From
		{
		get; set;
		}
	}

internal interface IEaseeSitePatchWire
	{
	Easee_Cloud_API_Operation Model ();
	}

internal sealed class EaseeSitePatchWire<T> : EaseeSitePatchHeader, IEaseeSitePatchWire
	{
	private T? _value;
	[JsonIgnore]
	public bool HasValue
		{
		get; private set;
		}
	[JsonPropertyName ("value")]
	public T? Value
		{
		get => _value; set
			{
			_value = value;
			HasValue = true;
			}
		}
	public Easee_Cloud_API_Operation Model () => new ()
		{
		Op = Op,
		Path = Path,
		From = From,
		Value = HasValue ? EaseeSitePatchValue.From<T> (Value) : null
		};
	}

/// <summary>Uses the typed path/op header and the site's attribute model to select a typed value model.</summary>
internal sealed class EaseeSitePatchConverter : JsonConverter<Easee_Cloud_API_Operation>
	{
	public override Easee_Cloud_API_Operation? Read (ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
		var copy = reader;
		var header = JsonSerializer.Deserialize<EaseeSitePatchHeader> (ref copy, options) ?? throw new JsonException ("Missing patch operation header.");
		if (header.Op is "remove" or "move" or "copy")
			{
			var read = JsonSerializer.Deserialize<EaseeSitePatchHeader> (ref reader, options)!;
			return new ()
				{
				Op = read.Op,
				Path = read.Path,
				From = read.From
				};
			}
		if (header.Op is not ("add" or "replace" or "test"))
			throw new JsonException ("Unknown JSON Patch operation.");
		Type target = TargetType (header.Path);
		// Null is represented by a nullable target type rather than by a JSON value container.
		if (target.IsValueType && Nullable.GetUnderlyingType (target) is null)
			target = typeof (Nullable<>).MakeGenericType (target);
		Type wire = typeof (EaseeSitePatchWire<>).MakeGenericType (target);
		var model = ((IEaseeSitePatchWire)JsonSerializer.Deserialize (ref reader, wire, options)!).Model ();
		if (model.Value is null)
			throw new JsonException ("This patch operation requires a value member; it may explicitly be null.");
		return model;
		}
	public override void Write (Utf8JsonWriter writer, Easee_Cloud_API_Operation value, JsonSerializerOptions options)
		{
		object wire = value.Value?.Wire (value) ?? new EaseeSitePatchHeader { Op = value.Op, Path = value.Path, From = value.From };
		JsonSerializer.Serialize (writer, wire, wire.GetType (), options);
		}
	private static Type TargetType (string? path)
		{
		if (path is null || path.Length > 0 && !path.StartsWith ("/", StringComparison.Ordinal))
			throw new JsonException ("A JSON Pointer site path is required.");
		Type target = typeof (Easee_Cloud_API_SiteDTO2);
		if (path.Length == 0)
			return target;
		foreach (string encoded in path.Substring (1).Split ('/'))
			{
			string part = encoded.Replace ("~1", "/").Replace ("~0", "~");
			target = Nullable.GetUnderlyingType (target) ?? target;
			if (target.IsGenericType && target.GetGenericTypeDefinition () == typeof (List<>))
				{
				if (part != "-" && !part.All (char.IsDigit))
					throw new JsonException ("An array path segment must be an index or '-'.");
				target = target.GetGenericArguments ()[0];
				}
			else if (target.IsGenericType && target.GetGenericTypeDefinition () == typeof (Dictionary<,>))
				target = target.GetGenericArguments ()[1];
			else
				{
				var property = target.GetProperties ().SingleOrDefault (p => p.GetCustomAttribute<JsonPropertyNameAttribute> ()?.Name == part);
				target = property?.PropertyType ?? throw new JsonException ("The patch path is outside the published site model.");
				}
			}
		return target;
		}
	}