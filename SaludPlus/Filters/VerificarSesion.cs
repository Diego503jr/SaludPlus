using SaludPlus.Controllers;
using SaludPlus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace SaludPlus.Filters
{
    public class VerificarSesion : ActionFilterAttribute
    {

        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            try
            {
                base.OnActionExecuting(filterContext);

                var oUsuario = (Usuarios)HttpContext.Current.Session["User"];

                if (oUsuario == null)
                {
                    if (!(filterContext.Controller is LoginController))
                    {
                        filterContext.Result = new RedirectResult("~/Login/Login");
                    }
                }
            }
            catch (Exception)
            {
                filterContext.Result = new RedirectResult("~/Login/Login");
            }
        }
    }
}