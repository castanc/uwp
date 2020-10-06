using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WindowsGateway
{
    public class UserDTO
    {
        public string DisplayName { set; get; }
        public string LastName  {set;get;}
        public string FirstName { set; get; }
        public string AccountName  {set;get;}
        public string DomainName  {set;get;}
        public string GuestHost  {set;get;}
        public string PrincipalName  {set;get;}
        public string ProviderName  {set;get;}
        public string SessionInitiationProtocolUri  {set;get;}

        public override string ToString()
        {
            return $"DisplayName:\t{DisplayName}\r" +
                $"FirstName:\t{FirstName}\r" +
                $"lastName:\t{LastName}\r" +
                $"accountName\t{AccountName}\r" +
                $"domainName\t{DomainName}\r" +
                $"guestHost\t{GuestHost}\r" +
                $"principalName\t{PrincipalName}\r" +
                $"providerName\t{ProviderName}\r" +
                $"sessionInitiationProtocolUri\t{SessionInitiationProtocolUri}\r";
        }

        public string GetData()
        {
            return $"DisplayName:_{DisplayName}|" + 
                $"FirstName:_{FirstName}|" +
                $"lastName:_{LastName}|" +
                $"accountName_{AccountName}|" +
                $"domainName_{DomainName}|" +
                $"guestHost_{GuestHost}|" +
                $"principalName_{PrincipalName}|" +
                $"providerName_{ProviderName}|" +
                $"sessionInitiationProtocolUri_{SessionInitiationProtocolUri}|";
        }

        public string GetRouteData()
        {
            return $"?data={DisplayName}_{FirstName}_{LastName}_{AccountName}_{DomainName}";
        }


    }
}
