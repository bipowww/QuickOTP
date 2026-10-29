using System;
using System.IO;
using System.Text.Json;
using QuickOTP.Core.Models;

namespace QuickOTP.Core.Settings;

public sealed class JsonSettingsStore : ISettingsStore
{
	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		WriteIndented = true,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase
	};

	private readonly string _path;

	private readonly object _lock = new object();

	public string FilePath => _path;

	public JsonSettingsStore(string? path = null)
	{
		string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuickOTP");
		Directory.CreateDirectory(text);
		_path = path ?? Path.Combine(text, "settings.json");
	}

	public AppSettings Load()
	{
		lock (_lock)
		{
			try
			{
				if (!File.Exists(_path))
				{
					return new AppSettings();
				}
				return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path), JsonOptions) ?? new AppSettings();
			}
			catch
			{
				return new AppSettings();
			}
		}
	}

	public void Save(AppSettings settings)
	{
		lock (_lock)
		{
			string directoryName = Path.GetDirectoryName(_path);
			if (!string.IsNullOrEmpty(directoryName))
			{
				Directory.CreateDirectory(directoryName);
			}
			string contents = JsonSerializer.Serialize(settings, JsonOptions);
			File.WriteAllText(_path, contents);
		}
	}
}
