using SaludPlus.Models;
using System;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.Security;

namespace SaludPlus.Helpers
{
    public class LoginService
    {
        private SaludPlussEntities1 db = new SaludPlussEntities1();

        public object ProcesarLogin(string email, string passwordPlano)
        {
            string claveCifrada = SecurityHelper.GetSHA256(passwordPlano);

            var info = db.Usuarios
                         .Include(u => u.Roles)
                         .FirstOrDefault(u => u.Email == email
                                           && u.PasswordHash == claveCifrada);

            if (info != null)
            {
                info.UltimoAcceso = DateTime.Now;
                db.Entry(info).State = EntityState.Modified;
                db.SaveChanges();

                // Sesión web
                HttpContext.Current.Session["User"] = info;

                string nombreRol = info.Roles != null ? info.Roles.Nombre : "SinRol";

                // Cookie FormsAuthentication
                FormsAuthenticationTicket ticket = new FormsAuthenticationTicket(
                    1, info.Email, DateTime.Now,
                    DateTime.Now.AddMinutes(480),
                    false, nombreRol,
                    FormsAuthentication.FormsCookiePath
                );

                string encTicket = FormsAuthentication.Encrypt(ticket);
                HttpCookie cookie = new HttpCookie(FormsAuthentication.FormsCookieName, encTicket);
                HttpContext.Current.Response.Cookies.Add(cookie);

                return new
                {
                    success = true,
                    nombre = info.Nombres,
                    email = info.Email,
                    rol = nombreRol
                };
            }

            return new { success = false, message = "Datos incorrectos" };
        }
    }
}