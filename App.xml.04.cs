using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Linq;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Background;
using Windows.Networking.PushNotifications;
using Windows.UI.Notifications;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Data.Json;
using Microsoft.QueryStringDotNET;
using System.Threading.Tasks;
using System.Collections;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Dynamic;
using Windows.Storage;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;

namespace Deskhelp
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    /// 

    //azure repository:
    //

    //ApplicationInsights Traces
    //https://portal.azure.com/#@TCSHiTech15.onmicrosoft.com/resource/subscriptions/b65c35f3-1c9c-4e66-b1fe-5cd91a96f1da/resourceGroups/WorkspaceOne/providers/microsoft.insights/components/deskhelptest/logs

    sealed partial class App : Application
    {
        public static WebView WebView;
        public static Boolean Appload = false;
        public static Boolean Redirect = false;
        public static string RedirectURL = "";
        public DateTime ExpirationDate;
        public DateTime LastExpirationDateVerification = new DateTime(1, 1, 1);
        private static DeskHelpParameters dhp = new DeskHelpParameters();
        public static string DeviceInfoString = "";
        //private HttpResponseMessage httpResponse;

        #region ApplicationInsights Telemetry

        public static Microsoft.ApplicationInsights.TelemetryClient telemetry;
        public static bool TelemetryEnabled = false;

        public static Dictionary<string,string> BuildProperties(string text)
        {
            var prop = new Dictionary<string, string>();
            var lines = text.Split('\n');
            foreach(string l in lines)
            {
                var line = l.Split('\t');
                if (line.Length >= 2)
                    prop.Add(line[0], line[1]);
            }
            return prop;
        }

        public static Dictionary<string,double> BuildMetrics(string text)
        {
            Dictionary<string, double> metrics = new Dictionary<string, double>();
            string lastValue = "";
            var lines = text.Split('\n');
            foreach(string l in lines)
            {
                var line = l.Split('\t');
                if ( line.Length >= 2 )
                {
                    try
                    {
                        lastValue = line[1];
                        metrics.Add(line[0], Convert.ToDouble(line[1]));
                    }
                    catch(Exception ex)
                    {
                        //LogException(ex, BuildProperties("BuildMetrics()\t76"));
                        LogException(ex);
                    }
                    
                }
            }

            return metrics;
        }
            


        /// <summary>
        /// Initialize telemetry for ApplicationInsights in Azure
        /// </summary>
        public async static void InitializeTelemetry()
        {
            try
            {
                await Microsoft.ApplicationInsights.WindowsAppInitializer.InitializeAsync(
                    WindowsCollectors.Metadata | WindowsCollectors.Session | WindowsCollectors.UnhandledException);

                telemetry = new Microsoft.ApplicationInsights.TelemetryClient();
                telemetry.InstrumentationKey = dhp.AppInsightsKey;
                TelemetryEnabled = telemetry.IsEnabled();
            }
            catch(Exception ex )
            {
                if (TelemetryEnabled)
                    LogException(ex, "EX0015 InitialilzeTelemetry()");
            }
        }

        public static void LogTelemetry(string text,   SeverityLevel severity = SeverityLevel.Information, Dictionary<string,string> properties = null )
        {
            if (TelemetryEnabled)
            {
                telemetry.TrackTrace($"UWP::{text}", severity, properties);
                telemetry.Flush();

            }
        }

        public static void LogException(Exception ex, string method = "", Dictionary<string,string> properties = null , Dictionary<string,double> metrics = null)
        {
            if (TelemetryEnabled)
            {
                if ( !string.IsNullOrEmpty(method))
                    LogTelemetry($"Device Info:[{DeviceInfoString}] EXCEPTION at {method}", SeverityLevel.Critical, BuildProperties($"Error Message\t{ex.Message}\nStack Trace\t{ex.StackTrace}"));
                telemetry.TrackException(ex, properties, metrics);
                telemetry.Flush();
            }
        }
        #endregion

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            try
            {
                this.InitializeComponent();
                InitializeTelemetry();
                Application.Current.UnhandledException += Current_UnhandledException;
                MainPage.dhp = dhp;
                this.Suspending += OnSuspending;
                RegisterPushBackgroundTask();
                
            }
            catch(Exception ex)
            {
                LogException(ex, "EX0016 App constructor initialization");
            }
        }

        private void Current_UnhandledException(object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            LogException(e.Exception, "EX0001 App Unhandled exception");
        }


        /// <summary>
        /// Registers all the required background tasks
        /// This should be done once
        /// <summary>c
        private void RegisterPushBackgroundTask()
        {
            const string PushBackgroundTaskName = "DeskhelpBackgroundNotification";
            const string timerRegisterTask = "DeskHelpBackgroundDailyValidation";
            const string startupTask = "DeskHelpBackgroundStartupValidation";


            try
            {

                //If deactivation of all tasks is required, uncomment this code
                //todo: leave commented for release
                //foreach (var task in BackgroundTaskRegistration.AllTasks)
                //     task.Value.Unregister(true);

                if (!BackgroundTaskRegistration.AllTasks.Any(i => i.Value.Name == PushBackgroundTaskName))
                {
                    var builder = new BackgroundTaskBuilder();
                    builder.Name = PushBackgroundTaskName;
                    builder.SetTrigger(new PushNotificationTrigger());
                    builder.Register();
                    LogTelemetry("Register BGTask PushBackgroundTask");
                }

                if (!BackgroundTaskRegistration.AllTasks.Any(i => i.Value.Name == timerRegisterTask))
                {
                    var builder = new BackgroundTaskBuilder();
                    builder.Name = timerRegisterTask;

                    SystemCondition internetCondition = new SystemCondition(SystemConditionType.InternetAvailable);
                    builder.AddCondition(internetCondition);

                    builder.SetTrigger(new TimeTrigger(24 * 60, false));

                    builder.Register();
                    LogTelemetry("Register BG Task Timer CheckValidation Date, InternetAvailable");

                }

                if (!BackgroundTaskRegistration.AllTasks.Any(i => i.Value.Name == startupTask))
                {
                    var builder = new BackgroundTaskBuilder();
                    builder.Name = startupTask;

                    builder.SetTrigger(new SystemTrigger(SystemTriggerType.SessionConnected, false));
                    builder.SetTrigger(new SystemTrigger(SystemTriggerType.UserPresent, false));
                    builder.Register();
                    LogTelemetry("Register BGTask User Available");
                }
            }
            catch (Exception ex)
            {
                var dontWait = new MessageDialog(ex.ToString()).ShowAsync();
                LogException(ex, "EX0028 RegisterPushBackgroundTask()");
            }
        }

        /// <summary>
        /// PopUp a toast with a message and optional Yes/No Buttons
        /// </summary>
        /// <param name="title">Title of the toast</param>
        /// <param name="content">Messaage of the toast</param>
        /// <param name="ButtonArray">Preloaded buttons default: null</param>
        /// <param name="addButtons">Flag to create default buttons default:true</param>
        /// <returns></returns>
        private ToastContent showToast(string title, string content, dynamic ButtonArray = null, bool addButtons = true, String notificationID = "00000")
        {
            try
            {
                if (addButtons)
                {
                    ToastActionsCustom actions = new ToastActionsCustom()
                    {
                        Buttons = //{ };
                {

                }
                    };
                    if (ButtonArray != null)
                    {
                        var label = ButtonArray.GetObject().GetNamedString("label");
                        var action = ButtonArray.GetObject().GetNamedString("action");
 
                        if (action.ToString().Contains("viewTicket"))
                        {
                            action = action + "&notificationID=" + notificationID;
                        }

                        actions.Buttons.Add(
                           new ToastButton(label, new QueryString() {
                            { "action", action}
                               //{ "conversationId", action}
                           }.ToString())
                           {
                               ActivationType = ToastActivationType.Foreground
                           });
                        
                        
                        
                        actions.Buttons.Add(
                           new ToastButton("Close", new QueryString() {
                            { "action", "close"}
                               //{ "conversationId", action}
                           }.ToString())
                           {
                               ActivationType = ToastActivationType.Background
                           });

                        //foreach (var elem in ButtonArray)
                        //{
                        //    var label = elem["label"];// elem.GetObject().GetNamedString("label");
                        //    var action = elem.GetObject().GetNamedString("action"); 
                            
                        //    //try
                        //    //{
                        //    //    var label = elem.GetObject().GetNamedString("label");
                        //    //    var action = elem.GetObject().GetNamedString("action");
                        //    //}
                        //    //catch(Exception ex)
                        //    //{
                        //    //    var label = elem.GetObject().GetNamedString("label");
                        //    //    var action = elem.GetObject().GetNamedString("action");
                        //    //}
                        //    if (action.ToString().Contains("viewTicket"))
                        //    {
                        //        action = action + "&notifcationID=" + notificationID;
                        //    }
                        //    actions.Buttons.Add(
                        //       new ToastButton(label, new QueryString() {
                        //    { "action", action}
                        //           //{ "conversationId", action}
                        //       }.ToString())
                        //       {
                        //           ActivationType = ToastActivationType.Foreground
                        //       });
                        //}
                    }
                    ToastContent visual = new ToastContent()
                    {
                        Visual = new ToastVisual()
                        {
                            BindingGeneric = new ToastBindingGeneric()
                            {
                                Children = {
                                        new AdaptiveText() {
                                          Text = title
                                        },
                                        new AdaptiveText() {
                                          Text = content
                                        }
                                      }
                            }
                        },
                        Actions = actions,
                        Launch = new QueryString()
                    {
                        { "action",  dhp.RenderUrl+"notification?notificationID="+notificationID}

                    }.ToString()
                    };
                    return visual;
                }
                else
                {
                    ToastContent visual = new ToastContent()
                    {
                        Visual = new ToastVisual()
                        {
                            BindingGeneric = new ToastBindingGeneric()
                            {
                                Children = {
                                        new AdaptiveText() {
                                          Text = title
                                        },
                                        new AdaptiveText() {
                                          Text = content
                                        }
                                      }
                            }
                        },
                        Launch = new QueryString()
                    {
                        { "action",  dhp.RenderUrl+"notification?notificationID="+notificationID}

                    }.ToString()
                    };
                    return visual;
                }
            }
            catch(Exception ex)
            {
                LogException(ex, "showToast");
                ToastContent visual = new ToastContent()
                {
                    Visual = new ToastVisual()
                    {
                        BindingGeneric = new ToastBindingGeneric()
                        {
                            Children = {
                                        new AdaptiveText() {
                                          Text = title
                                        },
                                        new AdaptiveText() {
                                          Text = content
                                        }
                                      }
                        }
                    },
                    Launch = new QueryString()
                    {
                        { "action",  dhp.RenderUrl+"notification?notificationID="+notificationID}

                    }.ToString()
                };
                return visual;
            }
        }


        /// <summary>
        /// This method hits whenever a background task execution meets the conditions
        /// </summary>
        /// <param name="args">BacklgroundActivatedArguments</param>
        protected override async void OnBackgroundActivated(BackgroundActivatedEventArgs args)
        {
            const string PushBackgroundTaskName = "DeskhelpBackgroundNotification";
            const string timerRegisterTask = "DeskHelpBackgroundDailyValidation";
            const string startupTask = "DeskHelpBackgroundStartupValidation";

            var deferral = args.TaskInstance.GetDeferral();

            var applicationData = Windows.Storage.ApplicationData.Current;
            var localSettings = applicationData.LocalSettings;
            DeviceInfoString = (string)localSettings.Values["DeviceInfo"];

            if (args.TaskInstance.Task.Name == timerRegisterTask || args.TaskInstance.Task.Name == startupTask)
            {
                var result = await validateExpirationDate();
                ToastContent visual = showToast("Registration Verification" + args.TaskInstance.Task.Name, "Registration was verified succesfully.", null, false);
                ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(visual.GetXml()));
                LogTelemetry($"BG Task triggered Results: Device:{DeviceInfoString}," + args.TaskInstance.Task.Name + ", Results:"+ result, SeverityLevel.Information); 
                deferral.Complete();
                return;
            }
            else if (args.TaskInstance.Task.Name == PushBackgroundTaskName)
            {
                try
                {
                    LogTelemetry($"BG Task triggered Push Background Task triggered. Device Info:{DeviceInfoString}", SeverityLevel.Information);
                    RawNotification notification = (RawNotification)args.TaskInstance.TriggerDetails;

                    // Decrypt the content
                    string payload = "";
                    //payload = payload.ToString().Replace(@"\", "");
                    var payloadJson = JsonObject.Parse(@payload);
                    //dynamic json = JsonConvert.Deserialize<Dictionary<string, string>>(@payload);


                    var deviceDetails = payloadJson.GetObject().GetNamedObject("deviceDetails");
                    var notificationID = payloadJson.GetObject().GetNamedString("notificationID");

                    if (deviceDetails["deviceID"].ToString().Replace("{", "").Replace("}", "").Replace("\"", "") == DeviceInfoString)
                    {
                        bool inApp = true, inDevice = true;
                        try
                        {
                            inApp = payloadJson["inApp"].ToString() == "true" ? payloadJson.GetObject().GetNamedBoolean("inApp") : false;
                        }
                        catch 
                        {
                            inApp = false;
                        }
                        try
                        {
                            inDevice = payloadJson["inDevice"].ToString() == "true" ? payloadJson.GetObject().GetNamedBoolean("inDevice") : false;
                        }
                        catch
                        {
                            inDevice = false;
                        }

                        JsonObject buttons = null;
                        try
                        {
                            buttons = payloadJson.GetObject().GetNamedObject("button");
                        }
                        catch
                        {
                            buttons = null;
                        }



                        //In - Device 
                        if (inDevice && !inApp)
                        {

                            var title = payloadJson["title"].ToString().Replace("{", "").Replace("}", "").Replace("\"", "");
                            var content = payloadJson["description"].ToString().Replace("{", "").Replace("}", "").Replace("\"", "");

                            ToastContent visual = showToast(title, content, buttons, true, notificationID);
                            ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(visual.GetXml()));
                        }
                        else if (inApp && !inDevice)
                        {
                            var userID = payloadJson["userID"].ToString().Replace("{", "").Replace("}", "").Replace("\"", "");
                            //var content = payloadJson["description"].ToString().Replace("{", "").Replace("}", "").Replace("\"", "");
                            
                            if (Appload == true)
                            {
                                await App.WebView.InvokeScriptAsync("pushNotifications", new string[] { userID });
                            }
                            
                        }
                        else if (inApp && inDevice)
                        {
                            var title = payloadJson["title"].ToString().Replace("{", "").Replace("}", "").Replace("\"", "");
                            var content = payloadJson["description"].ToString().Replace("{", "").Replace("}", "").Replace("\"", "");
                            var userID = payloadJson["userID"].ToString().Replace("{", "").Replace("}", "").Replace("\"", "");

                            if (Appload == false)
                            {
                                ToastContent visual = showToast(title, content, buttons, true, notificationID); 
                                ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(visual.GetXml()));
                            }
                            else
                            {
                                await App.WebView.InvokeScriptAsync("pushNotifications", new string[] { userID });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogException(ex, "EX0002 BackgroundActivated()");
                    var dontWait = new MessageDialog(ex.ToString()).ShowAsync();
                }
            }

            deferral.Complete();
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            try
            {
                LogTelemetry("OnLaunched()");
                Frame rootFrame = Window.Current.Content as Frame;

                // Do not repeat app initialization when the Window already has content,
                // just ensure that the window is active
                if (rootFrame == null)
                {
                    // Create a Frame to act as the navigation context and navigate to the first page
                    rootFrame = new Frame();

                    rootFrame.NavigationFailed += OnNavigationFailed;

                    if (e.PreviousExecutionState == ApplicationExecutionState.Terminated)
                    {
                        //TODO: Load state from previously suspended application
                    }

                    // Place the frame in the current Window
                    Window.Current.Content = rootFrame;
                }

                if (e.PrelaunchActivated == false)
                {
                    if (rootFrame.Content == null)
                    {
                        // When the navigation stack isn't restored navigate to the first page,
                        // configuring the new page by passing required information as a navigation
                        // parameter
                        rootFrame.Navigate(typeof(MainPage), e.Arguments);
                    }
                    Windows.UI.ViewManagement.ApplicationView.PreferredLaunchViewSize = new Size(500, 620);

                    // Set app window preferred launch windowing mode
                    Windows.UI.ViewManagement.ApplicationView.PreferredLaunchWindowingMode = Windows.UI.ViewManagement.ApplicationViewWindowingMode.PreferredLaunchViewSize;

                    Window.Current.Activate();

                    Windows.UI.ViewManagement.ApplicationView.GetForCurrentView().TryResizeView(new Size(500, 620));
                }
            }
            catch(Exception ex )
            {
                LogException(ex, "EX0017 OnLaunched()");
            }
        }

        /// <summary>
        /// This method executes when the UWP application is activated
        /// </summary>
        /// <param name="e"></param>
        /// 

        //todo: warning removal
        //protected override async void OnActivated(IActivatedEventArgs e)
        protected override void OnActivated(IActivatedEventArgs e)
        {
            try
            {
                // Get the root frame
                LogTelemetry("OnActivated()");
                Frame rootFrame = Window.Current.Content as Frame;

                // TODO: Initialize root frame just like in OnLaunched

                // Handle toast activation
                if (e is ToastNotificationActivatedEventArgs)
                {
                    var toastActivationArgs = e as Windows.ApplicationModel.Activation.ToastNotificationActivatedEventArgs;
                    QueryString args = QueryString.Parse(toastActivationArgs.Argument);
                    var query = "";
                    try
                    {
                        query = args["action"];
                    }
                    catch (Exception ex)
                    {
                        query = null;
                        LogException(ex, "EX0003 OnActivated()");
                    }
                    if (query.ToString().Contains("close"))// == "close")
                    {
                        return;
                    }
                    if (query != null)
                    {
                        Redirect = true;
                        RedirectURL = query;
                        if (rootFrame == null)
                        {
                            rootFrame = new Frame();
                            Window.Current.Content = rootFrame;
                        }
                        rootFrame.Navigate(typeof(MainPage));
                        Window.Current.Activate();

                    }
                    else
                    {
                        if (rootFrame == null)
                        {
                            rootFrame = new Frame();
                            Window.Current.Content = rootFrame;
                        }
                        rootFrame.Navigate(typeof(MainPage));
                        Window.Current.Activate();
                    }
                }
                else
                {
                    if (rootFrame == null)
                    {
                        rootFrame = new Frame();
                        Window.Current.Content = rootFrame;
                    }
                    rootFrame.Navigate(typeof(MainPage));
                    Window.Current.Activate();
                }
            }
            catch (Exception ex)
            {
                LogException(ex, "EX0018 OnActivated()");
            }
        }

        /// <summary>
        /// Invoked when Navigation to a certain page fails
        /// </summary>
        /// <param name="sender">The Frame which failed navigation</param>
        /// <param name="e">Details about the navigation failure</param>
        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            LogTelemetry($"OnNavigationFailed() {e.SourcePageType.FullName}", SeverityLevel.Error);
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        /// <summary>
        /// Invoked when application execution is being suspended.  Application state is saved
        /// without knowing whether the application will be terminated or resumed with the contents
        /// of memory still intact.
        /// </summary>
        /// <param name="sender">The source of the suspend request.</param>
        /// <param name="e">Details about the suspend request.</param>
        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            try
            {
                LogTelemetry("OnSuspending()");
                var deferral = e.SuspendingOperation.GetDeferral();
                //TODO: Save application state and stop any background activity
                deferral.Complete();
            }
            catch (Exception ex)
            {
                LogException(ex, "EX0019 OnSuspending()");
            }
        }

        /// <summary>
        /// Sends a POST message to a Web API
        /// </summary>
        /// <param name="url"></param>
        /// <param name="json"></param>
        /// <returns></returns>
        /// 

        public static async Task<string> Post(string url, string json)
        {
            try
            {
                HttpClient _httpClient = new HttpClient();
                string resultText = "";

                using (var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"))
                {
                    var httpResponse = _httpClient.PostAsync(url, content).Result;
                    if (httpResponse.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        resultText = httpResponse.Content.ReadAsStringAsync().Result;
                    }
                    LogTelemetry("Post()", SeverityLevel.Information, BuildProperties($"url\t{url}\nStatusCode\t{httpResponse.StatusCode}\nResult\t{resultText}"));

                }
                return resultText;
            }
            catch(Exception ex)
            {
                LogException(ex, $"EX0020 Post() {url} {json}");
                return null;
            }
        }
         
        /// <summary>
        /// Expiration date validation logic
        /// </summary>
        /// <returns></returns>
        #region ExpirationDate

        /// <summary>
        /// Reads expiration date from the Registration json string 
        /// <summary>
        private DateTime getExpirationDateFromRegistration()
        {
            DateTime dt = new DateTime(1, 1, 1);
            try
            {
                var applicationData = Windows.Storage.ApplicationData.Current;
                var localSettings = applicationData.LocalSettings;

                //Get expiration date from registrationid in localstorage
                var registrationId = localSettings.Values["WNSChannelRegistrationId"];

                //todo: warning removal
                //if (!(registrationId == null || registrationId == ""))
                if (!(registrationId == null || (string) registrationId == ""))
                {
                    var regJson = registrationId.ToString();
                    var regObject = JsonObject.Parse(regJson);
                    string dateReg = regObject["expirationTime"].ToString().Replace("{", "").Replace("}", "").Replace("\"", "");
                    dt = Convert.ToDateTime(dateReg);
                }
            }
            catch (Exception ex)
            {
                LogException(ex, "EX0031 getExpirationDateFromRegistration()");
            }

            return dt;

        }


        /// <summary>
        /// Renew the subscription to the back end
        /// </summary>
        public static async Task<string> RenewSubscription()
        {
            string result = "";
            string _subscriptionString = "";
            try
            {
                string guid = MainPage.UserGUID();

                //int index = guid.LastIndexOf("\\");
                //if (index >= 0)
                //    guid = guid.Substring(index + 1);

                //todo: temp code, to remove
                //guid = "1596757";


                string json = dhp.JsonTemplate;
                json = json.Replace("'", "\"").Replace("USERID", guid);

                LogTelemetry("Executing RenewSubscription()");
                string returnValue = await Post(dhp.CheckUserUrl, json);

                //if (returnValue.ToLower().Contains("user details are not available in database"))
                //    return;
                if (string.IsNullOrEmpty(returnValue))
                    return "ERROR";

                var payloadJson = JsonObject.Parse(returnValue);
                var applicationData2 = Windows.Storage.ApplicationData.Current;
                var localSettings2 = applicationData2.LocalSettings;

                var subscriptionObject = localSettings2.Values["WNSChannelRegistrationId"];

                if (subscriptionObject != null)
                {
                    _subscriptionString = subscriptionObject.ToString();
                    var subscription = JsonObject.Parse(_subscriptionString);
                    if (subscription != null) //|| subscription != "")
                    {
                        JsonArray deviceDetails = payloadJson.GetObject().GetNamedArray("deviceDetails");
                        JsonObject deviceDetailTemp = deviceDetails.GetObjectAt(0);
                        //var deviceDetailTemp = deviceDetails[0];
                        payloadJson.GetObject().Remove("deviceDetails");
                        payloadJson.GetObject().Remove("_id");

                        var _deviceInfo = JsonValue.CreateStringValue(localSettings2.Values["DeviceInfo"].ToString());
                        deviceDetailTemp.GetObject().SetNamedValue("deviceID", _deviceInfo);

                        JsonObject notificationRegistry = deviceDetailTemp.GetObject().GetNamedObject("notificationRegistry");
                        notificationRegistry.GetObject().Remove("notificationIdentifier");
                        notificationRegistry.GetObject().Add("notificationIdentifier", subscription);
                        payloadJson.GetObject().Add("deviceDetails", deviceDetailTemp);
                        result = await Post(dhp.UpdateUserUrl, payloadJson.Stringify());
                        DeviceInfoString = _deviceInfo.ToString();
                        result = "";
                    }
                }
                else
                {
                    LogTelemetry("RenewSubscription() Subscription object is null");
                    result = "ERROR 0021";
                }
            }
            catch (Exception ex)
            {
                LogException(ex, $"EX0021 RenewSubscritption(). SubscriptionString at localStorage[WNSChannelRegistrationId]:[{_subscriptionString}]");
                result = "ERROR 0021";
            }
            LogTelemetry($"RenewSubscription() Result:[{result}]");
            return result;
        }
        /// <summary>
        /// Validates the current expiration date when the background task requests it
        /// </summary>
        /// <returns></returns>
        private async Task<string> validateExpirationDate()
        {
            LogTelemetry("BG Task triggered validateExpirationDate() Background Task Run");

            string result = "";
            try
            {
                //var expirationDate = getExpirationDateFromRegistration();
                //if (expirationDate < DateTime.Now.AddDays(-1))
                //{
                //    if (PushManager.IsSupported)
                //    {
                //        var applicationData = Windows.Storage.ApplicationData.Current;
                //        var localSettings = applicationData.LocalSettings;
                //        var DeviceInfo = (string)localSettings.Values["DeviceInfo"];
                //        var subscription = await PushManager.SubscribeAsync(dhp.PublicKey, DeviceInfo);
                //        var _subscriptionJson = subscription.ToJson();
                //        localSettings.Values["WNSChannelRegistrationId"] = _subscriptionJson.ToString();
                //        App.LogTelemetry("BG Task triggered Request for SetUpSubscription()", SeverityLevel.Information, App.BuildProperties($"SubscriptionJson\t{_subscriptionJson}"));
                //        result = await RenewSubscription();
                //    }
                //    else
                //        LogTelemetry("BG Task Trigger validateExpirationdate() PushManager is not supported");
                //}
                //else
                //    LogTelemetry("BG Task Trigger. validateExpirationDate() was run and date is valid");
               
            }
            catch (Exception ex)
            {
                result = "-1";
                LogException(ex, "EX0007 validateExpirationDate()");
            }
            return result;
        }
        #endregion
        
    }
     
}
