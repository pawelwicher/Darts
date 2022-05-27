using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;

namespace Darts
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            config.Routes.MapHttpRoute(name: "DefaultApi1", routeTemplate: "api/{controller}/{action}/{seasonNo}", defaults: new { seasonNo = RouteParameter.Optional });
            config.Routes.MapHttpRoute(name: "DefaultApi2", routeTemplate: "api/{controller}/{action}/{seasonNo}/{playerhome}/{playeraway}/{winner}");
            config.Routes.MapHttpRoute(name: "DefaultApi3", routeTemplate: "api/{controller}/{action}/{seasonNo}/{matchType}/{playerhome}/{playeraway}/{winner}");
            config.Routes.MapHttpRoute(name: "DefaultApi4", routeTemplate: "api/{controller}/{action}/{message}");
        }
    }
}
