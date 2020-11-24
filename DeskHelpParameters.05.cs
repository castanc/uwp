namespace Deskhelp
{
    public class DeskHelpParameters
    {
        public string CheckUserUrl { set; get; }
        public string UpdateUserUrl { set; get; }
        public string PublicKey { set; get; }
        public string JsonTemplate { set; get; }
        public string AppInsightsKey { set; get; }
        public string RenderUrl { set; get; }
        public string NotificationHubURL { set; get; }
        public string NotificationHubName { set; get; }



        public DeskHelpParameters()
        {
            AppInsightsKey = "1f02820d-e873-4c04-9562-e9376037c310";
            NotificationHubURL = "Endpoint=sb://deskhelpdev.servicebus.windows.net/;SharedAccessKeyName=DefaultListenSharedAccessSignature;SharedAccessKey=TIej1gJ3RQbP3KKKhJElI/mqXnjRw4z97XibauhEbxs=";
            RenderUrl = "https://tcsdeskhelpdev.azurewebsites.net/ui";
            NotificationHubName = "deskhelp";
        }
    }
}
