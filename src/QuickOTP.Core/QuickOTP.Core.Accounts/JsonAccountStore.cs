using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using QuickOTP.Core.Models;

namespace QuickOTP.Core.Accounts;

public sealed class JsonAccountStore : IAccountStore
{
	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		WriteIndented = true,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		Converters = { (JsonConverter)new JsonStringEnumConverter() }
	};

	private readonly string _path;

	private readonly object _lock = new object();

	private List<AccountInfo> _accounts = new List<AccountInfo>();

	public JsonAccountStore(string? path = null)
	{
		string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuickOTP");
		Directory.CreateDirectory(text);
		_path = path ?? Path.Combine(text, "accounts.json");
		Load();
	}

	public IReadOnlyList<AccountInfo> GetAll()
	{
		lock (_lock)
		{
			return _accounts.ToList();
		}
	}

	public AccountInfo? Get(string id)
	{
		lock (_lock)
		{
			return _accounts.FirstOrDefault((AccountInfo a) => a.Id == id);
		}
	}

	public void Upsert(AccountInfo account)
	{
		lock (_lock)
		{
			int num = _accounts.FindIndex((AccountInfo a) => a.Id == account.Id);
			if (num >= 0)
			{
				_accounts[num] = account;
			}
			else
			{
				_accounts.Add(account);
			}
			Persist();
		}
	}

	public void Remove(string id)
	{
		lock (_lock)
		{
			_accounts.RemoveAll((AccountInfo a) => a.Id == id);
			Persist();
		}
	}

	public void Save()
	{
		lock (_lock)
		{
			Persist();
		}
	}

	private void Load()
	{
		try
		{
			if (!File.Exists(_path))
			{
				_accounts = new List<AccountInfo>();
				return;
			}
			string json = File.ReadAllText(_path);
			_accounts = JsonSerializer.Deserialize<List<AccountInfo>>(json, JsonOptions) ?? new List<AccountInfo>();
		}
		catch
		{
			_accounts = new List<AccountInfo>();
		}
	}

	private void Persist()
	{
		try
		{
			File.WriteAllText(_path, JsonSerializer.Serialize(_accounts, JsonOptions));
		}
		catch
		{
		}
	}
}
