using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.WindowsAzure.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Networking;
using Windows.Networking.PushNotifications;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;


// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace Deskhelp
{


    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public static DeskHelpParameters dhp { set; get; }
        public static Microsoft.ApplicationInsights.TelemetryClient telemetry;
        public static bool TelemetryEnabled = false;


        /// <summary>
        /// Initializes the Main Page
        /// Set up a handler for the Main Wndow rezise event
        /// </summary>
        public MainPage()
        {
            try
            {
                this.InitializeComponent();

                /// Set up a handler for the Main Wndow rezise event
                ///TODO:  Window.Current.CoreWindow.SizeChanged += async (ss, ee) =>      //async removed
                Window.Current.CoreWindow.SizeChanged += (ss, ee) =>
                {
                    var appView = ApplicationView.GetForCurrentView();
                    appView.TryResizeView(new Size(500, 620));
                    ee.Handled = true;
                };
            }
            catch (Exception ex)
            {
                App.LogException(ex, "MainPage() Initialziation");
            }

        }

        /// <summary>
        /// Gets subscriptionId from Azure ntoificationhub
        /// </summary>
        /// <param name="_deviceInfo"></param>
        /// <returns></returns>
        //private async Task<string> GetSubscriptionFromNotificationHub(string _deviceInfo)
        //{
        //    var channel = await PushNotificationChannelManager.CreatePushNotificationChannelForApplicationAsync();
        //    //channel.PushNotificationReceived += Channel_PushNotificationReceived;
        //    var hub = new NotificationHub(dhp.NotificationHubName, dhp.NotificationHubURL);

        //    //List<string> tags = new List<string>
        //    //{
        //    //    $"@{_deviceInfo}"
        //    //};
        //    var tags = new List<string>();
        //    tags.Add(_deviceInfo);
        //    var result = await hub.RegisterNativeAsync(channel.Uri, tags);
        //    return result?.RegistrationId;
        //}
        ///// <summary>
        /// Set Up the subscription to the Message Hub
        /// </summary>
        private async Task<bool> SetUpSubscription()
        {
            try
            {
                var applicationData = Windows.Storage.ApplicationData.Current;
                var localSettings = applicationData.LocalSettings;
                var _deviceInfo = (string)localSettings.Values["DeviceInfo"];
                App.DeviceInfoString = _deviceInfo;

                //
                var channel = await PushNotificationChannelManager.CreatePushNotificationChannelForApplicationAsync();
                var hub = new NotificationHub(dhp.NotificationHubName, dhp.NotificationHubURL);
                var tags = new List<string>();

                tags.Add("@" + _deviceInfo);
                var result = await hub.RegisterNativeAsync(channel.Uri, tags);

                string _registrationId = "";
                if (result.RegistrationId != null)
                {
                    _registrationId = result.RegistrationId;
                }
                //
                //string _registrationId = await GetSubscriptionFromNotificationHub(_deviceInfo);
                if (!string.IsNullOrEmpty(_registrationId))
                {
                    localSettings.Values["WNSChannelRegistrationId"] = _registrationId;
                    App.LogTelemetry("Request for SetUpSubscription()", SeverityLevel.Information, App.BuildProperties($"RegistrationId\t{_registrationId}"));
                    return true;
                }
            }
            catch (Exception ex)
            {
                App.LogException(ex, "MainPage.SetUpSubscription()");
            }
            return false;
        }
        /// <summary>
        /// Gets user info from localstorage
        /// </summary>
        public static String UserInfo()
        {
            string UserInfo = null;
            try
            {
                var applicationData = Windows.Storage.ApplicationData.Current;
                var localSettings = applicationData.LocalSettings;
                UserInfo = (string)localSettings.Values["UserInfo"];
                if (UserInfo == null || UserInfo == "")
                {
                    string username = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
                    localSettings.Values["UserInfo"] = UserInfo;
                    App.LogTelemetry("Request for UserInfo()", SeverityLevel.Information, App.BuildProperties($"UserInfo\t{username}"));
                    return username;
                }
                else
                {
                    App.LogTelemetry("Request for UserInfo()", SeverityLevel.Information, App.BuildProperties($"UserInfo\t{UserInfo}"));
                    return UserInfo;
                }
            }
            catch (Exception ex)
            {
                App.LogException(ex, "MainPage.UserInfo()");
                return UserInfo;
            }

        }



        /// <summary>
        /// Gets device info from localstorage
        /// </summary>
        private String DeviceInfo()
        {
            string DeviceInfo = null;
            try
            {
                var applicationData = Windows.Storage.ApplicationData.Current;
                var localSettings = applicationData.LocalSettings;
                DeviceInfo = (string)localSettings.Values["DeviceInfo"];
                if (DeviceInfo == "" || DeviceInfo == null)
                {
                    var hostNames = Windows.Networking.Connectivity.NetworkInformation.GetHostNames();
                    var devieinfo = hostNames.FirstOrDefault(name => name.Type == HostNameType.DomainName)?.DisplayName ?? "null";// System.Security.Principal.WindowsIdentity.GetCurrent().Name;
                    localSettings.Values["DeviceInfo"] = devieinfo;
                    App.LogTelemetry("Request for DeviceInfo()", SeverityLevel.Information, App.BuildProperties($"DeviceInfo\t{devieinfo}"));
                    return devieinfo;
                }
                else
                {
                    App.LogTelemetry("Request for DeviceInfo()", SeverityLevel.Information, App.BuildProperties($"DeviceInfo\t{DeviceInfo}"));
                    return DeviceInfo;
                }
            }
            catch (Exception ex)
            {
                App.LogException(ex, "MainPage.DeviceInfo()");
                return DeviceInfo;
            }
        }


        /// <summary>
        /// This function is called once all the page elements are loaded and ready.
        /// </summary>
        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var applicationData = Windows.Storage.ApplicationData.Current;
                var localSettings = applicationData.LocalSettings;
                var FirstLogin = localSettings.Values["FirstLogin"];

                //if ((FirstLogin == null) || FirstLogin == "")
                if ((FirstLogin == null) || FirstLogin == "")
                {
                    App.LogTelemetry("Request for Page_Loaded() for First Login", SeverityLevel.Information, App.BuildProperties($"FirstLogin\t{"true"}"));
                    localSettings.Values["UWPUninstallCheck"] = true;
                    UserInfo();
                    DeviceInfo();
                }
                else
                    localSettings.Values["UWPUninstallCheck"] = !true;//App.LogTelemetry("Request for Page_Loaded() for First Login", SeverityLevel.Information, App.BuildProperties($"FirstLogin\t{FirstLogin}"));
                renderPWAApp();
            }
            catch (Exception ex)
            {
                App.LogException(ex, "Page_Loaded()");
            }
        }

        /// <summary>
        /// Renders the PWA loading its url in the webview
        /// </summary>
        private void renderPWAApp()
        {
            try
            {
                string url = dhp.RenderUrl; // 
                                            //url = "http://localhost:3000/";
                                            //url =  "https://deskhelptest.azurewebsites.net/ui/";
                App.WebView = this.mywebview;
                this.mywebview.ScriptNotify += MyWebView_ScriptNotifyAsync;
                //await WebView.ClearTemporaryWebDataAsync();
                App.LogTelemetry("Request for renderPWAApp()", SeverityLevel.Information, App.BuildProperties($"Url\t{url}"));
                if (App.Redirect)
                {
                    //url = App.RedirectURL; 
                    var applicationData = Windows.Storage.ApplicationData.Current;
                    var localSettings = applicationData.LocalSettings;
                    localSettings.Values["RedirectURL"] = App.RedirectURL;
                    this.mywebview.Navigate(new Uri(@url));
                }
                else
                {
                    this.mywebview.Navigate(new Uri(@url));
                }

                this.mywebview.DOMContentLoaded += DOMContentLoaded;
            }
            catch (Exception ex)
            {
                App.LogException(ex, "renderPWA()");
            }
        }

        /// <summary>
        /// Executes once all the DOM Content laoded in the webview
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void DOMContentLoaded(WebView sender, WebViewDOMContentLoadedEventArgs args)
        {
            try
            {
                App.LogTelemetry("Request for DOMContentLoaded()");
                App.Appload = true;
            }
            catch (Exception ex)
            {
                App.LogException(ex, "DOMCOntentLoaded()");
            }
        }

        /// <summary>
        /// Handler method for PWA Javascript requests to the parent host ( this app)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        async void MyWebView_ScriptNotifyAsync(object sender, NotifyEventArgs e)
        {
            try
            {

                App.LogTelemetry("Request for MyWebView_ScriptNotifyAsync()", SeverityLevel.Information, App.BuildProperties($"Value\t{e.Value}"));
                if (e.Value == "subscribed")
                {
                    bool isSubscribed = await SetUpSubscription();
                    if (isSubscribed)
                        await this.mywebview.InvokeScriptAsync("isSubscribed", new string[] { "true" });
                    else
                        await this.mywebview.InvokeScriptAsync("isSubscribed", new string[] { "false" });

                }
                if (e.Value == "UserInfo")
                {
                    var UserInfoCheck = UserInfo();
                    if (UserInfoCheck != "" || UserInfoCheck != null)
                        await this.mywebview.InvokeScriptAsync("UserInfo", new string[] { "true" });
                    else
                        await this.mywebview.InvokeScriptAsync("UserInfo", new string[] { "false" });
                }
                if (e.Value == "DeviceInfo")
                {
                    var DeviceInfoCheck = DeviceInfo();
                    if (DeviceInfoCheck != "" || DeviceInfoCheck != null)
                        await this.mywebview.InvokeScriptAsync("DeviceInfo", new string[] { "true" });
                    else
                        await this.mywebview.InvokeScriptAsync("DeviceInfo", new string[] { "false" });
                }
            }
            catch (Exception ex)
            {
                App.LogException(ex, "MyWebView_ScriptNotifyAsync function Exception:" + e.Value);
            }
        }
    }
}
