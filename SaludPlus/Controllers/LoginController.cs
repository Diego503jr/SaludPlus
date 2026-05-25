using SaludPlus.Helpers;
using SaludPlus.Models;
using System;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Security; 

namespace SaludPlus.Controllers
{
    [AllowAnonymous]
    public class LoginController : Controller
    {
        private SaludPlussEntities1 db = new SaludPlussEntities1();

        public ActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public JsonResult ValidarUsuario(Usuarios usuario)
        {
            string claveCifrada = SecurityHelper.GetSHA256(usuario.PasswordHash);

            var info = db.Usuarios
                         .Include(u => u.Roles)
                         .FirstOrDefault(u => u.Email == usuario.Email && u.PasswordHash == claveCifrada);

            if (info != null)
            {
                info.UltimoAcceso = DateTime.Now;
                db.Entry(info).State = EntityState.Modified;
                db.SaveChanges();

                Session["User"] = info;

                string nombreRol = info.Roles != null ? info.Roles.Nombre : "SinRol";

                FormsAuthenticationTicket ticket = new FormsAuthenticationTicket(
                    1,
                    info.Email,                            
                    DateTime.Now,                          
                    DateTime.Now.AddMinutes(480),          
                    false,                                 
                    nombreRol,                              
                    FormsAuthentication.FormsCookiePath
                );

               
                string encTicket = FormsAuthentication.Encrypt(ticket);
                HttpCookie cookie = new HttpCookie(FormsAuthentication.FormsCookieName, encTicket);
                Response.Cookies.Add(cookie);

                return Json(new { success = true, url = Url.Action("Index", "Home") });
            }

            return Json(new { success = false, message = "Datos incorrectos" });
        }

        public ActionResult Logout()
        {
           
            FormsAuthentication.SignOut();

           
            Session.Clear();
            Session.Abandon();
            return RedirectToAction("Login", "Login");
        }
    }
}