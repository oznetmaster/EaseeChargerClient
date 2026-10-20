using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace EaseeChargerClient.Tests;

/// <summary>Independently reads the vendor specification at test time, exercising every supported operation through its public method.</summary>
[TestFixture]
public sealed class ApiContractTests
	{
	private static JsonDocument ReadSpec ()
		{
		using Stream source = typeof (ApiContractTests).Assembly.GetManifestResourceStream ("EaseeChargerClient.Tests.ApiContract.data") ?? throw new InvalidDataException ("The published API contract resource is missing.");
		return JsonDocument.Parse (source);
		}
	public static IEnumerable<TestCaseData> Operations ()
		{
		using JsonDocument spec = ReadSpec ();
		foreach (JsonProperty path in spec.RootElement.GetProperty ("paths").EnumerateObject ())
			foreach (JsonProperty operation in path.Value.EnumerateObject ())
				if (operation.Value.TryGetProperty ("operationId", out JsonElement id) && (!operation.Value.TryGetProperty ("deprecated", out JsonElement deprecated) || !deprecated.GetBoolean ()))
					yield return new TestCaseData (path.Name, operation.Name, operation.Value.Clone ()).SetName ("Contract_" + id.GetString ());
		}
	[TestCaseSource (nameof (Operations))]
	public async Task EveryOperationUsesPublishedRouteParametersBodyAndResponse (string path, string verb, JsonElement specification)
		{
		string id = specification.GetProperty ("operationId").GetString ()!;
		MethodInfo method = typeof (EaseeClient).GetMethods ().Single (m => m.GetCustomAttribute<EaseeOperationAttribute> ()?.Id == id);
		using JsonDocument spec = ReadSpec ();
		JsonElement schemas = spec.RootElement.GetProperty ("components").GetProperty ("schemas");
		JsonProperty[] successes = specification.GetProperty ("responses").EnumerateObject ().Where (x => x.Name.StartsWith ("2", StringComparison.Ordinal)).ToArray ();
		// The vendor documents only a 401 for AuthorizeCharging_StartCharging. Exercise transport without inventing a response schema.
		int statusCode = successes.Length == 0 ? 204 : int.Parse (successes[0].Name);
		string response = "";
		if (successes.Length > 0 && successes[0].Value.TryGetProperty ("content", out JsonElement media))
			response = Sample (media.EnumerateObject ().First ().Value.GetProperty ("schema"), schemas, 0);
		using var handler = new FakeHttp (async (request, _) =>
		{
			Assert.That (request.Method.Method, Is.EqualTo (verb.ToUpperInvariant ()));
			Assert.That (request.Headers.Authorization?.Scheme, Is.EqualTo (id is "Account_Authenticate" or "Account_RefreshToken" ? null : "Bearer"));
			string expectedPath = path;
			if (specification.TryGetProperty ("parameters", out JsonElement parameters))
				foreach (JsonElement parameter in parameters.EnumerateArray ())
					{
					string name = parameter.GetProperty ("name").GetString ()!;
					ParameterInfo info = method.GetParameters ().Single (p => p.Name == name);
					object? value = Input (info.ParameterType);
					if (parameter.GetProperty ("in").GetString () == "path")
						expectedPath = expectedPath.Replace ("{" + name + "}", Uri.EscapeDataString (Wire (value!)));
					else
						{
						string query = request.RequestUri!.Query;
						Assert.That (query, Does.Contain (Uri.EscapeDataString (name) + "="));
						foreach (object element in value is System.Collections.IEnumerable sequence && value is not string ? sequence : new[] { value })
							Assert.That (query, Does.Contain (Uri.EscapeDataString (name) + "=" + Uri.EscapeDataString (Wire (element))));
						}
					}
			Assert.That (request.RequestUri!.AbsolutePath, Is.EqualTo (expectedPath));
			if (specification.TryGetProperty ("requestBody", out JsonElement body))
				{
				Assert.That (request.Content, Is.Not.Null);
				using JsonDocument actual = JsonDocument.Parse (await request.Content!.ReadAsStringAsync ());
				JsonElement expectedSchema = body.GetProperty ("content").GetProperty ("application/json").GetProperty ("schema");
				using JsonDocument expected = JsonDocument.Parse (Sample (expectedSchema, schemas, 0));
				Assert.That (actual.RootElement.ValueKind, Is.EqualTo (expected.RootElement.ValueKind));
				}
			else
				Assert.That (request.Content, Is.Null);
			return FakeHttp.Json (response, (HttpStatusCode)statusCode);
		});
		using var http = new HttpClient (handler);
		using var client = new EaseeClient (new ()
			{
			TokenProvider = new TokenProvider ()
			}, http);
		object?[] args = method.GetParameters ().Select (p => p.Name is "cancellationToken" ? (object)CancellationToken.None : p.Name == "requestOptions" ? null : p.Name == "body" ? JsonSerializer.Deserialize (Sample (specification.GetProperty ("requestBody").GetProperty ("content").GetProperty ("application/json").GetProperty ("schema"), schemas, 0), p.ParameterType) : Input (p.ParameterType)).ToArray ();
		var task = (Task)method.Invoke (client, args)!;
		await task;
		object result = task.GetType ().GetProperty ("Result")!.GetValue (task)!;
		if (result is IDisposable disposable)
			disposable.Dispose ();
		Assert.That (handler.Calls, Is.EqualTo (1));
		}
	internal static object? Input (Type type)
		{
		type = Nullable.GetUnderlyingType (type) ?? type;
		if (type == typeof (string))
			return "sample+value";
		if (type == typeof (DateTimeOffset))
			return new DateTimeOffset (2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
		if (type == typeof (Guid))
			return Guid.Parse ("11111111-2222-3333-4444-555555555555");
		if (type == typeof (bool))
			return true;
		if (type.IsEnum)
			return Enum.GetValues (type).GetValue (0);
		if (type.IsGenericType && type.GetGenericTypeDefinition () == typeof (List<>))
			{
			var list = (System.Collections.IList)Activator.CreateInstance (type)!;
			list.Add (Input (type.GenericTypeArguments[0]));
			return list;
			}
		return Convert.ChangeType (7, type, System.Globalization.CultureInfo.InvariantCulture);
		}
	private static string Wire (object value)
		{
		if (value is DateTimeOffset date)
			return date.ToString ("O", System.Globalization.CultureInfo.InvariantCulture);
		if (value is bool boolean)
			return boolean ? "true" : "false";
		if (value is Enum e)
			return e.GetType ().GetCustomAttribute<System.Text.Json.Serialization.JsonConverterAttribute> () is null ? Convert.ToInt64 (e).ToString () : JsonSerializer.Serialize (e, e.GetType ()).Trim ('"');
		return Convert.ToString (value, System.Globalization.CultureInfo.InvariantCulture)!;
		}
	internal static string Sample (JsonElement schema, JsonElement schemas, int depth)
		{
		if (schema.TryGetProperty ("$ref", out JsonElement reference))
			return reference.GetString ()!.EndsWith ("/Easee_Cloud_API_Operation", StringComparison.Ordinal) ? "{\"op\":\"replace\",\"path\":\"/name\",\"value\":\"example\"}" : Sample (schemas.GetProperty (reference.GetString ()!.Split ('/').Last ()), schemas, depth);
		if (schema.TryGetProperty ("oneOf", out JsonElement one))
			return Sample (one[0], schemas, depth);
		if (schema.TryGetProperty ("allOf", out JsonElement all))
			{
			var merged = new Dictionary<string, JsonElement> ();
			foreach (JsonElement item in all.EnumerateArray ())
				{
				using JsonDocument d = JsonDocument.Parse (Sample (item, schemas, depth));
				foreach (JsonProperty p in d.RootElement.EnumerateObject ())
					merged[p.Name] = p.Value.Clone ();
				}
			return JsonSerializer.Serialize (merged);
			}
		if (schema.TryGetProperty ("enum", out JsonElement enumeration))
			return enumeration[0].GetRawText ();
		if (!schema.TryGetProperty ("type", out JsonElement kind))
			return "null";
		return kind.GetString () switch
			{
				"string" => schema.TryGetProperty ("format", out JsonElement f) && f.GetString () == "date-time" ? "\"2026-01-02T03:04:05Z\"" : f.ValueKind == JsonValueKind.String && f.GetString () == "guid" ? "\"11111111-2222-3333-4444-555555555555\"" : "\"example\"",
				"integer" => "7",
				"number" => "1.25",
				"boolean" => "true",
				"array" => depth > 6 ? "[]" : "[" + Sample (schema.GetProperty ("items"), schemas, depth + 1) + "]",
				"object" => depth > 6 ? "{}" : schema.TryGetProperty ("properties", out JsonElement props) ? "{" + string.Join (",", props.EnumerateObject ().Select (p => JsonSerializer.Serialize (p.Name) + ":" + Sample (p.Value, schemas, depth + 1))) + "}" : "{}",
				_ => throw new InvalidOperationException ("Unsupported schema in test fixture.")
				};
		}
	[Test]
	public void SurfaceMatchesTheVendorInventoryAndExcludesDeprecatedMethods ()
		{
		using JsonDocument spec = ReadSpec ();
		var methods = typeof (EaseeClient).GetMethods ().Select (x => x.GetCustomAttribute<EaseeOperationAttribute> ()).Where (x => x is not null).ToArray ();
		Assert.That (methods, Has.Length.EqualTo (168));
		Assert.That (EaseeOperations.All.Select (x => x.Id), Is.EquivalentTo (methods.Select (x => x!.Id)));
		foreach (JsonProperty path in spec.RootElement.GetProperty ("paths").EnumerateObject ())
			foreach (JsonProperty operation in path.Value.EnumerateObject ())
				if (operation.Value.TryGetProperty ("deprecated", out JsonElement d) && d.GetBoolean ())
					Assert.That (methods.Select (x => x!.Id), Does.Not.Contain (operation.Value.GetProperty ("operationId").GetString ()));
		}
	public static IEnumerable<TestCaseData> Schemas ()
		{
		using JsonDocument spec = ReadSpec ();
		foreach (JsonProperty schema in spec.RootElement.GetProperty ("components").GetProperty ("schemas").EnumerateObject ())
			yield return new TestCaseData (schema.Name).SetName ("Schema_" + schema.Name);
		}
	[TestCaseSource (nameof (Schemas))]
	public void EveryPublishedSchemaCanDeserializeItsWireFields (string name)
		{
		using JsonDocument spec = ReadSpec ();
		JsonElement schemas = spec.RootElement.GetProperty ("components").GetProperty ("schemas");
		Type type = typeof (EaseeClient).Assembly.GetType ("EaseeChargerClient.Models." + name.Replace ('.', '_'))!;
		string fixture = name == "Easee_Cloud_API_Operation" ? "{\"op\":\"replace\",\"path\":\"/name\",\"value\":\"example\"}" : Sample (schemas.GetProperty (name), schemas, 0);
		object? model = JsonSerializer.Deserialize (fixture, type);
		Assert.That (model, Is.Not.Null);
		using JsonDocument original = JsonDocument.Parse (fixture);
		using JsonDocument roundTrip = JsonDocument.Parse (JsonSerializer.Serialize (model, type));
		if (original.RootElement.ValueKind == JsonValueKind.Object)
			foreach (JsonProperty field in original.RootElement.EnumerateObject ())
				if (field.Value.ValueKind != JsonValueKind.Null)
					Assert.That (roundTrip.RootElement.TryGetProperty (field.Name, out _), Is.True, field.Name);
		}
	}