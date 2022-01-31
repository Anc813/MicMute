using Owin;
using System;
using System.Web.Http;

namespace MicMute
{
    public class Startup
    {

        public void Configuration(IAppBuilder app)
        {
            app.UseWelcomePage("/");

            HttpConfiguration config = new HttpConfiguration();
            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );

            app.UseWebApi(config);

            config.Formatters.Remove(config.Formatters.XmlFormatter);
        }

    }
}
