using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using QuickOTP.Core.Models;

namespace QuickOTP.Core.History;

public sealed class SecureHistoryStore : IHistoryStore
{
	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		WriteIndented = false,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		Converters = { (JsonConverter)new JsonStringEnumConverter() }
	};

	private readonly string _path;

	private readonly object _lock = new object();

	private List<HistoryEntry> _cache = new List<HistoryEntry>();

	public SecureHistoryStore(string? path = null)
	{
		string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuickOTP");
		Directory.CreateDirectory(text);
		_path = path ?? Path.Combine(text, "history.dpapi");
		MigratePlainJson(text);
		Load();
	}

	public IReadOnlyList<HistoryEntry> GetEntries()
	{
		lock (_lock)
		{
			return (from e in _cache
				where !e.MarkedFalsePositive
				orderby e.Timestamp descending
				select e).ToList();
		}
	}

	public IReadOnlyList<HistoryEntry> GetValidEntries()
	{
		lock (_lock)
		{
			DateTimeOffset now = DateTimeOffset.Now;
			return (from e in _cache
				where !e.MarkedFalsePositive
				where !e.ExpiresAt.HasValue || e.ExpiresAt > now
				orderby e.Timestamp descending
				select e).ToList();
		}
	}

	public void Add(HistoryEntry entry)
	{
		lock (_lock)
		{
			_cache.Add(entry);
			Persist();
		}
	}

	public void Clear()
	{
		lock (_lock)
		{
			_cache.Clear();
			Persist();
		}
	}

	public void Prune(HistoryRetention retention)
	{
		lock (_lock)
		{
			if (retention == HistoryRetention.None)
			{
				_cache.Clear();
				try
				{
					if (File.Exists(_path))
					{
						File.Delete(_path);
					}
					return;
				}
				catch
				{
					return;
				}
			}
			bool flag = ((retention == (HistoryRetention)1 || retention == (HistoryRetention)24) ? true : false);
			TimeSpan timeSpan = (flag ? TimeSpan.FromHours((double)retention) : TimeSpan.FromMinutes((double)retention));
			DateTimeOffset cutoff = DateTimeOffset.UtcNow - timeSpan;
			_cache = _cache.Where((HistoryEntry e) => e.Timestamp >= cutoff).ToList();
			Persist();
		}
	}

	public void MarkFalsePositive(string messageId, string code)
	{
		lock (_lock)
		{
			foreach (HistoryEntry item in _cache.Where((HistoryEntry x) => x.MessageId == messageId && x.Code == code))
			{
				item.MarkedFalsePositive = true;
			}
			Persist();
		}
	}

	private void Load()
	{
		try
		{
			if (!File.Exists(_path))
			{
				_cache = new List<HistoryEntry>();
				return;
			}
			byte[] array = ProtectedData.Unprotect(File.ReadAllBytes(_path), Entropy(), DataProtectionScope.CurrentUser);
			string json = Encoding.UTF8.GetString(array);
			CryptographicOperations.ZeroMemory(array);
			_cache = JsonSerializer.Deserialize<List<HistoryEntry>>(json, JsonOptions) ?? new List<HistoryEntry>();
		}
		catch
		{
			_cache = new List<HistoryEntry>();
		}
	}

	private void Persist()
	{
		try
		{
			string s = JsonSerializer.Serialize(_cache, JsonOptions);
			byte[] bytes = Encoding.UTF8.GetBytes(s);
			File.WriteAllBytes(bytes: ProtectedData.Protect(bytes, Entropy(), DataProtectionScope.CurrentUser), path: _path);
			CryptographicOperations.ZeroMemory(bytes);
		}
		catch
		{
		}
	}

	private void MigratePlainJson(string dir)
	{
		try
		{
			string path = Path.Combine(dir, "history.json");
			if (File.Exists(path) && !File.Exists(_path))
			{
				List<HistoryEntry> list = JsonSerializer.Deserialize<List<HistoryEntry>>(File.ReadAllText(path), JsonOptions);
				if (list != null)
				{
					_cache = list;
					Persist();
					File.Delete(path);
				}
			}
		}
		catch
		{
		}
	}

	private static byte[] Entropy()
	{
		return Encoding.UTF8.GetBytes("QuickOTP.History.v1");
	}
}
