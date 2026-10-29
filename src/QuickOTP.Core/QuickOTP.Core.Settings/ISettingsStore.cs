using QuickOTP.Core.Models;

namespace QuickOTP.Core.Settings;

public interface ISettingsStore
{
	AppSettings Load();

	void Save(AppSettings settings);
}
