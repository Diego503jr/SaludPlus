using SaludPlus.Models;
using System;
using System.Linq;
using System.Web.Mvc;

namespace SaludPlus.Controllers
{
    public class RecetasController : Controller
    {
        private SaludPlussEntities1 db = new SaludPlussEntities1();

        public ActionResult Index()
        {
            return View();
        }

        //LISTAR LAS RECETAS PARA LA TABLA AJAX
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

        //OBTENER LOS DETALLES DE UNA RECETA ESPECÍFICA
        [HttpGet]
        public JsonResult ConsultarDetalle(int id)
        {
            try
            {
                var receta = db.Recetas.Find(id);
                if (receta == null) return Json(new { success = false, mensaje = "Receta no encontrada" }, JsonRequestBehavior.AllowGet);

                var infoGeneral = new
                {
                    receta.RecetaID,
                    Paciente = receta.Pacientes.Nombres + " " + receta.Pacientes.Apellidos,
                    Medico = receta.Medicos.Usuarios.Nombres + " " + receta.Medicos.Usuarios.Apellidos,
                    Fecha = receta.FechaEmision.ToString("dd/MM/yyyy HH:mm"),
                    receta.Estado,
                    receta.Observaciones,
                };

                var listaDetalles = db.DetalleReceta
                    .Where(d => d.RecetaID == id)
                    .Select(d => new
                    {
                        d.DetalleID,
                        Medicamento = d.Medicamentos.Nombre,
                        EsComodin = d.Medicamentos.Nombre == "MEDICAMENTO EXTERNO (Solo texto)",
                        d.Dosis,
                        d.Cantidad,
                        d.Indicaciones,
                        d.Estado
                    }).ToList();

                return Json(new { success = true, header = infoGeneral, detalles = listaDetalles }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, mensaje = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // DESPACHAR POR MEDICAMENTO INDIVIDUAL
        [HttpPost]
        public JsonResult DespacharDetalle(int idDetalle, int idReceta)
        {
            try
            {
                var detalle = db.DetalleReceta.Find(idDetalle);
                if (detalle == null) return Json(new { success = false, mensaje = "El medicamento no existe en la receta." });

                //Marcamos solo este detalle como entregado
                detalle.Estado = true;
                db.Entry(detalle).State = System.Data.Entity.EntityState.Modified;
                db.SaveChanges();

                // Buscamos si existe ALGÚN detalle de esta receta que NO esté dispensado
                bool quedanPendientes = db.DetalleReceta.Any(d => d.RecetaID == idReceta && (d.Estado == null || d.Estado != true));

                bool recetaCompletadaAl100 = false;

                // Si ya no hay pendientes, actualizamos la Receta principal a Dispensada
                if (!quedanPendientes)
                {
                    var recetaMaestra = db.Recetas.Find(idReceta);
                    if (recetaMaestra != null && recetaMaestra.Estado != "Dispensada")
                    {
                        recetaMaestra.Estado = "Dispensada";
                        db.Entry(recetaMaestra).State = System.Data.Entity.EntityState.Modified;
                        db.SaveChanges();
                        recetaCompletadaAl100 = true; // Avisamos a la vista que ya terminó
                    }
                }

                return Json(new { success = true, completada = recetaCompletadaAl100 });
            }
            catch (Exception ex)
            {
                string errorReal = ex.InnerException != null ? (ex.InnerException.InnerException != null ? ex.InnerException.InnerException.Message : ex.InnerException.Message) : ex.Message;
                return Json(new { success = false, mensaje = "Error SQL: " + errorReal });
            }
        }
    }
}