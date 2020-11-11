using System;
using IdentityModel;
using Microsoft.Azure.NotificationHubs;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;
using System.Threading;
using System.Security.Cryptography;


//https://enzocontini.blog/2015/10/19/push-notification-using-the-azure-notification-hub/
/*
 * packages
 * Microsoft.Azure.NotificationHubs
 * IdentityServer4
 */
namespace SendNotification
{
    class Program
    {

        static void Main(string[] args)
        {
            //codegen();
            SendNotificationAsync();
            //SendNotification2();
        }


        private static async void codegen()
        {
            byte[] toEncodeAsBytes = System.Text.Encoding.GetEncoding("ISO-8859-1").GetBytes("9BarH1M6JSEk2X.fWI.c~WZrFyKD_vAQI-");
            string returnValue = System.Convert.ToBase64String(toEncodeAsBytes); 
            
            
            var codeVerifier = CryptoRandom.CreateUniqueId(32);
            //var codeVerifier = "1qaz2wsx3edc4rfv5tgb6yhn1234567890qwertyuiop";

            string codeChallenge;
            using (var sha256 = SHA256.Create())
            {
                var challengeBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(codeVerifier));
                codeChallenge = Base64Url.Encode(challengeBytes);
            }

            Console.WriteLine("codeVerifier " + codeVerifier + "\n");
            Console.WriteLine("codeChallenge " + codeChallenge + "\n");

            Console.ReadLine();
        }


        private const string ConnectionString = "Endpoint=sb://deskhelpdev.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=wTai8dxwIiYMt+FhtoTNegYqlfFreGWMFNSykjfDhZs=";
        private static async void SendNotificationAsync()
        {

            NotificationHubClient hubClient = NotificationHubClient.CreateClientFromConnectionString(ConnectionString, "deskhelp",true);
            string[] userTag = new string[2];
            userTag[0] = "username: Test" ;
            userTag[1] = "from:  user";
            var toast = @"<toast><visual><binding template=""ToastText01""><text id=""1"">From any .NET App!</text></binding></visual></toast>";
            var rowPayload = "Notification at " + DateTime.Now.ToString(System.Globalization.CultureInfo.CreateSpecificCulture("it-IT"));
            var toasts = @"<toast><visual><binding template=""ToastText01""><text id=""1"">" +
                       "From Test: Message" + "</text></binding></visual></toast>";
            try {
                //var outcome = hubClient.SendWindowsNativeNotificationAsync(toasts, userTag);
                var payLoad = @"From any .NET App!";
                var notif = new WindowsNotification(payLoad);
                notif.ContentType = "application/octet-stream";
                notif.Headers.Add("X-WNS-Type", "wns/raw");
                notif.Headers.Add("ServiceBusNotification-Format", "windows");
                //notif.Headers.Add("Authorization", "wTai8dxwIiYMt+FhtoTNegYqlfFreGWMFNSykjfDhZs=");
                //notif.Headers.Add("Host", "cloud.notify.windows.com");
                notif.Body = "{\"name\":\"test\"}";
                
                var ct = new CancellationToken();

                var tags = new List<string>();
                tags.Add("@tag1");

                hubClient.SendNotificationAsync(notif,tags).Wait();






            }
            catch (Exception e)
            {
                var s = "";
            }
            //            #region case 1: broadcasted
            //            /*
            //            //the payload can be whatever: the Azure Notification Hub pass through everything to WNS and possible errore could be returned froew that is not well formed.
            //            await hubClient.SendWindowsNativeNotificationAsync(toast);
            //            */
            //            #endregion case 1: broadcasted

            //            #region case 2: client subscribed by SubscribeToCategories
            //            /* */
            //            //There is not the pain for a developer to mantain the registry of tags
            //            //If we want a toast notification
            //            // await hubClient.SendWindowsNativeNotificationAsync(toast, "Torino"); // hubClient.SendWindowsNativeNotificationAsync(toast, "Torino").Wait(); //notity to clients subcribed to "World" tag
            //            // //or hubClient.SendWindowsNativeNotificationAsync(toast, "Torino &amp;amp;&amp;amp; !Politics").Wait(); //notify to clients subcribed to "World" tag but not subscribed to the Politics tag too. In expression like this (that can use also parenthesis) it can be used at maximun 6 tags in the expression

            //            //If we want to have a row notification that can be handled by code in the running client app
            //            Notification notification = new WindowsNotification(rowPayload);
            //            notification.Headers = new Dictionary<string, string> {
            //// {"Content-Type", "application/octet-stream")},
            //{"X-WNS-TTL","300"}, // e.g. 300 seconds=> 5 minutes - Specifies the TTL (expiration time) for a notification.
            //{"X-WNS-Type", "wns/raw" },
            //{"ServiceBusNotification-Format", "windows"}
            //};
            //            await hubClient.SendNotificationAsync(notification, "deskhelp");
            //            /* */
            //            #endregion case 2: client subscribed by SubscribeToCategories

            //            #region case 3: client SubscribeToCategoriesWithCustomTemplate
            //            /*
            //            //the template and internalization is own by the client that registes to have notifications
            //            //template back to the mobile app: it is the client that knows the format he will receive
            //            //you can put any property and payload you whant; you can personalize the notification, depending to the registration
            //            //we do not use anymore the var toast but a dictionary: the server code is agnostic of the type of client (IOS, Android, Windows) that has to define a similar template related to News_locale
            //            var notification = new Dictionary<string, string>() {
            //            {"News_English", "World news in English"},
            //            {"News_Italian", "Notizie dal mondo in italiano"}
            //            };
            //            //send then a template notification not a Windows one
            //            await hubClient.SendTemplateNotificationAsync(notification, "World");
            //            */
            //            #endregion case 3: client SubscribeToCategoriesWithCustomTemplate        
        }


        private async static void SendNotification2()
        {
            Registration reg = new Registration();
            await reg.CreateNewRegistration("wns");
        }

    }
}
