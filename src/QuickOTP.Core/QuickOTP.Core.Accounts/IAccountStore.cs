using System.Collections.Generic;
using QuickOTP.Core.Models;

namespace QuickOTP.Core.Accounts;

public interface IAccountStore
{
	IReadOnlyList<AccountInfo> GetAll();

	AccountInfo? Get(string id);

	void Upsert(AccountInfo account);

	void Remove(string id);

	void Save();
}
