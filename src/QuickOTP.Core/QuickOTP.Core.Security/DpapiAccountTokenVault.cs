using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace QuickOTP.Core.Security;

public sealed class DpapiAccountTokenVault : IAccountTokenVault
{
	private sealed class AccountScopedTokenStorage(DpapiAccountTokenVault vault, string accountId) : ITokenStorage
	{
		public void SaveTokens(string refreshToken, string? accessToken, DateTimeOffset? accessTokenExpiry, string? email)
		{
			vault.Save(accountId, new StoredTokens
			{
				RefreshToken = refreshToken,
				AccessToken = accessToken,
				AccessTokenExpiry = accessTokenExpiry,
				Email = email
			});
		}

		public StoredTokens? LoadTokens()
		{
			return vault.Load(accountId);
		}

		public void Clear()
		{
			vault.Clear(accountId);
		}

		public bool HasTokens()
		{
			return vault.HasTokens(accountId);
		}
	}

	private readonly string _dir;

	public DpapiAccountTokenVault(string? directory = null)
	{
		_dir = directory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuickOTP", "tokens");
		Directory.CreateDirectory(_dir);
		MigrateLegacySingleAccount();
	}

	public ITokenStorage ForAccount(string accountId)
	{
		return new AccountScopedTokenStorage(this, accountId);
	}

	public void Save(string accountId, StoredTokens tokens)
	{
		string path = PathFor(accountId);
		string s = string.Join('\n', tokens.RefreshToken ?? "", tokens.AccessToken ?? "", tokens.AccessTokenExpiry?.ToUnixTimeSeconds().ToString() ?? "", tokens.Email ?? "");
		byte[] bytes = Encoding.UTF8.GetBytes(s);
		byte[] bytes2 = ProtectedData.Protect(bytes, GetEntropy(accountId), DataProtectionScope.CurrentUser);
		File.WriteAllBytes(path, bytes2);
		CryptographicOperations.ZeroMemory(bytes);
	}

	public StoredTokens? Load(string accountId)
	{
		try
		{
			string path = PathFor(accountId);
			if (!File.Exists(path))
			{
				return null;
			}
			byte[] array = File.ReadAllBytes(path);
			if (array.Length == 0)
			{
				return null;
			}
			byte[] array2 = ProtectedData.Unprotect(array, GetEntropy(accountId), DataProtectionScope.CurrentUser);
			string text = Encoding.UTF8.GetString(array2);
			CryptographicOperations.ZeroMemory(array2);
			string[] array3 = text.Split('\n');
			if (array3.Length < 1 || string.IsNullOrEmpty(array3[0]))
			{
				return null;
			}
			DateTimeOffset? accessTokenExpiry = null;
			if (array3.Length > 2 && long.TryParse(array3[2], out var result))
			{
				accessTokenExpiry = DateTimeOffset.FromUnixTimeSeconds(result);
			}
			return new StoredTokens
			{
				RefreshToken = array3[0],
				AccessToken = ((array3.Length > 1 && array3[1].Length > 0) ? array3[1] : null),
				AccessTokenExpiry = accessTokenExpiry,
				Email = ((array3.Length > 3 && array3[3].Length > 0) ? array3[3] : null)
			};
		}
		catch
		{
			return null;
		}
	}

	public void Clear(string accountId)
	{
		try
		{
			string path = PathFor(accountId);
			if (File.Exists(path))
			{
				File.Delete(path);
			}
		}
		catch
		{
		}
	}

	public bool HasTokens(string accountId)
	{
		string text = PathFor(accountId);
		if (File.Exists(text))
		{
			return new FileInfo(text).Length > 0;
		}
		return false;
	}

	private string PathFor(string accountId)
	{
		string text = string.Concat(accountId.Select((char c) => (!char.IsLetterOrDigit(c)) ? '_' : c));
		return Path.Combine(_dir, text + ".dpapi");
	}

	private static byte[] GetEntropy(string accountId)
	{
		return Encoding.UTF8.GetBytes("QuickOTP.OAuth.v2." + accountId);
	}

	private void MigrateLegacySingleAccount()
	{
		try
		{
			string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuickOTP", "tokens.dpapi");
			if (!File.Exists(path))
			{
				return;
			}
			string path2 = Path.Combine(_dir, ".migrated");
			if (File.Exists(path2))
			{
				return;
			}
			byte[] array = ProtectedData.Unprotect(File.ReadAllBytes(path), Encoding.UTF8.GetBytes("QuickOTP.OAuth.v1"), DataProtectionScope.CurrentUser);
			string text = Encoding.UTF8.GetString(array);
			CryptographicOperations.ZeroMemory(array);
			string[] array2 = text.Split('\n');
			if (array2.Length >= 1 && !string.IsNullOrEmpty(array2[0]))
			{
				DateTimeOffset? accessTokenExpiry = null;
				if (array2.Length > 2 && long.TryParse(array2[2], out var result))
				{
					accessTokenExpiry = DateTimeOffset.FromUnixTimeSeconds(result);
				}
				string accountId = "gmail-legacy";
				Save(accountId, new StoredTokens
				{
					RefreshToken = array2[0],
					AccessToken = ((array2.Length > 1 && array2[1].Length > 0) ? array2[1] : null),
					AccessTokenExpiry = accessTokenExpiry,
					Email = ((array2.Length > 3 && array2[3].Length > 0) ? array2[3] : null)
				});
			}
			File.WriteAllText(path2, DateTimeOffset.UtcNow.ToString("O"));
		}
		catch
		{
		}
	}
}
