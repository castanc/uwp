 private static async void SendNotificationAsync()
        {
            NotificationHubClient hubClient = NotificationHubClient.CreateClientFromConnectionString(ConnectionString, "deskhelp");
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
                notif.Body = "hi from .net";
                
                var ct = new CancellationToken();

                await hubClient.SendNotificationAsync(notif, "@1234", ct);  //.Wait();

            }
            catch (Exception e)
            {
                var s = "";
            }
