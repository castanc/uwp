using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.System.Diagnostics;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using WindowsGateway;
using TaskMonitor;


using System.ComponentModel;
using System.Runtime.InteropServices;
//using TaskMonitor.Controls;
//using TaskMonitor.ViewModels;
using Windows.Devices.Power;
using Windows.Networking;
using Windows.Networking.Connectivity;
using Windows.Security.ExchangeActiveSyncProvisioning;
using Windows.Storage;
using Windows.System;
using Windows.System.Profile;
using Windows.UI.ViewManagement;
using static TaskMonitor.NativeMethods;
using System.Text;



// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace ClientWindowsIntegration2
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {


        #region Fields & properties

        private const int LIST_HEIGHT_OFFSET = 200;
        private const int SCROLLBAR_WIDTH_OFFSET = 24;
        private const int DEFAULT_APPLIST_TIMER_INTERVAL = 5;
        private const int DEFAULT_PROCESSLIST_TIMER_INTERVAL = 30;
        private const string DEFAULT_PIVOT_NAME = "Processes";

        private DispatcherTimer processUpdateTimer;
        private DispatcherTimer appUpdateTimer;
        private DispatcherTimer systemUpdateTimer;
        //private ProcRowInfoCollection processes = new ProcRowInfoCollection();
        //private AppRowInfoCollection apps = new AppRowInfoCollection();
        private bool isFocusOnDetails = false;
        //private AppRowInfo detailsApp;
        //private AppProcessesTasks appProcessTasks;
        private string processSortColumn = "ExecutableFileName";
        private string appSortColumn = "Name";

        private const double GB = 1024 * 1024 * 1024;
        private const double MBPS = 1000 * 1000;
        private bool isStaticSystemInfoInitialized;
        private bool isFrozen;

        public StaticSystemInfo StaticSystemData { get; set; }
        public DynamicSystemInfo DynamicSystemData { get; set; }        //internal

        private DiagnosticAccessStatus accessStatus;
        public DiagnosticAccessStatus AccessStatus
        {
            get { return accessStatus; }
            set
            {
                if (accessStatus != value)
                {
                    accessStatus = value;
                    //NotifyPropertyChanged("AccessStatus");
                }
            }
        }

        private DateTime processLastUpdate;
        public DateTime ProcessLastUpdate
        {
            get { return processLastUpdate; }
            set
            {
                if (processLastUpdate != value)
                {
                    processLastUpdate = value;
                    //NotifyPropertyChanged("ProcessLastUpdate");
                }
            }
        }

        private DateTime appLastUpdate;
        public DateTime AppLastUpdate
        {
            get { return appLastUpdate; }
            set
            {
                if (appLastUpdate != value)
                {
                    appLastUpdate = value;
                    //NotifyPropertyChanged("AppLastUpdate");
                }
            }
        }

        private int processPollingInterval;
        public int ProcessPollingInterval
        {
            get { return processPollingInterval; }
            set
            {
                if (processPollingInterval != value)
                {
                    processPollingInterval = value;
                    //NotifyPropertyChanged("ProcessPollingInterval");
                }
            }
        }

        private int appPollingInterval;
        public int AppPollingInterval
        {
            get { return appPollingInterval; }
            set
            {
                if (appPollingInterval != value)
                {
                    appPollingInterval = value;
                    //NotifyPropertyChanged("AppPollingInterval");
                }
            }
        }

        private int systemPollingInterval;
        public int SystemPollingInterval
        {
            get { return systemPollingInterval; }
            set
            {
                if (systemPollingInterval != value)
                {
                    systemPollingInterval = value;
                    //NotifyPropertyChanged("SystemPollingInterval");
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        public async Task Launch(string url)
        {
            var uri = new Uri(url);
            var success = await Windows.System.Launcher.LaunchUriAsync(uri);

            if (success)
            {
                // URI launched
            }
            else
            {
                // URI launch failed
            }
        }
        public MainPage()
        {
            this.InitializeComponent();
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            var wg = new WindowsGateway.WindowsGateway();
            var User = await wg.GetUserInfo();
            await Launch($"https://localhost:44392/User/{User.GetRouteData()}");
        }

        //system info
        private async void Button_Click_1(object sender, RoutedEventArgs e)
        {
            await Launch($"https://localhost:44392/SystemInfo/{SystemInfo.RoutedData()}");
        }

        private async void showSystemInfo()
        {
            StaticSystemData = new StaticSystemInfo();
            DynamicSystemData = new DynamicSystemInfo();

            SYSTEM_INFO sysInfo = new SYSTEM_INFO();
            NativeMethods.GetSystemInfo(ref sysInfo);
            StaticSystemData.LogicalProcessors = sysInfo.dwNumberOfProcessors.ToString();
            StaticSystemData.Processor = $"{sysInfo.wProcessorArchitecture}, level {sysInfo.wProcessorLevel}, rev {sysInfo.wProcessorRevision}";
            StaticSystemData.PageSize = sysInfo.dwPageSize.ToString();

            var routeData = "?data=" + StaticSystemData.GetRouteData();
            await Launch($"https://localhost:44392/InternalSystemInfo/{routeData}");


        }

        private async void showMemoryInfo()
        {
            DynamicSystemData = new DynamicSystemInfo();

            #region Memory Status
            var memoryStatus = new NativeMethods.MEMORYSTATUSEX();
            var result = NativeMethods.GlobalMemoryStatusEx(memoryStatus);

            DynamicSystemData.PhysicalMemory = $"{memoryStatus.ullTotalPhys / GB:N2} GB of, {memoryStatus.ullAvailPhys / GB:N2} GB";
            DynamicSystemData.PhysicalPlusPagefile = $"{memoryStatus.ullTotalPageFile / GB:N2} GB, of {memoryStatus.ullAvailPageFile / GB:N2} GB";
            DynamicSystemData.VirtualMemory = $"{memoryStatus.ullTotalVirtual / GB:N2} GB, of {memoryStatus.ullAvailVirtual / GB:N2} GB";
            ulong pageFileOnDisk = memoryStatus.ullTotalPageFile - memoryStatus.ullTotalPhys;
            DynamicSystemData.PagefileOnDisk = $"{pageFileOnDisk / GB:N2} GB";
            DynamicSystemData.MemoryLoad = $"{memoryStatus.dwMemoryLoad} percent";
            #endregion

            var routeData = "?data=" + $"MemoryInfo*{DynamicSystemData.PhysicalMemory}*{DynamicSystemData.PhysicalPlusPagefile}*{DynamicSystemData.VirtualMemory}*{DynamicSystemData.PagefileOnDisk}*{DynamicSystemData.MemoryLoad}";
            await Launch($"https://localhost:44392/InternalSystemInfo/{routeData}");

        }

        //deep system info
        private async void Button_Click_2(object sender, RoutedEventArgs e)
        {

            StaticSystemData = new StaticSystemInfo();
            DynamicSystemData = new DynamicSystemInfo();


            SYSTEM_INFO sysInfo = new SYSTEM_INFO();
            NativeMethods.GetSystemInfo(ref sysInfo);
            StaticSystemData.LogicalProcessors = sysInfo.dwNumberOfProcessors.ToString();
            StaticSystemData.Processor = $"{sysInfo.wProcessorArchitecture}, level {sysInfo.wProcessorLevel}, rev {sysInfo.wProcessorRevision}";
            StaticSystemData.PageSize = sysInfo.dwPageSize.ToString();


            #region Memory Status
            var memoryStatus = new NativeMethods.MEMORYSTATUSEX();
            var result = NativeMethods.GlobalMemoryStatusEx(memoryStatus);

            DynamicSystemData.PhysicalMemory = $"{memoryStatus.ullTotalPhys / GB:N2} GB of, {memoryStatus.ullAvailPhys / GB:N2} GB";
            DynamicSystemData.PhysicalPlusPagefile = $"{memoryStatus.ullTotalPageFile / GB:N2} GB, of {memoryStatus.ullAvailPageFile / GB:N2} GB";
            DynamicSystemData.VirtualMemory = $"{memoryStatus.ullTotalVirtual / GB:N2} GB, of {memoryStatus.ullAvailVirtual / GB:N2} GB";
            ulong pageFileOnDisk = memoryStatus.ullTotalPageFile - memoryStatus.ullTotalPhys;
            DynamicSystemData.PagefileOnDisk = $"{pageFileOnDisk / GB:N2} GB";
            DynamicSystemData.MemoryLoad = $"{memoryStatus.dwMemoryLoad} percent";
            #endregion


            #region LogicalProcessorInformation
            //var logicalProcessorInformation = NativeMethods.GetLogicalProcessorInformation();


            bool isBatteryAvailable = true;
            try
            {
                SYSTEM_POWER_STATUS powerStatus = new SYSTEM_POWER_STATUS();
                GetSystemPowerStatus(ref powerStatus);
                DynamicSystemData.ACLineStatus = powerStatus.ACLineStatus.ToString();

                DynamicSystemData.BatteryChargeStatus = $"{powerStatus.BatteryChargeStatus:G}";
                if (powerStatus.BatteryChargeStatus == BatteryFlag.NoSystemBattery
                    || powerStatus.BatteryChargeStatus == BatteryFlag.Unknown)
                {
                    isBatteryAvailable = false;
                    DynamicSystemData.BatteryLife = "n/a";
                }
                else
                {
                    DynamicSystemData.BatteryLife = $"{powerStatus.BatteryLifePercent}%";
                }
                DynamicSystemData.BatterySaver = powerStatus.BatterySaver.ToString();
            }
            catch (Exception ex)
            {
                //App.AnalyticsWriteLine("MainPage.UpdateDynamicSystemData", "SYSTEM_POWER_STATUS", ex.Message);
            }

            if (isBatteryAvailable)
            {
                try
                {
                    Battery battery = Battery.AggregateBattery;
                    BatteryReport batteryReport = battery.GetReport();
                    DynamicSystemData.ChargeRate = $"{batteryReport.ChargeRateInMilliwatts:N0} mW";
                    DynamicSystemData.Capacity =
                        $"design = {batteryReport.DesignCapacityInMilliwattHours:N0} mWh, " +
                        $"full = {batteryReport.FullChargeCapacityInMilliwattHours:N0} mWh, " +
                        $"remaining = {batteryReport.RemainingCapacityInMilliwattHours:N0} mWh";
                }
                catch (Exception ex)
                {
                    //App.AnalyticsWriteLine("MainPage.UpdateDynamicSystemData", "BatteryReport", ex.Message);
                }
            }
            else
            {
                DynamicSystemData.ChargeRate = "n/a";
                DynamicSystemData.Capacity = "n/a";
            }
            #endregion

            #region FreeDiskSpace
            try
            {
                ulong freeBytesAvailable;
                ulong totalNumberOfBytes;
                ulong totalNumberOfFreeBytes;

                // You can only specify a folder path that this app can access, but you can
                // get full disk information from any folder path.
                IStorageFolder appFolder = ApplicationData.Current.LocalFolder;
                GetDiskFreeSpaceEx(appFolder.Path, out freeBytesAvailable, out totalNumberOfBytes, out totalNumberOfFreeBytes);
                DynamicSystemData.TotalDiskSize = $"{totalNumberOfBytes / GB:N2} GB";
                DynamicSystemData.DiskFreeSpace = $"{freeBytesAvailable / GB:N2} GB";
            }
            catch (Exception ex)
            {
                //App.AnalyticsWriteLine("MainPage.UpdateDynamicSystemData", "GetDiskFreeSpaceEx", ex.Message);
            }
            #endregion

            #region Network Parameters

            try
            {
                IntPtr infoPtr = IntPtr.Zero;
                uint infoLen = (uint)Marshal.SizeOf<FIXED_INFO>();
                int ret = -1;

                while (ret != ERROR_SUCCESS)
                {
                    infoPtr = Marshal.AllocHGlobal(Convert.ToInt32(infoLen));
                    ret = GetNetworkParams(infoPtr, ref infoLen);
                    if (ret == ERROR_BUFFER_OVERFLOW)
                    {
                        // Try again with a bigger buffer.
                        Marshal.FreeHGlobal(infoPtr);
                        continue; 
                    }
                }

                FIXED_INFO info = Marshal.PtrToStructure<FIXED_INFO>(infoPtr);
                DynamicSystemData.DomainName = info.DomainName;

                string nodeType = string.Empty;
                switch (info.NodeType)
                {
                    case BROADCAST_NODETYPE:
                        nodeType = "Broadcast";
                        break;
                    case PEER_TO_PEER_NODETYPE:
                        nodeType = "Peer to Peer";
                        break;
                    case MIXED_NODETYPE:
                        nodeType = "Mixed";
                        break;
                    case HYBRID_NODETYPE:
                        nodeType = "Hybrid";
                        break;
                    default:
                        nodeType = $"Unknown ({info.NodeType})";
                        break;
                }
                DynamicSystemData.NodeType = nodeType;
            }
            catch (Exception ex)
            {
                //App.AnalyticsWriteLine("MainPage.UpdateDynamicSystemData", "GetNetworkParams", ex.Message);
            }

            try
            {
                ConnectionProfile profile = NetworkInformation.GetInternetConnectionProfile();
                DynamicSystemData.ConnectedProfile = profile.ProfileName;

                NetworkAdapter internetAdapter = profile.NetworkAdapter;
                DynamicSystemData.IanaInterfaceType = $"{(IanaInterfaceType)internetAdapter.IanaInterfaceType}";
                DynamicSystemData.InboundSpeed = $"{internetAdapter.InboundMaxBitsPerSecond / MBPS:N0} Mbps";
                DynamicSystemData.OutboundSpeed = $"{internetAdapter.OutboundMaxBitsPerSecond / MBPS:N0} Mbps";

                IReadOnlyList<HostName> hostNames = NetworkInformation.GetHostNames();
                HostName connectedHost = hostNames.Where
                    (h => h.IPInformation != null
                    && h.IPInformation.NetworkAdapter != null
                    && h.IPInformation.NetworkAdapter.NetworkAdapterId == internetAdapter.NetworkAdapterId)
                    .FirstOrDefault();
                if (connectedHost != null)
                {
                    DynamicSystemData.HostAddress = connectedHost.CanonicalName;
                    DynamicSystemData.AddressType = connectedHost.Type.ToString();
                }
                StringBuilder sb = new StringBuilder();
                foreach(var hn in hostNames)
                {
                    sb.Append(hn.DisplayName + ",");
                }

                DynamicSystemData.HostNames = sb.ToString();
            }
            catch (Exception ex)
            {
                //App.AnalyticsWriteLine("MainPage.UpdateDynamicSystemData", "GetInternetConnectionProfile", ex.Message);
            }
            #endregion

            var routeData = "?data=All_" + StaticSystemData.GetRouteData() + "*" + 
                DynamicSystemData.GetRouteData();
            routeData = routeData.Replace("/", "");
            await Launch($"https://localhost:44392/InternalSystemInfo/{routeData}");
        }

        private void btnMemoryInfo_Click(object sender, RoutedEventArgs e)
        {
            showMemoryInfo();
        }
    }
}
