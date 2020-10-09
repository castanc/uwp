 private async Task<bool> ReSubscription()
        {
            try
            {
                var applicationData = Windows.Storage.ApplicationData.Current;
                var localSettings = applicationData.LocalSettings;
                var registrationId = localSettings.Values["WNSChannelRegistrationId"];
                DateTime regDateObject = new DateTime(1, 1, 1);

                if (registrationId != null && (string)registrationId != "")
                {
                    var regJson = registrationId.ToString();
                    var regObject = JsonObject.Parse(regJson);
                    string dateReg = regObject["expirationTime"].ToString().Replace("{", "").Replace("}", "").Replace("\"", "");
                    regDateObject = Convert.ToDateTime(dateReg);

                }
                if ( regDateObject < DateTime.Now)
                {
                    if (PushManager.IsSupported)
                    {
                        var subscription = await PushManager.SubscribeAsync(PublicKey, "myChannel1");
                        localSettings.Values["WNSChannelRegistrationId"] = _subscriptionJson;
                        localSettings.Values["WNSChannelRegistrationIdCheck"] = true;
                    }
                }
                localSettings.Values["WNSChannelRegistrationIdCheck"] = !true;
                return true;
            }
            catch (Exception ex)
            {
                return true;
            }
        }
