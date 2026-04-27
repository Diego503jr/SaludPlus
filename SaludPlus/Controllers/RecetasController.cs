using SaludPlus.Models;
using System;
using System.Linq;
using System.Web.Mvc;

namespace SaludPlus.Controllers
{
    public class RecetasController : Controller
    {
        private SaludPlussEntities1 db = new SaludPlussEntities1();

        // 1. ABRIR LA PANTALLA PRINCIPAL
        public ActionResult Index()
        {
            return View();
        }

        // 2. LISTAR LAS RECETAS PARA LA TABLA AJAX
        [HttpGet]
        public JsonResult Listar()
        {
            try
            {
                var lista = db.Recetas
                    .Select(r => new
                    {
                        r.RecetaID,
                        // Traemos los nombres cruzando las tablas
                        PacienteNombre = r.Pacientes.Nombres + " " + r.Pacientes.Apellidos,
                        MedicoNombre = r.Medicos.Usuarios.Nombres + " " + r.Medicos.Usuarios.Apellidos,
                        r.FechaEmision,
                        r.Estado,
                        // Contamos cuántos medicamentos diferentes tiene esta receta
                        TotalMedicamentos = db.DetalleReceta.Count(d => d.RecetaID == r.RecetaID)
                    })
                    .OrderByDescending(r => r.RecetaID) // Las más recientes primero
                    .ToList();

                return Json(lista, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, mensaje = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // 3. OBTENER LOS DETALLES DE UNA RECETA ESPECÍFICA (Para el Modal)
        [HttpGet]
        public JsonResult ConsultarDetalle(int id)
        {
            try
            {
                var receta = db.Recetas.Find(id);
                if (receta == null) return Json(new { success = false, mensaje = "Receta no encontrada" }, JsonRequestBehavior.AllowGet);

                // Preparamos el encabezado (Información general)
                var infoGeneral = new
                {
                    receta.RecetaID,
                    Paciente = receta.Pacientes.Nombres + " " + receta.Pacientes.Apellidos,
                    Medico = receta.Medicos.Usuarios.Nombres + " " + receta.Medicos.Usuarios.Apellidos,
                    Fecha = receta.FechaEmision.ToString("dd/MM/yyyy HH:mm"),
                    receta.Estado,
                    receta.Observaciones
                };

                // Preparamos el cuerpo (La lista de pastillas a entregar)
                var listaDetalles = db.DetalleReceta
                    .Where(d => d.RecetaID == id)
                    .Select(d => new
                    {
                        Medicamento = d.Medicamentos.Nombre,
                        EsComodin = d.Medicamentos.Nombre == "MEDICAMENTO EXTERNO (Solo texto)",
                        d.Dosis,
                        d.Cantidad,
                        d.Indicaciones
                    }).ToList();

                return Json(new { success = true, header = infoGeneral, detalles = listaDetalles }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, mensaje = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // 4. CAMBIAR ESTADO A "ENTREGADA"
        // 4. CAMBIAR ESTADO A "DISPENSADA"
        [HttpPost]
        public JsonResult DespacharReceta(int id)
        {
            try
            {
                var receta = db.Recetas.Find(id);
                if (receta == null) return Json(new { success = false, mensaje = "La receta no existe." });

                // Validamos con la nueva palabra
                if (receta.Estado == "Dispensada")
                    return Json(new { success = false, mensaje = "Esta receta ya fue dispensada anteriormente." });

                // AQUÍ ESTÁ LA MAGIA: Le mandamos a SQL Server la palabra exacta que espera
                receta.Estado = "Dispensada";
                db.Entry(receta).State = System.Data.Entity.EntityState.Modified;
                db.SaveChanges();

                return Json(new { success = true });
            }
            // 1. Atrapa errores si faltan campos obligatorios en el modelo
            catch (System.Data.Entity.Validation.DbEntityValidationException dbEx)
            {
                string validErrors = "";
                foreach (var validationErrors in dbEx.EntityValidationErrors)
                {
                    foreach (var validationError in validationErrors.ValidationErrors)
                    {
                        validErrors += validationError.PropertyName + ": " + validationError.ErrorMessage + "\n";
                    }
                }
                return Json(new { success = false, mensaje = "Error de validación EF: " + validErrors });
            }
            // 2. Atrapa errores directos de SQL Server (Check constraints, Foreign Keys, etc)
            catch (Exception ex)
            {
                string errorReal = ex.InnerException != null ?
                                  (ex.InnerException.InnerException != null ? ex.InnerException.InnerException.Message : ex.InnerException.Message)
                                  : ex.Message;
                return Json(new { success = false, mensaje = "Error SQL: " + errorReal });
            }
        }
    }
}