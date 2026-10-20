using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EaseeChargerClient;

/// <summary>Identifies the published REST operation implemented by a client method.</summary>
/// <param name="id">The published operation identifier.</param>
/// <param name="method">The HTTP method.</param>
/// <param name="path">The published URI template.</param>
[AttributeUsage (AttributeTargets.Method)]
public sealed class EaseeOperationAttribute (string id, string method, string path) : Attribute
	{
	/// <summary>The published operation identifier.</summary>
	/// <value>The published operation identifier.</value>
	public string Id { get; } = id;
	/// <summary>The HTTP method used by the operation.</summary>
	/// <value>The HTTP method used by the operation.</value>
	public string Method { get; } = method;
	/// <summary>The published URI template, including any parameter placeholders.</summary>
	/// <value>The published URI template, including any parameter placeholders.</value>
	public string Path { get; } = path;
	}

/// <summary>Describes one supported REST operation without invoking it.</summary>
/// <param name="id">The published operation identifier.</param>
/// <param name="methodName">The implementing asynchronous C# method name.</param>
/// <param name="httpMethod">The HTTP method.</param>
/// <param name="path">The published URI template.</param>
/// <param name="category">The published operation category.</param>
/// <param name="summary">The published operation summary.</param>
public sealed class EaseeOperation (string id, string methodName, string httpMethod, string path, string category, string summary)
	{
	/// <summary>The published operation identifier.</summary>
	/// <value>The published operation identifier.</value>
	public string Id { get; } = id;
	/// <summary>The C# asynchronous method name implementing the operation.</summary>
	/// <value>The C# asynchronous method name implementing the operation.</value>
	public string MethodName { get; } = methodName;
	/// <summary>The HTTP method used by the operation.</summary>
	/// <value>The HTTP method used by the operation.</value>
	public string HttpMethod { get; } = httpMethod;
	/// <summary>The published URI template, including any parameter placeholders.</summary>
	/// <value>The published URI template, including any parameter placeholders.</value>
	public string Path { get; } = path;
	/// <summary>The first category tag in the published specification.</summary>
	/// <value>The first category tag in the published specification.</value>
	public string Category { get; } = category;
	/// <summary>The operation summary supplied by Easee.</summary>
	/// <value>The operation summary supplied by Easee.</value>
	public string Summary { get; } = summary;
	}

/// <summary>Supplies additional headers for a single REST request.</summary>
public sealed class EaseeRequestOptions
	{
	/// <summary>Additional headers, including traceparent for Direct OCPP workflows. Authorization is controlled by the token provider.</summary>
	/// <value>Additional request headers, including traceparent for Direct OCPP. Authorization and Host cannot be overridden.</value>
	public IDictionary<string, string> Headers { get; } = new Dictionary<string, string> (StringComparer.OrdinalIgnoreCase);
	}

/// <summary>Marks a successful REST operation whose contract has no response body.</summary>
public sealed class EaseeNoContent
	{
	internal EaseeNoContent ()
		{
		}
	}

/// <summary>Contains the HTTP status, typed response body and response headers.</summary>
/// <typeparam name="T">The concrete response or value model type.</typeparam>
/// <param name="statusCode">The HTTP response status.</param>
/// <param name="value">The typed value to retain or serialize; may be null when its type permits null.</param>
/// <param name="headers">The response and content headers.</param>
public sealed class EaseeResponse<T> (HttpStatusCode statusCode, T? value, IReadOnlyDictionary<string, string[]> headers)
	{
	/// <summary>The HTTP status code returned by the server.</summary>
	/// <value>The HTTP status code returned by the server.</value>
	public HttpStatusCode StatusCode { get; } = statusCode;
	/// <summary>The deserialized response body, or the default value when the operation returns no body.</summary>
	/// <value>The deserialized response body, or the default value when the operation returns no body.</value>
	public T? Value { get; } = value;
	/// <summary>The response and content headers, indexed without regard to header-name case.</summary>
	/// <value>The response and content headers, indexed without regard to header-name case.</value>
	public IReadOnlyDictionary<string, string[]> Headers { get; } = headers;
	}

/// <summary>Owns a streamed or binary HTTP response. Dispose after reading Content.</summary>
public sealed class EaseeStreamResponse : IDisposable
	{
	private readonly HttpResponseMessage _response;
	internal EaseeStreamResponse (HttpResponseMessage response, Stream content)
		{
		_response = response;
		Content = content;
		}
	/// <summary>The HTTP status code returned by the server.</summary>
	/// <value>The HTTP status code returned by the server.</value>
	public HttpStatusCode StatusCode => _response.StatusCode;
	/// <summary>The response media type, or null when no Content-Type header was returned.</summary>
	/// <value>The response media type, or null when no Content-Type header was returned.</value>
	public string? ContentType => _response.Content?.Headers.ContentType?.ToString ();
	/// <summary>The response stream. Consume it before disposing this response.</summary>
	/// <value>The response stream. Consume it before disposing this response.</value>
	public Stream Content
		{
		get;
		}
	/// <summary>The response and content headers, indexed without regard to header-name case.</summary>
	/// <value>The response and content headers, indexed without regard to header-name case.</value>
	public IReadOnlyDictionary<string, string[]> Headers => EaseeClient.GetHeaders (_response);
	/// <summary>Disposes the HTTP response and its content stream.</summary>
	public void Dispose () => _response.Dispose ();
	}

/// <summary>Exposes the common problem details fields returned by Easee services.</summary>
public interface IEaseeProblemDetails
	{
	/// <summary>The problem type URI reference, or null when absent.</summary>
	/// <value>The problem type URI reference, or null when absent.</value>
	string? Type
		{
		get;
		}
	/// <summary>The problem title, or null when absent.</summary>
	/// <value>The problem title, or null when absent.</value>
	string? Title
		{
		get;
		}
	/// <summary>The HTTP status declared in the problem body, or null when absent. The HTTP response status is authoritative.</summary>
	/// <value>The HTTP status declared in the problem body, or null when absent. The HTTP response status is authoritative.</value>
	int? Status
		{
		get;
		}
	/// <summary>The server-provided explanation of this problem occurrence, or null when absent.</summary>
	/// <value>The server-provided explanation of this problem occurrence, or null when absent.</value>
	string? Detail
		{
		get;
		}
	/// <summary>The problem occurrence URI reference, or null when absent.</summary>
	/// <value>The problem occurrence URI reference, or null when absent.</value>
	string? Instance
		{
		get;
		}
	/// <summary>The server trace identifier, or null when absent.</summary>
	/// <value>The server trace identifier, or null when absent.</value>
	string? TraceId
		{
		get;
		}
	}

/// <summary>Provides common problem details and calculated views of supported validation errors.</summary>
public sealed class EaseeProblem : IEaseeProblemDetails
	{
	/// <summary>The server trace identifier, or null when absent.</summary>
	/// <value>The server trace identifier, or null when absent.</value>
	[JsonPropertyName ("traceId")]
	public string? TraceId
		{
		get; set;
		}
	/// <summary>The typed OCPP validation failures, or null for other problem contracts.</summary>
	/// <value>The typed OCPP validation failures, or null for other problem contracts.</value>
	[JsonIgnore]
	public List<Models.Easee_OCPP_Configuration_API_ProblemDetails_Error>? ValidationFailures
		{
		get; private set;
		}
	internal static EaseeProblem From (IEaseeProblemDetails source) => new ()
		{
		Type = source.Type,
		Title = source.Title,
		Status = source.Status,
		Detail = source.Detail,
		Instance = source.Instance,
		TraceId = source.TraceId,
		Errors = (source as Models.Scheduled_charging_HttpValidationProblemDetails)?.Errors?.ToDictionary (x => x.Key, x => x.Value.ToArray ()),
		ValidationFailures = (source as Models.Easee_OCPP_Configuration_API_ProblemDetails)?.Errors
		};

	/// <summary>The problem type URI reference, or null when absent.</summary>
	/// <value>The problem type URI reference, or null when absent.</value>
	[JsonPropertyName ("type")]
	public string? Type
		{
		get; set;
		}
	/// <summary>The problem title, or null when absent.</summary>
	/// <value>The problem title, or null when absent.</value>
	[JsonPropertyName ("title")]
	public string? Title
		{
		get; set;
		}
	/// <summary>The HTTP status declared in the problem body, or null when absent. The HTTP response status is authoritative.</summary>
	/// <value>The HTTP status declared in the problem body, or null when absent. The HTTP response status is authoritative.</value>
	[JsonPropertyName ("status")]
	public int? Status
		{
		get; set;
		}
	/// <summary>The server-provided explanation of this problem occurrence, or null when absent.</summary>
	/// <value>The server-provided explanation of this problem occurrence, or null when absent.</value>
	[JsonPropertyName ("detail")]
	public string? Detail
		{
		get; set;
		}
	/// <summary>The problem occurrence URI reference, or null when absent.</summary>
	/// <value>The problem occurrence URI reference, or null when absent.</value>
	[JsonPropertyName ("instance")]
	public string? Instance
		{
		get; set;
		}
	/// <summary>Validation messages indexed by field name, or null when absent.</summary>
	/// <value>Validation messages indexed by field name, or null when absent.</value>
	[JsonPropertyName ("errors")]
	public Dictionary<string, string[]>? Errors
		{
		get; set;
		}
	}

/// <summary>Reports a non-success HTTP response with its operation and typed error details.</summary>
public sealed class EaseeApiException : Exception
	{
	internal EaseeApiException (string operation, HttpStatusCode status, EaseeProblem? problem, IReadOnlyDictionary<string, string[]> headers, object? errorResponse = null)
		: base ($"Easee operation {operation} returned HTTP {(int)status}.")
		{
		OperationId = operation;
		StatusCode = status;
		Problem = problem;
		ErrorResponse = errorResponse;
		Headers = headers;
		}
	/// <summary>The identifier of the failed REST operation.</summary>
	/// <value>The identifier of the failed REST operation.</value>
	public string OperationId
		{
		get;
		}
	/// <summary>The HTTP status code returned by the server.</summary>
	/// <value>The HTTP status code returned by the server.</value>
	public HttpStatusCode StatusCode
		{
		get;
		}
	/// <summary>The published error DTO or typed error collection for this operation/status, when present.</summary>
	/// <value>The published error DTO or typed error collection, or null when no valid JSON error body was returned.</value>
	public object? ErrorResponse
		{
		get;
		}
	/// <summary>The common problem details view, or null for errors that do not use a problem details contract.</summary>
	/// <value>The common problem details view, or null for errors that do not use a problem details contract.</value>
	public EaseeProblem? Problem
		{
		get;
		}
	/// <summary>The response and content headers, indexed without regard to header-name case.</summary>
	/// <value>The response and content headers, indexed without regard to header-name case.</value>
	public IReadOnlyDictionary<string, string[]> Headers
		{
		get;
		}
	}

/// <summary>A JSON value only for fields whose published schema deliberately permits arbitrary data.</summary>
[JsonConverter (typeof (EaseeValueConverter))]
public sealed class EaseeValue
	{
	internal JsonElement Element
		{
		get;
		}
	internal EaseeValue (JsonElement element) => Element = element.Clone ();
	/// <summary>The JSON value kind name, such as String, Number, Object, Array or Null.</summary>
	/// <value>The JSON value kind name, such as String, Number, Object, Array or Null.</value>
	public string Kind => Element.ValueKind.ToString ();
	/// <summary>The string value, or null when this value is not a JSON string.</summary>
	/// <value>The string value, or null when this value is not a JSON string.</value>
	public string? String => Element.ValueKind == JsonValueKind.String ? Element.GetString () : null;
	/// <summary>The Boolean value, or null when this value is not a JSON Boolean.</summary>
	/// <value>The Boolean value, or null when this value is not a JSON Boolean.</value>
	public bool? Boolean => Element.ValueKind is JsonValueKind.True or JsonValueKind.False ? Element.GetBoolean () : null;
	/// <summary>The number as a double, or null when this value is not a JSON number.</summary>
	/// <value>The number as a double, or null when this value is not a JSON number.</value>
	public double? Number => Element.ValueKind == JsonValueKind.Number ? Element.GetDouble () : null;
	/// <summary>The array elements, or an empty list when this value is not an array.</summary>
	/// <value>The array elements, or an empty list when this value is not an array.</value>
	public IReadOnlyList<EaseeValue> Items => Element.ValueKind == JsonValueKind.Array ? Element.EnumerateArray ().Select (x => new EaseeValue (x)).ToArray () : [];
	/// <summary>The object members, or an empty dictionary when this value is not an object.</summary>
	/// <value>The object members, or an empty dictionary when this value is not an object.</value>
	public IReadOnlyDictionary<string, EaseeValue> Properties => Element.ValueKind == JsonValueKind.Object ? Element.EnumerateObject ().ToDictionary (x => x.Name, x => new EaseeValue (x.Value)) : new Dictionary<string, EaseeValue> ();
	/// <summary>Serializes a typed value into an open-ended problem extension.</summary>
	/// <returns>An independent JSON-backed value for an explicitly open schema field.</returns>
	/// <typeparam name="T">The concrete response or value model type.</typeparam>
	/// <param name="value">The typed value to retain or serialize; may be null when its type permits null.</param>
	public static EaseeValue From<T> (T value) => new (JsonSerializer.SerializeToElement (value, EaseeJson.Options));
	}

internal sealed class EaseeValueConverter : JsonConverter<EaseeValue>
	{
	public override EaseeValue? Read (ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
		using JsonDocument document = JsonDocument.ParseValue (ref reader);
		return new EaseeValue (document.RootElement);
		}
	public override void Write (Utf8JsonWriter writer, EaseeValue value, JsonSerializerOptions options) => value.Element.WriteTo (writer);
	}

internal abstract class EaseeStringEnumConverter<T> (Dictionary<T, string> names) : JsonConverter<T> where T : struct, Enum
	{
	public override T Read (ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
		if (reader.TokenType == JsonTokenType.String)
			foreach (KeyValuePair<T, string> entry in names)
				if (entry.Value == reader.GetString ())
					return entry.Key;
		throw new JsonException ("Unknown or invalid Easee enum value.");
		}
	public override void Write (Utf8JsonWriter writer, T value, JsonSerializerOptions options)
		{
		if (!names.TryGetValue (value, out string? name))
			throw new JsonException ("Unknown Easee enum value.");
		writer.WriteStringValue (name);
		}
	}

/// <summary>Serializes and deserializes Easee models using their JSON attributes and converters.</summary>
public static class EaseeJson
	{
	internal static JsonSerializerOptions Options { get; } = new () { PropertyNameCaseInsensitive = false };
	/// <summary>Serializes a typed Easee model using its JSON attributes.</summary>
	/// <returns>The JSON representation of the supplied value.</returns>
	/// <exception cref="System.Text.Json.JsonException">The model cannot be serialized under its declared contract.</exception>
	/// <typeparam name="T">The concrete response or value model type.</typeparam>
	/// <param name="value">The typed value to retain or serialize; may be null when its type permits null.</param>
	public static string Serialize<T> (T value) => JsonSerializer.Serialize (value, Options);
	/// <summary>Deserializes a typed Easee model using its JSON attributes.</summary>
	/// <returns>The deserialized model, or null when the JSON value is null.</returns>
	/// <exception cref="System.Text.Json.JsonException">The JSON does not match the requested model contract.</exception>
	/// <typeparam name="T">The concrete response or value model type.</typeparam>
	/// <param name="text">The JSON text to deserialize using the Easee model contracts.</param>
	public static T? Deserialize<T> (string text) => JsonSerializer.Deserialize<T> (text, Options);
	}