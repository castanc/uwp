using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.QueryStringDotNET;
using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Background;
using Windows.Data.Json;
using Windows.Foundation;
using Windows.Networking.PushNotifications;
using Windows.UI.Notifications;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Deskhelp
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    ///  

    sealed partial class App : Application
    {
        public static WebView WebView;
        public static bool Appload = false;
        public static bool Redirect = false;
        public static string RedirectURL = "";
        private static readonly DeskHelpParameters dhp = new DeskHelpParameters();
        public static string DeviceInfoString = "";

        const string PushBackgroundTaskName = "DeskhelpBackgroundNotificationHub";

        #region ApplicationInsights Telemetry

        public static Microsoft.ApplicationInsights.TelemetryClient telemetry;
        public static bool TelemetryEnabled = false;

        public static Dictionary<string, string> BuildProperties(string text)
        {
            var prop = new Dictionary<string, string>();
            var lines = text.Split('\n');
            foreach (string l in lines)
            {
                var line = l.Split('\t');
                if (line.Length >= 2)
                    prop.Add(line[0], line[1]);
            }
            return prop;
        }

        public static Dictionary<string, double> BuildMetrics(string text)
        {
            Dictionary<string, double> metrics = new Dictionary<string, double>();
            string lastValue = "";
            var lines = text.Split('\n');
            foreach (string l in lines)
            {
                var line = l.Split('\t');
                if (line.Length >= 2)
                {
                    try
                    {
                        lastValue = line[1];
                        metrics.Add(line[0], Convert.ToDouble(line[1]));
                    }
                    catch (Exception ex)
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
        public static async void InitializeTelemetry()
        {
            try
            {
                await Microsoft.ApplicationInsights.WindowsAppInitializer.InitializeAsync(
                    WindowsCollectors.Metadata | WindowsCollectors.Session | WindowsCollectors.UnhandledException);

                telemetry = new Microsoft.ApplicationInsights.TelemetryClient();
                telemetry.InstrumentationKey = dhp.AppInsightsKey;
                TelemetryEnabled = telemetry.IsEnabled();
            }
            catch (Exception ex)
            {
                if (TelemetryEnabled)
                    LogException(ex, "InitialilzeTelemetry()");
            }
        }

        public static void LogTelemetry(string text, SeverityLevel severity = SeverityLevel.Information, Dictionary<string, string> properties = null)
        {
            if (TelemetryEnabled)
            {
                telemetry.TrackTrace($"UWP:: DeviceId: [{DeviceInfoString}] {text}", severity, properties);
                telemetry.Flush();

            }
        }

        public static void LogException(Exception ex, string method = "", Dictionary<string, string> properties = null, Dictionary<string, double> metrics = null)
        {
            if (TelemetryEnabled)
            {
                if (!string.IsNullOrEmpty(method))
                    LogTelemetry($"UWP:: DeviceId: [{DeviceInfoString}] EXCEPTION at {method}", SeverityLevel.Critical, BuildProperties($"Error Message\t{ex.Message}\nStack Trace\t{ex.StackTrace}"));
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
            catch (Exception ex)
            {
                LogException(ex, "App constructor initialization");
            }
        }

        private void Current_UnhandledException(object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            LogException(e.Exception, "App Unhandled exception");
        }


        /// <summary>
        /// Registers all the required background tasks
        /// This should be done once
        /// <summary>c
        private void RegisterPushBackgroundTask()
        {
            try
            {

                //If deactivation of all tasks is required, uncomment this code
                //todo: leave commented for release
                //foreach (var task in BackgroundTaskRegistration.AllTasks)
                //     task.Value.Unregister(true);

                if (!BackgroundTaskRegistration.AllTasks.Any(i => i.Value.Name == PushBackgroundTaskName))
                {
                    var builder = new BackgroundTaskBuilder
                    {
                        Name = PushBackgroundTaskName
                    };
                    builder.SetTrigger(new PushNotificationTrigger());
                    builder.Register();
                    LogTelemetry($"Request for Register BGTask {PushBackgroundTaskName}");
                }
            }
            catch (Exception ex)
            {
                //var dontWait = new MessageDialog(ex.ToString()).ShowAsync();
                LogException(ex, $"RegisterPushBackgroundTask() {PushBackgroundTaskName}");
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
            catch (Exception ex)
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
            var applicationData = Windows.Storage.ApplicationData.Current;
            var localSettings = applicationData.LocalSettings;
            var DeviceInfo = (string)localSettings.Values["DeviceInfo"];

            var deferral = args.TaskInstance.GetDeferral();

            if (args.TaskInstance.Task.Name == PushBackgroundTaskName)
            {
                try
                {
                    LogTelemetry($"BG Task triggered Push Background Task triggered.", SeverityLevel.Information);
                    RawNotification notification = (RawNotification)args.TaskInstance.TriggerDetails;

                    // Decrypt the content
                    var payload = notification.Content;
                    var payloadJson = JsonObject.Parse(@payload);

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

                        if (inApp && !inDevice)
                        {
                            var userID = payloadJson["userID"].ToString().Replace("{", "").Replace("}", "").Replace("\"", "");
                            if (Appload == true)
                                await App.WebView.InvokeScriptAsync("pushNotifications", new string[] { userID });
                        }
                        else if ((inApp && inDevice) || (inDevice && !inApp))
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
                                await App.WebView.InvokeScriptAsync("pushNotifications", new string[] { userID });
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogException(ex, "BackgroundActivated()");
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
                var applicationData = Windows.Storage.ApplicationData.Current;
                var localSettings = applicationData.LocalSettings;
                string DeviceInfo = (string)localSettings.Values["DeviceInfo"];

                App.DeviceInfoString = DeviceInfo;

                LogTelemetry("OnLaunched()");
                Frame rootFrame = Window.Current.Content as Frame;

                // Do not repeat app initialization when the Window already has content,
                // just ensure that the window is active
                if (rootFrame == null)
                {
                    // Create a Frame to act as the navigation context and navigate to the first page
                    rootFrame = new Frame();
                    rootFrame.NavigationFailed += OnNavigationFailed;

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
            catch (Exception ex)
            {
                LogException(ex, "OnLaunched()");
            }
        }

        /// <summary>
        /// This method executes when the UWP application is activated
        /// </summary>
        /// <param name="e"></param>
        /// 

        protected override void OnActivated(IActivatedEventArgs e)
        {
            try
            {
                // Get the root frame
                LogTelemetry("OnActivated()");
                Frame rootFrame = Window.Current.Content as Frame;

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
                    catch (Exception)
                    {
                        query = null;
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
                LogException(ex, "OnActivated()");
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
                LogException(ex, "OnSuspending()");
            }
        }
    }
}
