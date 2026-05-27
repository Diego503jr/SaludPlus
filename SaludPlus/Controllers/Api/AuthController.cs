using SaludPlus.Helpers;
using System;
using System.Web;
using System.Web.Http;
using System.Web.Security;

namespace SaludPlus.Controllers.Api
{
    [RoutePrefix("api/auth")]
    public class AuthController : ApiController
    {
        [HttpPost]
        [Route("login")]
        public IHttpActionResult Login([FromBody] LoginRequest request)
        {
            try
            {
                // ← Verificar null primero antes de acceder a propiedades
                if (request == null)
                    return BadRequest("El cuerpo de la petición es requerido.");

                if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.PasswordHash))
                    return BadRequest("Email y contraseña son requeridos.");

                var service = new LoginService();
                var resultado = service.ProcesarLogin(request.Email, request.PasswordHash);
                return Ok(resultado);
            }
            catch (Exception ex)
            {
                // Retorna el error completo para depuración
                return InternalServerError(new Exception(ex.Message + " | INNER: " + ex.InnerException?.Message));
            }
        }

        [HttpPost]
        [Route("logout")]
        public IHttpActionResult Logout()
        {
            try
            {
                if (HttpContext.Current != null)
                {
                    // Limpiar sesión
                    HttpContext.Current.Session?.Clear();
                    HttpContext.Current.Session?.Abandon();

                    // Limpiar cookie de FormsAuthentication
                    FormsAuthentication.SignOut();

                    // Eliminar la cookie manualmente para forzar expiración
                    HttpCookie cookie = new HttpCookie(FormsAuthentication.FormsCookieName, "")
                    {
                        Expires = DateTime.Now.AddYears(-1)
                    };
                    HttpContext.Current.Response.Cookies.Add(cookie);
                }

                return Ok(new { success = true, message = "Sesión cerrada correctamente" });
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception(ex.Message + " | INNER: " + ex.InnerException?.Message));
            }
        }
    }

    public class LoginRequest
    {
        public string Email { get; set; }
        public string PasswordHash { get; set; } // ← usa PasswordHash
    }
}