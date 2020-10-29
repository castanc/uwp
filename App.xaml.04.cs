using AlternatePushChannel.Library;
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

    sealed partial class App : Application
    {
        public static WebView WebView;
        public static Boolean Appload = false;
        public static Boolean Redirect = false;
        public static string RedirectURL = "";
        public DateTime ExpirationDate;
        public DateTime LastExpirationDateVerification = new DateTime(1, 1, 1);
        private static DeskHelpParameters dhp = new DeskHelpParameters();
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

        public static void LogTelemetry(string text, SeverityLevel severity = SeverityLevel.Information, Dictionary<string,string> properties = null )
        {
            if (TelemetryEnabled)
            {
                telemetry.TrackTrace($"UWP::{text}", severity, properties);
            }
        }

        public static void LogException(Exception ex, string method = "", Dictionary<string,string> properties = null , Dictionary<string,double> metrics = null)
        {
            if (TelemetryEnabled)
            {
                if ( !string.IsNullOrEmpty(method))
                    LogTelemetry($"EXCEPTION at {method}", SeverityLevel.Critical, BuildProperties($"Error Message\t{ex.Message}\nStack Trace\t{ex.StackTrace}"));
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
            this.InitializeComponent();
            InitializeTelemetry();
            Application.Current.UnhandledException += Current_UnhandledException;
            MainPage.dhp = dhp;
            this.Suspending += OnSuspending;
            RegisterPushBackgroundTask();
        }

        private void Current_UnhandledException(object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            LogException(e.Exception, "EX0001 App Unhandled exception");
        }


        /// <summary>
        /// Registers all the required background tasks
        /// This should be done once
        /// <summary>
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
                //    task.Value.Unregister(true);

                if (!BackgroundTaskRegistration.AllTasks.Any(i => i.Value.Name == PushBackgroundTaskName))
                {
                    var builder = new BackgroundTaskBuilder();
                    builder.Name = PushBackgroundTaskName;
                    builder.SetTrigger(new PushNotificationTrigger());
                    builder.Register();
                    LogTelemetry("RegisterPushBackgroundTask() Push background task registered for the first time");
                }

                if (!BackgroundTaskRegistration.AllTasks.Any(i => i.Value.Name == timerRegisterTask))
                {
                    var builder = new BackgroundTaskBuilder();
                    builder.Name = timerRegisterTask;

                    SystemCondition internetCondition = new SystemCondition(SystemConditionType.InternetAvailable);
                    builder.AddCondition(internetCondition);

                    builder.SetTrigger(new TimeTrigger(24 * 60, false));

                    builder.Register();
                    LogTelemetry("RegisterPushBackgroundTask() Timer background task registered for the first time");

                }

                if (!BackgroundTaskRegistration.AllTasks.Any(i => i.Value.Name == startupTask))
                {
                    var builder = new BackgroundTaskBuilder();
                    builder.Name = startupTask;

                    SystemCondition userPresent = new SystemCondition(SystemConditionType.UserPresent);
                    builder.AddCondition(userPresent);

                    builder.SetTrigger(new TimeTrigger(24*60, true));
                    builder.Register();
                    LogTelemetry("RegisterPushBackgroundTask() StartUp background task registered for the first time");
                }
            }
            catch (Exception ex)
            {
                var dontWait = new MessageDialog(ex.ToString()).ShowAsync();
                LogException(ex, "RegisterPushBackgroundTask()");
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
                LogTelemetry("showToast()", SeverityLevel.Information, BuildProperties($"Title\t{title}\nContent\t{content}"));
                LogTelemetry("ButtonArray()"+ ButtonArray.ToString());//, SeverityLevel.Information, BuildProperties($"Title\t{title}\nContent\t{content}")); 
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
                            action = action + "&notifcationID=" + notificationID;
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
                               ActivationType = ToastActivationType.Foreground
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
            LogTelemetry("OnBackgroundActivated() triggered", SeverityLevel.Information, BuildProperties($"Background Task Name\t{args.TaskInstance.Task.Name}"));

            if (args.TaskInstance.Task.Name == timerRegisterTask || args.TaskInstance.Task.Name == startupTask)
            {
                var result = await validateExpirationDate();
                //ToastContent visual = showToast("Registration Verification", "Registration was verified succesfully.", null, false);
                //ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(visual.GetXml()));
                deferral.Complete();
                return;
            }
            else if (args.TaskInstance.Task.Name == PushBackgroundTaskName)
            {
                try
                {

                    RawNotification notification = (RawNotification)args.TaskInstance.TriggerDetails;

                    // Decrypt the content
                    string payload = await PushManager.GetDecryptedContentAsync(notification);
                    //payload = payload.ToString().Replace(@"\", "");
                    var payloadJson = JsonObject.Parse(@payload);
                    //dynamic json = JsonConvert.Deserialize<Dictionary<string, string>>(@payload);

                    var applicationData2 = Windows.Storage.ApplicationData.Current;
                    var localSettings2 = applicationData2.LocalSettings;
                    var DeviceInfo = (string)localSettings2.Values["DeviceInfo"];

                    var deviceDetails = payloadJson.GetObject().GetNamedObject("deviceDetails");
                    var notificationID = payloadJson.GetObject().GetNamedString("notificationID");

                    if (deviceDetails["deviceID"].ToString().Replace("{", "").Replace("}", "").Replace("\"", "") == DeviceInfo)
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
                    var dontWait = new MessageDialog(ex.ToString()).ShowAsync();
                    LogException(ex, "EX0002 BackgroundActivated()");
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
                Windows.UI.ViewManagement.ApplicationView.PreferredLaunchViewSize = new Size(500, 600);

                // Set app window preferred launch windowing mode
                Windows.UI.ViewManagement.ApplicationView.PreferredLaunchWindowingMode = Windows.UI.ViewManagement.ApplicationViewWindowingMode.PreferredLaunchViewSize;

                Window.Current.Activate();

                Windows.UI.ViewManagement.ApplicationView.GetForCurrentView().TryResizeView(new Size(500, 600));
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
                if(query == "close")
                {
                    return;
                }
                if (query != null  )
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

        /// <summary>
        /// Invoked when Navigation to a certain page fails
        /// </summary>
        /// <param name="sender">The Frame which failed navigation</param>
        /// <param name="e">Details about the navigation failure</param>
        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            LogTelemetry($"OnNavigationFailed() {e.SourcePageType.FullName}");
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
            LogTelemetry("OnSuspending()");
            var deferral = e.SuspendingOperation.GetDeferral();
            //TODO: Save application state and stop any background activity
            deferral.Complete();
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
            HttpClient _httpClient = new HttpClient();
            string resultText = "";

            using (var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"))
            {
               var httpResponse = _httpClient.PostAsync(url, content).Result;
                if(httpResponse.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    resultText = httpResponse.Content.ReadAsStringAsync().Result;
                }
                LogTelemetry("Post()", SeverityLevel.Information, BuildProperties($"url\t{url}\nStatusCode\t{httpResponse.StatusCode}\nResult\t{resultText}"));

            }
            return resultText;
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
                LogException(ex, "EX0004 getExpirationDateFromRegistration()");
            }
            LogTelemetry("getExpirationDateFromRegistration() ", SeverityLevel.Information, BuildProperties($"Date\t{dt}"));

            return dt;

        }

        /// <summary>
        /// Gets expiration date from localstorage
        /// </summary>
        /// <param name="dt">Initial date</param>
        /// <returns></returns>
        private DateTime GetExpirationdate(DateTime dt)
        {
            try
            {
                var applicationData = Windows.Storage.ApplicationData.Current;
                var localSettings = applicationData.LocalSettings;

                if (dt == null || dt == new DateTime(1, 1, 1))
                {
                    //getcurrent expiration date from local storage
                    var expDate = localSettings.Values["ExpirationTime"];
                    if (expDate != null)
                        dt = buildExpirationDate(expDate.ToString());
                    else
                    {
                        dt = getExpirationDateFromRegistration();
                        //Saves expiration date in a string CSV format
                        string stringDate = $"{dt.Year},{dt.Month},{dt.Day},{dt.Hour},{dt.Minute},{dt.Second}";
                        localSettings.Values["ExpirationTime"] = stringDate;
                    }
                }
            }
            catch (Exception ex)
            {
                dt = new DateTime(1, 1, 1);
                LogException(ex, "EX0005 GetExpirationdate()");
            }
            return dt;
        }

        /// <summary>
        /// Build expiration date from CSV string
        /// </summary>
        /// <param name="sdt"></param>
        /// <returns></returns>
        private DateTime buildExpirationDate(string sdt)
        {
            DateTime dt = new DateTime(1, 1, 1);
            try
            {
                var p = sdt.Split(',');
                dt = new DateTime(Convert.ToInt32(p[0]),
                    Convert.ToInt32(p[1]),
                    Convert.ToInt32(p[2]),
                    Convert.ToInt32(p[3]),
                    Convert.ToInt32(p[4]),
                    Convert.ToInt32(p[5]));
            }
            catch (Exception ex)
            {
                LogException(ex, "EX0006 buildExpirationDate()");
            }
            return dt;
        }




        /// <summary>
        /// Renew the subscription to the back end
        /// </summary>
        public static async Task<string> RenewSubscription()
        {
            //todo: get user id
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
            if (returnValue == "")
                return returnValue;

            var payloadJson = JsonObject.Parse(returnValue);
            var applicationData2 = Windows.Storage.ApplicationData.Current;
            var localSettings2 = applicationData2.LocalSettings;

            var subscription = JsonObject.Parse(localSettings2.Values["WNSChannelRegistrationId"].ToString());
            if (subscription != null) //|| subscription != "")
            {
                var deviceDetails = payloadJson.GetObject().GetNamedArray("deviceDetails");
                var deviceDetailTemp = deviceDetails[0];
                payloadJson.GetObject().Remove("deviceDetails");
                payloadJson.GetObject().Remove("_id");


                dynamic notificationRegistry = deviceDetailTemp.GetObject().GetNamedObject("notificationRegistry");
                notificationRegistry.GetObject().Remove("notificationIdentifier");
                notificationRegistry.GetObject().Add("notificationIdentifier", subscription);
                payloadJson.GetObject().Add("deviceDetails", deviceDetailTemp);


                var renewResult = await Post(dhp.UpdateUserUrl, payloadJson.Stringify());
                return renewResult;
            }
            else
            {
                LogTelemetry("RenewSubscription() Subscription object is null");
                return "";
            }

        }
        /// <summary>
        /// Validates the current expiration date when the background task requests it
        /// </summary>
        /// <returns></returns>
        private async Task<int> validateExpirationDate()
        {
            LogTelemetry("validateExpirationDate() Background Task Run");

            int result = 0;
            try
            {
                if (LastExpirationDateVerification < DateTime.Now.AddDays(-1))
                {
                    var applicationData = Windows.Storage.ApplicationData.Current;
                    var localSettings = applicationData.LocalSettings;

                    ExpirationDate = GetExpirationdate(ExpirationDate);

                    if (ExpirationDate < DateTime.Now.AddDays(-1))
                    {
                        //resubscribe
                        LogTelemetry("validateExpirationDate() expiration Date is over, resubscribing");
                        var subscription = await PushManager.SubscribeAsync(dhp.PublicKey, "myChannel1");
                        string _subscriptionJson = subscription.ToJson();

                        var regObject = JsonObject.Parse(_subscriptionJson);
                        string dateReg = regObject["expirationTime"].ToString().Replace("{", "").Replace("}", "").Replace("\"", "");

                        //Update expiration date to localstorage
                        ExpirationDate = Convert.ToDateTime(dateReg);
                        string stringDate = $"{ExpirationDate.Year},{ExpirationDate.Month},{ExpirationDate.Day},{ExpirationDate.Hour},{ExpirationDate.Minute},{ExpirationDate.Second}";
                        localSettings.Values["ExpirationTime"] = stringDate;
                        LogTelemetry("validateExpirationDate() calling RenewSubscription()");
                        var RenewSubscriptionValue = await RenewSubscription();
                        result = 2;
                        
                    }
                    LastExpirationDateVerification = DateTime.Now;
                }
            }
            catch (Exception ex)
            {
                result = -1;
                LogException(ex, "EX0007 validateExpirationDate()");
            }
            return result;
        }
        #endregion
        
    }
     
}
