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

namespace Deskhelp_UWP
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    sealed partial class App : Application
    {
        public static WebView WebView;
        public static Boolean Appload= false;
        public static Boolean Redirect= false;
        public BackgroundTaskRegistration timerTask;
        public DateTime ExpirationDate;
        public DateTime LastExpirationDateVerification = new DateTime(1, 1, 1);

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
            this.Suspending += OnSuspending;

            RegisterPushBackgroundTask();
            validateExpirationDate();
        }

        private void RegisterPushBackgroundTask()
        {
            try
            {
                const string PushBackgroundTaskName = "DeskhelpBackground";

                if (!BackgroundTaskRegistration.AllTasks.Any(i => i.Value.Name == PushBackgroundTaskName))
                {
                    var builder = new BackgroundTaskBuilder();
                    builder.Name = PushBackgroundTaskName;
                    builder.SetTrigger(new PushNotificationTrigger());
                    builder.Register();
                }

                const string timerTaskName = "DeskHelpTimerTask";
                if (!BackgroundTaskRegistration.AllTasks.Any(i => i.Value.Name == timerTaskName))
                {
                    var builder = new BackgroundTaskBuilder();
                    builder.Name = timerTaskName;

                    builder.SetTrigger(new TimeTrigger(60*24, true));
                    timerTask = builder.Register();

                }

            }
            catch (Exception ex)
            {
                var dontWait = new MessageDialog(ex.ToString()).ShowAsync();
            }
        }


        #region ExpirationDate
        private DateTime getExpirationDateFromRegistration()
        {
            DateTime dt = new DateTime(1, 1, 1);
            try
            {
                var applicationData = Windows.Storage.ApplicationData.Current;
                var localSettings = applicationData.LocalSettings;

                //Get expiration date from registrationid in localstorage
                var registrationId = localSettings.Values["WNSChannelRegistrationId"];

                if (!(registrationId == null || registrationId == ""))
                {
                    var regJson = registrationId.ToString();
                    var regObject = JsonObject.Parse(regJson);
                    string dateReg = regObject["expirationTime"].ToString().Replace("{", "").Replace("}", "").Replace("\"", "");
                    dt = Convert.ToDateTime(dateReg);
                }
            }
            catch(Exception ex)
            {

            }

            return dt;

        }

        private DateTime GetExpirationdate(DateTime dt)
        {
            try
            {
                var applicationData = Windows.Storage.ApplicationData.Current;
                var localSettings = applicationData.LocalSettings;

                if (dt == null || dt == new DateTime(1,1,1) )
                {
                    //getcurrent expirationd ate from local storage
                    var expDate = localSettings.Values["ExpirationTime"];
                    if (expDate != null)
                        dt = buildExpirationDate(expDate.ToString());
                    else
                    {
                        dt = getExpirationDateFromRegistration();
                        string stringDate = $"{dt.Year},{dt.Month},{dt.Day},{dt.Hour},{dt.Minute},{dt.Second}";
                        localSettings.Values["ExpirationTime"] = stringDate;
                    }
                }
            }
            catch(Exception ex)
            {
                dt = new DateTime(1, 1, 1);
            }
            return dt;
        }

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
            catch(Exception ex )
            {

            }
            return dt;
        }

        private async void validateExpirationDate()
        {
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
                        var subscription = await PushManager.SubscribeAsync(MainPage.GetPublicKey(), "myChannel1");
                        string _subscriptionJson = subscription.ToJson();

                        var regObject = JsonObject.Parse(_subscriptionJson);
                        string dateReg = regObject["expirationTime"].ToString().Replace("{", "").Replace("}", "").Replace("\"", "");

                        //Update expiration date
                        ExpirationDate = Convert.ToDateTime(dateReg);
                        string stringDate = $"{ExpirationDate.Year},{ExpirationDate.Month},{ExpirationDate.Day},{ExpirationDate.Hour},{ExpirationDate.Minute},{ExpirationDate.Second}";
                        localSettings.Values["ExpirationTime"] = stringDate;
                    }
                    LastExpirationDateVerification = DateTime.Now;
                }
            }
            catch (Exception ex)
            {

            }
        }
#endregion



        protected override async void OnBackgroundActivated(BackgroundActivatedEventArgs args)
        {
            var deferral = args.TaskInstance.GetDeferral();

            try
            {
                validateExpirationDate();

                RawNotification notification = (RawNotification)args.TaskInstance.TriggerDetails;

                // Decrypt the content
                string payload = await PushManager.GetDecryptedContentAsync(notification);
                //var payloadJson = payload.ToString();
                JsonObject payloadJson = JsonObject.Parse(payload);

                var applicationData2 = Windows.Storage.ApplicationData.Current;
                var localSettings2 = applicationData2.LocalSettings;
                var DeviceInfo = (string)localSettings2.Values["DeviceInfo"];

                if (payloadJson["AssetID"].ToString().Replace("{", "").Replace("}", "").Replace("\"", "") == DeviceInfo)
                {
                    if (payloadJson["type"].ToString().Replace("{", "").Replace("}", "").Replace("\"", "") == "in-device")
                    {

                        var title = payloadJson["title"].ToString().Replace("{", "").Replace("}", "").Replace("\"", "");
                        var content = payloadJson["content"].ToString().Replace("{", "").Replace("}", "").Replace("\"", "");

                        // Show a notification
                        // You'll need Microsoft.Toolkit.Uwp.Notifications NuGet package installed for this code

                        // Construct the actions for the toast (inputs and buttons)
                        ToastActionsCustom actions = new ToastActionsCustom()
                        {
                            Buttons =
                {
                    new ToastButton("Yes", new QueryString()
                    {
                        { "action", "reply" },
                        { "conversationId", "test"}

                    }.ToString())
                    {
                        ActivationType = ToastActivationType.Background
                    },

                    new ToastButton("No", new QueryString()
                    {
                        { "action", "like" },
                        { "conversationId", "test" }

                    }.ToString())
                    {
                        ActivationType = ToastActivationType.Background
                    }

                    }
                        };


                        ToastContent visual = new ToastContent()
                        {
                            Visual = new ToastVisual()
                            {
                                BindingGeneric = new ToastBindingGeneric()
                                {
                                    Children =
                        {
                            new AdaptiveText()
                            {
                                Text = title
                            },
                            new AdaptiveText()
                            {
                                Text = content
                            }
                        }
                                }
                            },
                            Actions = actions
                        };
                        ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(visual.GetXml()));
                    }
                    else
                    {
                        await App.WebView.InvokeScriptAsync("pushNotifications", new string[] { "One" });
                    }
                    //if (Appload == false)
                    //{
                    //    ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(visual.GetXml()));
                    //}
                    //else
                    //{
                    //    await App.WebView.InvokeScriptAsync("pushNotifications", new string[] { "One" });
                    //}

                }
                //ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(content.GetXml()));
            }
            catch (Exception ex)
            {
                var dontWait = new MessageDialog(ex.ToString()).ShowAsync();

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
            //Windows.UI.ViewManagement.ApplicationView.PreferredLaunchViewSize = new Size(500, 700);
            //Windows.UI.ViewManagement.ApplicationView.PreferredLaunchWindowingMode = Windows.UI.ViewManagement.ApplicationViewWindowingMode.PreferredLaunchViewSize;
            //Windows.UI.ViewManagement.ApplicationView.GetForCurrentView().SetPreferredMinSize(new Size(500, 700));


            //Windows.UI.ViewManagement.ApplicationView.GetForCurrentView().SetPreferredMinSize(new Size { Width = 500, Height = 700 });
            //Windows.UI.ViewManagement.ApplicationView.PreferredLaunchViewSize = new Size(500, 700);
            //Windows.UI.ViewManagement.ApplicationView.PreferredLaunchWindowingMode = Windows.UI.ViewManagement.ApplicationViewWindowingMode.PreferredLaunchViewSize;
            
            
            //Windows.UI.ViewManagement.ApplicationView.GetForCurrentView().SetPreferredMinSize(new Size { Height = 400, Width = 600 }); 
            
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
                // Ensure the current window is active
                //float DPI = Windows.Graphics.Display.DisplayInformation.GetForCurrentView().LogicalDpi;

                //Windows.UI.ViewManagement.ApplicationView.PreferredLaunchWindowingMode = Windows.UI.ViewManagement.ApplicationViewWindowingMode.PreferredLaunchViewSize;

                ////var desiredSize = new Windows.Foundation.Size(((float)600 * 96.0f / DPI), ((float)800 * 96.0f / DPI));

                //Windows.UI.ViewManagement.ApplicationView.PreferredLaunchViewSize = new Size(500, 600); ;

                Window.Current.Activate();

                Windows.UI.ViewManagement.ApplicationView.GetForCurrentView().TryResizeView(new Size(500, 600));
                //bool result =  
            }
        }

        protected override async void OnActivated(IActivatedEventArgs e)
        {
            // Get the root frame
            Frame rootFrame = Window.Current.Content as Frame;

            // TODO: Initialize root frame just like in OnLaunched

            // Handle toast activation
            if (e is ToastNotificationActivatedEventArgs)
            {
                if (Appload == false)
                {                  
                    Appload = true;
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
                    Redirect = true;// string url_new = "https://deskhelptest.azurewebsites.net/ui/subscription";
                    if (rootFrame == null)
                    {
                        rootFrame = new Frame();
                        Window.Current.Content = rootFrame;
                    }
                    rootFrame.Navigate(typeof(MainPage));
                    Window.Current.Activate();
                }
                
            }
            Window.Current.Activate();
            
        }

        /// <summary>
        /// Invoked when Navigation to a certain page fails
        /// </summary>
        /// <param name="sender">The Frame which failed navigation</param>
        /// <param name="e">Details about the navigation failure</param>
        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }
         
        //void OnEx(object sender, NavigationFailedEventArgs e)
        //{
        //    throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        //}

        /// <summary>
        /// Invoked when application execution is being suspended.  Application state is saved
        /// without knowing whether the application will be terminated or resumed with the contents
        /// of memory still intact.
        /// </summary>
        /// <param name="sender">The source of the suspend request.</param>
        /// <param name="e">Details about the suspend request.</param>
        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            //TODO: Save application state and stop any background activity
            deferral.Complete();
        }
    }
}
