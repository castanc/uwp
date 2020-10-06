using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Windows.ApplicationModel;
using Windows.Graphics.Display;
using Windows.Security.ExchangeActiveSyncProvisioning;
using Windows.System.Profile;

namespace WindowsGateway
{
    public static class SystemInfo
    {
        public static string SystemFamily { get; }
        public static string SystemVersion { get; }
        public static string SystemArchitecture { get; }
        public static string ApplicationName { get; }
        public static string ApplicationVersion { get; }
        public static string DeviceManufacturer { get; }
        public static string DeviceModel { get; }

        static SystemInfo()
        {
            // get the system family name
            AnalyticsVersionInfo ai = AnalyticsInfo.VersionInfo;
            SystemFamily = ai.DeviceFamily;

            // get the system version number
            string sv = AnalyticsInfo.VersionInfo.DeviceFamilyVersion;
            ulong v = ulong.Parse(sv);
            ulong v1 = (v & 0xFFFF000000000000L) >> 48;
            ulong v2 = (v & 0x0000FFFF00000000L) >> 32;
            ulong v3 = (v & 0x00000000FFFF0000L) >> 16;
            ulong v4 = (v & 0x000000000000FFFFL);
            SystemVersion = $"{v1}.{v2}.{v3}.{v4}";

            // get the package architecure
            Package package = Package.Current;
            SystemArchitecture = package.Id.Architecture.ToString();

            // get the user friendly app name
            ApplicationName = package.DisplayName;

            // get the app version
            PackageVersion pv = package.Id.Version;
            ApplicationVersion = $"{pv.Major}.{pv.Minor}.{pv.Build}.{pv.Revision}";

            // get the device manufacturer and model name
            EasClientDeviceInformation eas = new EasClientDeviceInformation();
            DeviceManufacturer = eas.SystemManufacturer;
            DeviceModel = eas.SystemProductName;
        }

        public static string SystemInfoString()
        {
            string info = $@"{nameof(ApplicationName)} {ApplicationName}
{nameof(ApplicationVersion)} {ApplicationVersion}
{nameof(DeviceManufacturer)}{DeviceManufacturer}
{nameof(DeviceModel)} {DeviceModel}
{nameof(SystemArchitecture)} {SystemArchitecture}
{nameof(SystemFamily)} {SystemFamily}
{nameof(SystemVersion)} {SystemVersion}";
            return info;


        }

        public static string RoutedData()
        {
            return $@"?data={ApplicationName}_{ApplicationVersion}_{DeviceManufacturer}_{DeviceModel}_{SystemArchitecture}_{SystemFamily}_{SystemVersion}";
        }
    }
}
