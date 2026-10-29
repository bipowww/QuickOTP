using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QuickOTP.Core.Rules;

public sealed class JsonRulesStore
{
	private static readonly JsonSerializerOptions Opts = new JsonSerializerOptions
	{
		WriteIndented = true,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		Converters = { (JsonConverter)new JsonStringEnumConverter() }
	};

	private readonly string _path;

	public JsonRulesStore(string? path = null)
	{
		string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuickOTP");
		Directory.CreateDirectory(text);
		_path = path ?? Path.Combine(text, "rules.json");
	}

	public UserRules Load()
	{
		try
		{
			if (!File.Exists(_path))
			{
				return new UserRules();
			}
			return JsonSerializer.Deserialize<UserRules>(File.ReadAllText(_path), Opts) ?? new UserRules();
		}
		catch
		{
			return new UserRules();
		}
	}

	public void Save(UserRules rules)
	{
		try
		{
			File.WriteAllText(_path, JsonSerializer.Serialize(rules, Opts));
		}
		catch
		{
		}
	}
}
