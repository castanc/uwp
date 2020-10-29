using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using AlternatePushChannel.Library;
using System.Threading.Tasks;
using Windows.System.Threading;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.Networking;
using Windows.Data.Json;
using Windows.UI.Popups;
using System.Security.Cryptography.X509Certificates;
using System.Net.Http;
using Microsoft.ApplicationInsights.DataContracts;


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
            this.InitializeComponent();

            /// Set up a handler for the Main Wndow rezise event
            ///TODO:  Window.Current.CoreWindow.SizeChanged += async (ss, ee) =>      //async removed
            Window.Current.CoreWindow.SizeChanged += (ss, ee) =>
            {
                var appView = ApplicationView.GetForCurrentView();
                appView.TryResizeView(new Size(500, 600));
                ee.Handled = true;
            };
            
        }

        //Holds the json formatted string for the subscription
        private string _subscriptionJson;


        /// <summary>
        /// Set Up the subscription to the Message Hub
        /// </summary>
        private async Task<bool> SetUpSubscription()
        {
            try
            {
                if (PushManager.IsSupported)
                {
                    var applicationData = Windows.Storage.ApplicationData.Current;
                    var localSettings = applicationData.LocalSettings;
                    var DeviceInfo = (string)localSettings.Values["DeviceInfo"];
                    var subscription = await PushManager.SubscribeAsync(dhp.PublicKey, DeviceInfo);
                    _subscriptionJson = subscription.ToJson();                                      
                    localSettings.Values["WNSChannelRegistrationId"] = _subscriptionJson.ToString();
                    App.LogTelemetry("Request for SetUpSubscription()", SeverityLevel.Information, App.BuildProperties($"SubscriptionJson\t{_subscriptionJson}"));
                    return true;
                }
                else
                {
                    App.LogTelemetry("Request for SetUpSubscription() is not suported.", SeverityLevel.Information );
                    return false;   
                }
            }
            catch (Exception ex)
            {
                App.LogException(ex, "EX0008 MainPage.SetUpSubscription()");
                return false;
            }
        }

        /// <summary>
        /// Gets user info from localstorage
        /// </summary>
        public static  String UserInfo()
        {
            string UserInfo = null;
            try
            {
                var applicationData = Windows.Storage.ApplicationData.Current;
                var localSettings = applicationData.LocalSettings;
                UserInfo = (string)localSettings.Values["UserInfo"];
                if(UserInfo == null || UserInfo == ""  )
                {
                    string username = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
                    localSettings.Values["UserInfo"] = UserInfo;
                    App.LogTelemetry("Request for UserInfo()", SeverityLevel.Information, App.BuildProperties($"UserInfo\t{username}"));
                    return username;
                }
                else
                {
                    App.LogTelemetry("Request for UserInfo() is not suported.", SeverityLevel.Information);
                    return UserInfo;
                }
            }
            catch (Exception ex)
            {
                App.LogException(ex, "EX0009 MainPage.UserInfo()");
                return UserInfo;
            }
            
        }


        public static String UserGUID()
        {
            string UserInfo = null;
            try
            {
                var applicationData = Windows.Storage.ApplicationData.Current;
                var localSettings = applicationData.LocalSettings;
                UserInfo = (string)localSettings.Values["userID"];
                if (UserInfo == "" || UserInfo == null)
                {
                    App.LogTelemetry("Request for UserGUID() not able to get from PWA.", SeverityLevel.Information);
                    return null;
                }
                else
                {
                    return UserInfo;
                }
            }
            catch (Exception ex)
            {
                App.LogException(ex,"EX0010 MainPage.UserGUID()");
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
                    return DeviceInfo;
                }
            }
            catch (Exception ex)
            {
                App.LogException(ex,"EX0011 MainPage.DeviceInfo()");
                return DeviceInfo;
            }
        }


        /// <summary>
        /// Resubscribes to the service whenever the registration is invalid ( Expiration date is due)
        /// </summary>
        private async Task<bool> ReSubscription()
        {
            try
            {
                var applicationData = Windows.Storage.ApplicationData.Current;
                var localSettings = applicationData.LocalSettings;
                var registrationId = localSettings.Values["WNSChannelRegistrationId"];
                DateTime regDateObject = new DateTime(1, 1, 1);

                //TODO:changed for warning removal
                //if (registrationId == null || registrationId == ""  )
                if (registrationId == null || (string) registrationId == "")
                {
                    localSettings.Values["UWPUninstallCheck"] = true;
                }
                else
                {
                    if (PushManager.IsSupported)
                    {
                        var regJson = registrationId.ToString();
                        var regObject = JsonObject.Parse(regJson);
                        string dateReg = regObject["expirationTime"].ToString().Replace("{", "").Replace("}", "").Replace("\"", "");

                        regDateObject = Convert.ToDateTime(dateReg);
                        if (regDateObject < DateTime.Now)
                        {
                            var subscription = await PushManager.SubscribeAsync(dhp.PublicKey, "myChannel1");
                            _subscriptionJson = subscription.ToJson();
                            var _subscriptionJsonCheck = _subscriptionJson.ToString();
                            App.LogTelemetry("ReSubscription() Subscription was renewed");
                            localSettings.Values["WNSChannelRegistrationId"] = _subscriptionJson;
                            localSettings.Values["WNSChannelRegistrationIdCheck"] = true;
                        }
                        else
                        {
                            localSettings.Values["WNSChannelRegistrationIdCheck"] = !       true;
                        }
                            
                    }
                }
                  
                return true;
            }
            catch (Exception ex)
            {
                App.LogException(ex, "EX0014 MainPage.ReSubscription()");
                return true;
            }
        }


        /// <summary>
        /// This function is called once all the page elements are loaded and ready.
        /// </summary>
        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var applicationData = Windows.Storage.ApplicationData.Current;
                var localSettings = applicationData.LocalSettings;
                var FirstLogin = localSettings.Values["FirstLogin"];
                var UWPUninstallCheck = localSettings.Values["UWPUninstallCheck"];

                //TODO: Warnign removal
                //if ((FirstLogin == null) || FirstLogin == "")
                if ((FirstLogin == null) || (string) FirstLogin == "")
                {
                    UserInfo();
                    DeviceInfo();

                }
                else
                {
                    await ReSubscription();
                }

                //todo: warning removal
                //if ((UWPUninstallCheck == null) || UWPUninstallCheck == "")
                if ((UWPUninstallCheck == null) || (string)  UWPUninstallCheck == "")
                {
                    localSettings.Values["UWPUninstallCheck"] = true;
                }

                //It will call always on launch
                //await ReSubscription();
                renderPWAApp();
            }
            catch(Exception ex)
            {
                App.LogException(ex, "EX0013 Page_Loaded()");
            }
        }
        
        /// <summary>
        /// Renders the PWA loading its url in the webview
        /// </summary>
        private void renderPWAApp()
        {
            string url = dhp.RenderUrl; // 
            //url = "http://localhost:3000/";
            //url =  "https://deskhelptest.azurewebsites.net/ui/";
            App.WebView = this.mywebview;
            this.mywebview.ScriptNotify += MyWebView_ScriptNotifyAsync;
            //await WebView.ClearTemporaryWebDataAsync();
            App.LogTelemetry("renderPWAApp()", SeverityLevel.Information, App.BuildProperties($"Url\t{url}"));
            if (App.Redirect)
            {
                //url = App.RedirectURL; 
                var applicationData = Windows.Storage.ApplicationData.Current;
                var localSettings = applicationData.LocalSettings;
                localSettings.Values["RedirectURL"]= App.RedirectURL;
                this.mywebview.Navigate(new Uri(@url));
            }
            else 
            {
                this.mywebview.Navigate(new Uri(@url));
            }
            
            this.mywebview.DOMContentLoaded += DOMContentLoaded;
        }

        /// <summary>
        /// Executes once all the DOM Content laoded in the webview
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void DOMContentLoaded(WebView sender, WebViewDOMContentLoadedEventArgs args)
        {
            App.LogTelemetry("DOMContentLoaded()");
            App.Appload = true;
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

                App.LogTelemetry("MyWebView_ScriptNotifyAsync()", SeverityLevel.Information, App.BuildProperties($"Value\t{e.Value}"));
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
                if (e.Value == "getLocalStorageValue")
                {
                    App.LogTelemetry("Get local storage value called from UI (getLcoalStorageValue())");
                    bool isSubscribed = await SetUpSubscription();
                    var applicationData = Windows.Storage.ApplicationData.Current;
                    var localSettings = applicationData.LocalSettings;
                    localSettings.Values["FirstLogin"] = true;

                    var DeviceInfoCheck = DeviceInfo();
                    var UserInfoCheck = UserInfo();
                    if (isSubscribed)
                    {
                        var RenewSubscription = await App.RenewSubscription();
                        if (RenewSubscription == "")
                        {
                            localSettings.Values["UWPUninstallCheck"] = !true; 
                            App.LogTelemetry("MyWebView_ScriptNotifyAsync() calling RenewSubscription() is success");
                        }
                        else
                        {
                            localSettings.Values["UWPUninstallCheck"] = true;
                            App.LogTelemetry("MyWebView_ScriptNotifyAsync() calling RenewSubscription() is error");
                        } 
                    }
                    else
                    {
                        localSettings.Values["UWPUninstallCheck"] = true;
                        App.LogTelemetry("MyWebView_ScriptNotifyAsync() calling RenewSubscription() is isSubscribed: " + isSubscribed);
                    }
                }
            }
            catch(Exception ex)
            {
                App.LogException(ex, "MyWebView_ScriptNotifyAsync function Exception:"+ e.Value);
            }

        }  

    }
}
