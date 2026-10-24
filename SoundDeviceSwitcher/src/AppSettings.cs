using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SoundDeviceSwitcher
{
    internal sealed class AppSettings
    {
        private readonly string _path;
        private readonly Dictionary<string, string> _values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public AppSettings()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SoundDeviceSwitcher");
            _path = Path.Combine(dir, "settings.ini");
            Load();
        }

        public string SystemOutputDeviceId
        {
            get { return Get("SystemOutputDeviceId"); }
            set { Set("SystemOutputDeviceId", value); }
        }

        public string SystemInputDeviceId
        {
            get { return Get("SystemInputDeviceId"); }
            set { Set("SystemInputDeviceId", value); }
        }

        public string VoicemeeterInputDeviceKey
        {
            get { return Get("VoicemeeterInputDeviceKey"); }
            set { Set("VoicemeeterInputDeviceKey", value); }
        }

        public string VoicemeeterOutputDeviceKey
        {
            get { return Get("VoicemeeterOutputDeviceKey"); }
            set { Set("VoicemeeterOutputDeviceKey", value); }
        }

        public string LanguageCode
        {
            get { return Localizer.NormalizeLanguage(Get("LanguageCode")); }
            set { Set("LanguageCode", Localizer.NormalizeLanguage(value)); }
        }

        public string GetAudioRouteTargetDeviceId(string outputDeviceId)
        {
            if (string.IsNullOrWhiteSpace(outputDeviceId))
            {
                return string.Empty;
            }

            return DecodeSettingValue(Get(GetAudioRouteKey(outputDeviceId)));
        }

        public void SetAudioRouteTargetDeviceId(string outputDeviceId, string targetInputDeviceId)
        {
            if (string.IsNullOrWhiteSpace(outputDeviceId))
            {
                return;
            }

            string key = GetAudioRouteKey(outputDeviceId);
            if (string.IsNullOrWhiteSpace(targetInputDeviceId))
            {
                Set(key, string.Empty);
            }
            else
            {
                Set(key, EncodeSettingValue(targetInputDeviceId));
            }
        }

        public bool HasUserSystemSelection
        {
            get { return !string.IsNullOrWhiteSpace(SystemOutputDeviceId) || !string.IsNullOrWhiteSpace(SystemInputDeviceId); }
        }

        private static string GetAudioRouteKey(string outputDeviceId)
        {
            return "AudioRoute." + EncodeSettingValue(outputDeviceId);
        }

        private string Get(string key)
        {
            string value;
            return _values.TryGetValue(key, out value) ? value : string.Empty;
        }

        private void Set(string key, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                _values.Remove(key);
            }
            else
            {
                _values[key] = value;
            }

            Save();
        }

        private void Load()
        {
            if (!File.Exists(_path))
            {
                return;
            }

            foreach (string line in File.ReadAllLines(_path))
            {
                int index = line.IndexOf('=');
                if (index <= 0)
                {
                    continue;
                }

                string key = line.Substring(0, index).Trim();
                string value = line.Substring(index + 1).Trim();
                if (!string.IsNullOrEmpty(key))
                {
                    _values[key] = value;
                }
            }
        }

        private void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            using (var writer = new StreamWriter(_path, false))
            {
                foreach (var pair in _values)
                {
                    writer.WriteLine(pair.Key + "=" + pair.Value);
                }
            }
        }

        private static string EncodeSettingValue(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
            return encoded.TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        private static string DecodeSettingValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string encoded = value.Trim().Replace('-', '+').Replace('_', '/');
            switch (encoded.Length % 4)
            {
                case 2:
                    encoded += "==";
                    break;
                case 3:
                    encoded += "=";
                    break;
            }

            try
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
