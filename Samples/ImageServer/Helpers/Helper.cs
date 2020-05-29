using System;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace ImageServer.Helpers
{
    public class Helper
    {
        
        public  static Uri GetAbsoluteUrl(HttpRequest request)
        {
            return new Uri(new StringBuilder()
                                    .Append(request.Scheme)
                                    .Append("://")
                                    .Append(request.Host)
                                    .Append(request.PathBase)
                                    .Append(request.Path)
                                    .Append(request.QueryString)
                                    .ToString());
        }

        internal static bool IsLocalIp(ConnectionInfo connection)
        {
            var ip =  connection.RemoteIpAddress;

            return ip.ToString()=="127.0.0.1";
        }
    }
}