using System;
using Microsoft.Maui.Storage;

namespace StudyTime.DesktopClient.Services
{
    public class DeviceIdentityService : IDeviceIdentityService
    {
        private const string DeviceIdKey = "studytime_device_id";

        public string GetDeviceId()
        {
            var deviceId = Preferences.Get(DeviceIdKey, string.Empty);

            if (string.IsNullOrEmpty(deviceId))
            {
                deviceId = Guid.NewGuid().ToString("N");
                Preferences.Set(DeviceIdKey, deviceId);
            }

            return deviceId;
        }
    }
}
