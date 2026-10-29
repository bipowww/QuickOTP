using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace QuickOTP.Core.Security;

public sealed class DpapiTokenStorage : ITokenStorage
{
	private sealed class LegacyPathStorage : ITokenStorage
	{
		private readonly string _path;

		public LegacyPathStorage(string path)
		{
			_path = path;
		}

		public void SaveTokens(string refreshToken, string? accessToken, DateTimeOffset? accessTokenExpiry, string? email)
		{
			string s = string.Join('\n', refreshToken ?? "", accessToken ?? "", accessTokenExpiry?.ToUnixTimeSeconds().ToString() ?? "", email ?? "");
			byte[] bytes = Encoding.UTF8.GetBytes(s);
			byte[] bytes2 = ProtectedData.Protect(bytes, Encoding.UTF8.GetBytes("QuickOTP.OAuth.v1"), DataProtectionScope.CurrentUser);
			Directory.CreateDirectory(Path.GetDirectoryName(_path));
			File.WriteAllBytes(_path, bytes2);
			CryptographicOperations.ZeroMemory(bytes);
		}

		public StoredTokens? LoadTokens()
		{
			try
			{
				if (!File.Exists(_path))
				{
					return null;
				}
				byte[] array = ProtectedData.Unprotect(File.ReadAllBytes(_path), Encoding.UTF8.GetBytes("QuickOTP.OAuth.v1"), DataProtectionScope.CurrentUser);
				string text = Encoding.UTF8.GetString(array);
				CryptographicOperations.ZeroMemory(array);
				string[] array2 = text.Split('\n');
				if (array2.Length < 1 || string.IsNullOrEmpty(array2[0]))
				{
					return null;
				}
				DateTimeOffset? accessTokenExpiry = null;
				if (array2.Length > 2 && long.TryParse(array2[2], out var result))
				{
					accessTokenExpiry = DateTimeOffset.FromUnixTimeSeconds(result);
				}
				return new StoredTokens
				{
					RefreshToken = array2[0],
					AccessToken = ((array2.Length > 1 && array2[1].Length > 0) ? array2[1] : null),
					AccessTokenExpiry = accessTokenExpiry,
					Email = ((array2.Length > 3 && array2[3].Length > 0) ? array2[3] : null)
				};
			}
			catch
			{
				return null;
			}
		}

		public void Clear()
		{
			try
			{
				if (File.Exists(_path))
				{
					File.Delete(_path);
				}
			}
			catch
			{
			}
		}

		public bool HasTokens()
		{
			if (File.Exists(_path))
			{
				return new FileInfo(_path).Length > 0;
			}
			return false;
		}
	}

	private readonly ITokenStorage _inner;

	public DpapiTokenStorage(string? path = null)
	{
		if (path != null)
		{
			_inner = new LegacyPathStorage(path);
			return;
		}
		DpapiAccountTokenVault dpapiAccountTokenVault = new DpapiAccountTokenVault();
		_inner = dpapiAccountTokenVault.ForAccount("default");
	}

	public void SaveTokens(string refreshToken, string? accessToken, DateTimeOffset? accessTokenExpiry, string? email)
	{
		_inner.SaveTokens(refreshToken, accessToken, accessTokenExpiry, email);
	}

	public StoredTokens? LoadTokens()
	{
		return _inner.LoadTokens();
	}

	public void Clear()
	{
		_inner.Clear();
	}

	public bool HasTokens()
	{
		return _inner.HasTokens();
	}
}
