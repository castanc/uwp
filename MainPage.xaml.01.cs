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


// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace Deskhelp_UWP
{


    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        /// <summary>
        /// Publickey of notification hub registration
        /// This variable should be set in the Operating System Environment Variable: DESKHELPKEY
        /// </summary>
        public const string PublicKey = "BB5nzfoUKxl3DRazM1PD59LxWstFfeffstlyXA5RTT5XMGFD1gMa5xB3MGxHE0t3armwLPSblkjmjATWkV6KEvA";
        public static DeskHelpParameters dhp { set; get; }



        public static string GetPublicKey()
        {
            return PublicKey;
        }
        /// <summary>
        /// Initializes the Main Page
        /// Set up a handler for the Main Wndow rezise event
        /// </summary>
        public MainPage()
        {
            this.InitializeComponent();

            /// Set up a handler for the Main Wndow rezise event
            Window.Current.CoreWindow.SizeChanged += async (ss, ee) =>
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
                    var subscription = await PushManager.SubscribeAsync(dhp.PublicKey, "myChannel1");
                    _subscriptionJson = subscription.ToJson();                                      
                    var applicationData = Windows.Storage.ApplicationData.Current;
                    var localSettings = applicationData.LocalSettings;
                    localSettings.Values["WNSChannelURI"] = _subscriptionJson.ToString();
                    localSettings.Values["WNSChannelRegistrationId"] = _subscriptionJson.ToString(); 
                    return true;
                }
                else
                {
                    return false;   
                }
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        /// <summary>
        /// Gets user info from localstorage
        /// </summary>
        public static  String UserInfo()
        {
            var uinfo = System.Security.Principal.WindowsIdentity.GetCurrent();
            string UserInfo = null;
            try
            {
                var applicationData = Windows.Storage.ApplicationData.Current;
                var localSettings = applicationData.LocalSettings;
                UserInfo = (string) localSettings.Values["UserInfo"]; 
                if(UserInfo == "" || UserInfo == null)
                {
                    string username = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
                    localSettings.Values["UserInfo"] = UserInfo;
                    return username;
                }
                else
                {
                    return UserInfo;
                }
            }
            catch (Exception ex)
            {
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
                    return devieinfo;
                }
                else
                {
                    return DeviceInfo;
                }
            }
            catch (Exception ex)
            {
                //throw ex;
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


                if (registrationId == null || registrationId == ""  )
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


                            if (registrationId != _subscriptionJsonCheck)
                            {
                                localSettings.Values["WNSChannelRegistrationId"] = _subscriptionJson;
                                localSettings.Values["WNSChannelRegistrationIdCheck"] = true;
                            }
                            else
                                localSettings.Values["WNSChannelRegistrationIdCheck"] = !true;
                        }
                        else
                            localSettings.Values["WNSChannelRegistrationIdCheck"] = !true;
                    }
                }
                  
                return true;
            }
            catch (Exception ex)
            {
                return true;
            }
        }


        /// <summary>
        /// This function is called once all the page elements are loaded and ready.
        /// </summary>
        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            var applicationData = Windows.Storage.ApplicationData.Current;
            var localSettings = applicationData.LocalSettings;
            var FirstLogin = localSettings.Values["FirstLogin"];

            if ( (FirstLogin == null) ||  FirstLogin == ""  )
            {
                UserInfo();
                DeviceInfo();
            }
            //It will call always on launch
            await ReSubscription();
            renderPWAApp();
        }
        
        /// <summary>
        /// Renders the PWA loading its url in the webview
        /// </summary>
        private void renderPWAApp()
        {
            string url = "http://localhost:3000/";
            //url =  "https://deskhelptest.azurewebsites.net/ui/";
            App.WebView = this.mywebview;
            this.mywebview.ScriptNotify += MyWebView_ScriptNotifyAsync;
            //await WebView.ClearTemporaryWebDataAsync();
            if(App.Redirect)
            {
                url = App.RedirectURL; 
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
            App.Appload = true;
        }

        /// <summary>
        /// Handler method for PWA Javascript requests to the parent host ( this app)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        async void MyWebView_ScriptNotifyAsync(object sender, NotifyEventArgs e)
        {
            if (e.Value == "subscribed" )
            {
                bool isSubscribed =  await SetUpSubscription();
                if(isSubscribed)
                {
                    await this.mywebview.InvokeScriptAsync("isSubscribed", new string[] { "true" });
                } else
                {
                    await this.mywebview.InvokeScriptAsync("isSubscribed", new string[] { "false" });
                }
                
            }
            if (e.Value == "UserInfo")
            {
                var UserInfoCheck = UserInfo(); 
                if (UserInfoCheck != "" || UserInfoCheck != null)
                {
                    await this.mywebview.InvokeScriptAsync("UserInfo", new string[] { "true" });
                }
                else
                {
                    await this.mywebview.InvokeScriptAsync("UserInfo", new string[] { "false" });
                }

            }
            if (e.Value == "DeviceInfo")
            {
                var DeviceInfoCheck = DeviceInfo();
                if (DeviceInfoCheck != "" || DeviceInfoCheck != null)
                {
                    await this.mywebview.InvokeScriptAsync("DeviceInfo", new string[] { "true" });
                }
                else
                {
                    await this.mywebview.InvokeScriptAsync("DeviceInfo", new string[] { "false" });
                }

            }
            if(e.Value == "getLocalStorageValue")
            {
                bool isSubscribed = await SetUpSubscription(); 
                var DeviceInfoCheck = DeviceInfo();
                var UserInfoCheck = UserInfo();
            }
        }

    }
}
