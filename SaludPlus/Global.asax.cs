using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using System.Web.Security;
using System.Web.Http;

namespace SaludPlus
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();

            GlobalConfiguration.Configure(WebApiConfig.Register);
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
        }

        protected void Application_PostAuthenticateRequest(Object sender, EventArgs e)
        {

            if (IsWebApiRequest())
            {
                HttpContext.Current.SetSessionStateBehavior(
                    System.Web.SessionState.SessionStateBehavior.Required);
            }

            HttpCookie authCookie = Request.Cookies[FormsAuthentication.FormsCookieName];
            if (authCookie != null)
            {
                
                FormsAuthenticationTicket authTicket = FormsAuthentication.Decrypt(authCookie.Value);

                if (authTicket != null && !authTicket.Expired)
                {
                
                    string[] roles = new string[] { authTicket.UserData };

                
                    System.Security.Principal.GenericIdentity id = new System.Security.Principal.GenericIdentity(authTicket.Name, "Forms");
                    System.Security.Principal.GenericPrincipal principal = new System.Security.Principal.GenericPrincipal(id, roles);

                    Context.User = principal;
                }
            }
        }

        private bool IsWebApiRequest()
        {
            return HttpContext.Current.Request.AppRelativeCurrentExecutionFilePath
                             .StartsWith("~/api");
        }

        protected void Application_Error(object sender, EventArgs e)
        {
            Exception ex = Server.GetLastError();
            if (ex != null)
            {
                // Verás el error exacto en la ventana de Output de Visual Studio
                System.Diagnostics.Debug.WriteLine("ERROR: " + ex.Message);
                System.Diagnostics.Debug.WriteLine("STACK: " + ex.StackTrace);
                if (ex.InnerException != null)
                    System.Diagnostics.Debug.WriteLine("INNER: " + ex.InnerException.Message);
            }
        }
    }
}
