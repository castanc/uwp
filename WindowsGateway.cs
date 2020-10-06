using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.System;

namespace WindowsGateway
{
    public class WindowsGateway
    {
        public UserDTO dto = new UserDTO();

        public async Task<UserDTO> GetUserInfo()
        {
                
            IReadOnlyList<User> users = await User.FindAllAsync();

            var current = users.Where(p => p.AuthenticationStatus == UserAuthenticationStatus.LocallyAuthenticated &&
                                        p.Type == UserType.LocalUser).FirstOrDefault();

            // user may have username
            var data = await current.GetPropertyAsync(KnownUserProperties.AccountName);
            dto.DisplayName = (string)data;

            //or may be authinticated using hotmail 
            //if (String.IsNullOrEmpty(displayName))
            {

                dto.FirstName = (string)await current.GetPropertyAsync(KnownUserProperties.FirstName);
                dto.LastName = (string)await current.GetPropertyAsync(KnownUserProperties.LastName);
                dto.AccountName = (string)await current.GetPropertyAsync(KnownUserProperties.AccountName);
                dto.DomainName = (string)await current.GetPropertyAsync(KnownUserProperties.DomainName);
                dto.GuestHost = (string)await current.GetPropertyAsync(KnownUserProperties.GuestHost);
                dto.PrincipalName = (string)await current.GetPropertyAsync(KnownUserProperties.PrincipalName);
                dto.ProviderName = (string)await current.GetPropertyAsync(KnownUserProperties.ProviderName);
                dto.SessionInitiationProtocolUri = (string)await current.GetPropertyAsync(KnownUserProperties.SessionInitiationProtocolUri);

            }
            return dto;
        }
    }
}
