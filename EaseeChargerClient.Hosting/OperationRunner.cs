using System.Reflection;
using System.Text.Json;

namespace EaseeChargerClient.Hosting;

/// <summary>Shared typed-operation discovery and invocation for the console and WPF workbench.</summary>
public static class OperationRunner
	{
	/// <summary>Finds an operation by its published identifier, ignoring identifier case.</summary>
	/// <returns>The matching published operation descriptor.</returns>
	/// <exception cref="ArgumentException">The operation identifier is unknown.</exception>
	/// <param name="id">The published operation identifier.</param>
	public static EaseeOperation Find (string id) => EaseeOperations.All.FirstOrDefault (x => x.Id.Equals (id, StringComparison.OrdinalIgnoreCase)) ?? throw new ArgumentException ("Unknown Easee operation: " + id);
	/// <summary>Describes the operation route and its typed parameters.</summary>
	/// <returns>A multiline description of the operation and required or optional parameters.</returns>
	/// <param name="operation">The operation descriptor from the published catalogue.</param>
	public static string Describe (EaseeOperation operation)
		{
		MethodInfo method = typeof (EaseeClient).GetMethod (operation.MethodName)!;
		return operation.Id + " " + operation.HttpMethod + " " + operation.Path + Environment.NewLine + string.Join (Environment.NewLine, method.GetParameters ().Where (x => x.Name is not "cancellationToken" and not "requestOptions").Select (x => x.Name + " : " + x.ParameterType.Name + (x.HasDefaultValue ? " (optional)" : " (required)")));
		}
	/// <summary>Deserializes input into typed models and invokes the selected REST method once.</summary>
	/// <returns>A task yielding the typed response wrapper or an owned stream response that the caller must dispose.</returns>
	/// <exception cref="ArgumentException">A parameter is unknown, missing or invalid.</exception>
	/// <exception cref="System.Text.Json.JsonException">The input does not match the operation-specific typed model.</exception>
	/// <exception cref="EaseeApiException">Easee returned a non-success HTTP status.</exception>
	/// <exception cref="OperationCanceledException">The operation was cancelled.</exception>
	/// <param name="client">The authenticated client on which to invoke the typed operation.</param>
	/// <param name="operation">The operation descriptor from the published catalogue.</param>
	/// <param name="arguments">Path and query parameter names and textual values. Non-string values must use JSON syntax.</param>
	/// <param name="body">The JSON request body, or null when the operation has no body. Deserialized into the operation-specific model.</param>
	/// <param name="options">Additional headers for the request, or null for defaults.</param>
	/// <param name="cancellationToken">Cancels the asynchronous operation. Cancellation does not undo a request already accepted by the server.</param>
	public static async Task<object?> InvokeAsync (EaseeClient client, EaseeOperation operation, IReadOnlyDictionary<string, string> arguments, string? body, EaseeRequestOptions? options = null, CancellationToken cancellationToken = default)
		{
		MethodInfo method = typeof (EaseeClient).GetMethod (operation.MethodName)!;
		ParameterInfo[] parameters = method.GetParameters ();
		foreach (string name in arguments.Keys)
			if (!parameters.Any (p => p.Name == name && p.Name is not "body" and not "requestOptions" and not "cancellationToken"))
				throw new ArgumentException ("Unknown parameter: " + name);
		object?[] values = parameters.Select (p =>
		{
			if (p.Name == "cancellationToken")
				return (object)cancellationToken;
			if (p.Name == "requestOptions")
				return options;
			if (p.Name == "body")
				{
				if (body is null && !p.HasDefaultValue)
					throw new ArgumentException ("A JSON request body is required.");
				return body is null ? null : JsonSerializer.Deserialize (body, p.ParameterType);
				}
			if (arguments.TryGetValue (p.Name!, out string? value))
				return p.ParameterType == typeof (string) ? value : JsonSerializer.Deserialize (value, p.ParameterType);
			if (!p.HasDefaultValue)
				throw new ArgumentException ("Missing parameter: " + p.Name);
			return p.DefaultValue;
		}).ToArray ();
		Task task;
		try
			{
			task = (Task)method.Invoke (client, values)!;
			}
		catch (TargetInvocationException error) when (error.InnerException is not null) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture (error.InnerException).Throw (); throw; }
		await task.ConfigureAwait (false);
		return task.GetType ().GetProperty ("Result")!.GetValue (task);
		}
	}