using System.Collections.Generic;
using System;
using System.Data;
using System.Data.SqlClient;
using System.Web.Mvc;
using Subasta.Models;
using System.Data.SqlTypes;

namespace Subasta.Controllers
{
    public class HomeController : Controller
    {
        private readonly string connectionString;

        public HomeController()
        {
            // Obtener la cadena de conexión desde la clase LibroV
            LibroV libroV = new LibroV();
            connectionString = libroV.connectionString;
        }

        // GET: Home/Login
        public ActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public ActionResult Login(LoginViewModel model)
        {
            string procedimientoAlmacenado = "ValidarCredenciales";

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                using (SqlCommand command = new SqlCommand(procedimientoAlmacenado, connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@CorreoElectronico", model.CorreoElectronico);
                    command.Parameters.AddWithValue("@Contraseña", model.Contraseña);

                    connection.Open();

                    string resultado = (string)command.ExecuteScalar();

                    connection.Close();

                    if (resultado == "OK")
                    {
                        Session["UserID"] = ObtenerUserIDPorCorreoElectronico(model.CorreoElectronico);
                        return RedirectToAction("Index");
                    }
                    else
                    {
                        ViewBag.Error = "Correo electrónico o contraseña incorrectos";
                        return View(model);
                    }
                }
            }
        }

        private int ObtenerUserIDPorCorreoElectronico(string correoElectronico)
        {
            int userID = 0;

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string procedimientoAlmacenado = "ObtenerUserIDPorCorreoElectronico";

                using (SqlCommand command = new SqlCommand(procedimientoAlmacenado, connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@CorreoElectronico", correoElectronico);

                    connection.Open();
                    object result = command.ExecuteScalar();

                    if (result != null)
                    {
                        userID = Convert.ToInt32(result);
                    }
                }
            }

            return userID;
        }


        public ActionResult Index()
        {
            return ObtenerSubastasDesdeBD();
        }

        public ActionResult ObtenerSubastasDesdeBD()
        {
            // Lista para almacenar las subastas
            var subastas = new List<InicioViewModel>();

            // Obtener la cadena de conexión desde la clase LibroV
            LibroV libroV = new LibroV();
            string connectionString = libroV.connectionString;

            // Consulta SQL para obtener las subastas activas y actualizar el estado si es necesario
            string query = @"
        UPDATE Subastas
        SET Estado = 'Finalizado'
        WHERE FechaFin <= GETDATE() AND Estado = 'Activa';

        SELECT * FROM Subastas WHERE Estado = 'Activa';
    ";

            // Establecer la conexión a la base de datos y ejecutar la consulta
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    connection.Open();
                    SqlDataReader reader = command.ExecuteReader();

                    // Leer los resultados y agregarlos a la lista de subastas
                    while (reader.Read())
                    {
                        subastas.Add(new InicioViewModel
                        {
                            SubastaID = Convert.ToInt32(reader["SubastaID"]),
                            Titulo = reader["Titulo"].ToString(),
                            Descripcion = reader["Descripcion"].ToString(),
                            PrecioActual = Convert.ToDecimal(reader["PrecioActual"]),
                            FechaInicio = Convert.ToDateTime(reader["FechaInicio"]),
                            FechaFin = Convert.ToDateTime(reader["FechaFin"]),
                            Estado = reader["Estado"].ToString(),
                            UserID = Convert.ToInt32(reader["UserID"]),
                            ImagenProducto = reader["ImagenProducto"].ToString()
                        });
                    }

                    reader.Close();
                }
            }
            return View(subastas);
        }



        public ActionResult Registro()
        {
            return View();
        }


        [HttpPost]
        public ActionResult Registro(RegistroViewModel model)
        {
            if (ModelState.IsValid)
            {
                string procedimientoAlmacenado = "RegistrarUsuario";

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    using (SqlCommand command = new SqlCommand(procedimientoAlmacenado, connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.AddWithValue("@Nombre", model.Nombre);
                        command.Parameters.AddWithValue("@Apellido", model.Apellido);
                        command.Parameters.AddWithValue("@CorreoElectronico", model.CorreoElectronico);
                        command.Parameters.AddWithValue("@Contraseña", model.Contraseña);

                        connection.Open();
                        SqlDataReader reader = command.ExecuteReader();

                        if (reader.Read())
                        {
                            ViewBag.Mensaje = reader["Mensaje"].ToString();

                            if (ViewBag.Mensaje == "Usuario registrado correctamente")
                            {
                                return RedirectToAction("Login");
                            }
                        }
                        reader.Close();
                    }
                }
            }

            return View(model);
        }

        public ActionResult Pujar(int subastaID)
        {
            // Verificar si la sesión contiene el ID de usuario
            if (Session["UserID"] != null)
            {
                // Obtener el ID de usuario de la sesión
                int userID = (int)Session["UserID"];

                // Llamar al método para obtener los detalles de la subasta desde la base de datos
                PujarViewModel subasta = ObtenerDetallesSubastaDesdeBD(subastaID);

                // Verificar si se encontraron los detalles de la subasta
                if (subasta != null)
                {
                    // Si se encontraron los detalles de la subasta, pasarlos a la vista Pujar
                    return View(subasta);
                }
                else
                {
                    // Si no se encontraron los detalles de la subasta, redirigir a una página de error o realizar otra acción
                    return RedirectToAction("Error");
                }
            }
            else
            {
                // Si no se encontró el ID de usuario en la sesión, redirigir al usuario a la página de inicio de sesión
                return RedirectToAction("Login");
            }
        }

        [HttpPost]
        public ActionResult Pujar(int subastaID, decimal monto)
        {
            // Verificar si la sesión contiene el ID de usuario
            if (Session["UserID"] != null)
            {
                // Obtener el ID de usuario de la sesión
                int userID = (int)Session["UserID"];

                // Llamar al procedimiento almacenado para registrar la puja
                RegistrarPuja(subastaID, userID, monto, DateTime.Now);

                // Llamar al procedimiento almacenado para actualizar el precio actual
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    using (SqlCommand command = new SqlCommand("ActualizarPrecioActual", connection))
                    {
                        command.CommandType = System.Data.CommandType.StoredProcedure;

                        // Parámetros del procedimiento almacenado
                        command.Parameters.AddWithValue("@SubastaID", subastaID);
                        command.Parameters.AddWithValue("@NuevoPrecio", monto);

                        // Ejecutar el procedimiento almacenado
                        command.ExecuteNonQuery();
                    }
                }

                // Redirigir a la pantalla de inicio u otra página
                return RedirectToAction("Index");
            }
            else
            {
                // Si no se encontró el ID de usuario en la sesión, redirigir al usuario a la página de inicio de sesión
                return RedirectToAction("Login");
            }
        }



        private PujarViewModel ObtenerDetallesSubastaDesdeBD(int subastaID)
        {
            string query = "SELECT * FROM Subastas WHERE SubastaID = @SubastaID";

            // Objeto para almacenar los detalles de la subasta
            PujarViewModel subasta = null;

            // Establecer la conexión a la base de datos y ejecutar la consulta
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    // Pasar el parámetro SubastaID a la consulta SQL
                    command.Parameters.AddWithValue("@SubastaID", subastaID);

                    connection.Open();
                    SqlDataReader reader = command.ExecuteReader();

                    // Verificar si se encontraron resultados
                    if (reader.Read())
                    {
                        // Mapear los resultados a un objeto PujarViewModel
                        subasta = new PujarViewModel
                        {
                            SubastaID = Convert.ToInt32(reader["SubastaID"]),
                            Titulo = reader["Titulo"].ToString(),
                            Descripcion = reader["Descripcion"].ToString(),
                            PrecioActual = Convert.ToDecimal(reader["PrecioActual"]),
                            FechaInicio = Convert.ToDateTime(reader["FechaInicio"]),
                            FechaFin = Convert.ToDateTime(reader["FechaFin"]),
                            Estado = reader["Estado"].ToString(),
                            UserID = Convert.ToInt32(reader["UserID"]),
                            ImagenProducto = reader["ImagenProducto"].ToString()
                        };
                    }

                    reader.Close();
                }
            }

            return subasta;
        }

        private void RegistrarPuja(int subastaID, int userID, decimal monto, DateTime fechaHora)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                using (SqlCommand command = new SqlCommand("RegistrarPuja", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;

                    // Pasar los parámetros al procedimiento almacenado
                    command.Parameters.AddWithValue("@SubastaID", subastaID);
                    command.Parameters.AddWithValue("@UserID", userID);
                    command.Parameters.AddWithValue("@Monto", monto);
                    command.Parameters.AddWithValue("@FechaHora", fechaHora);

                    connection.Open();
                    command.ExecuteNonQuery();
                }
            }
        }

        public ActionResult Cuenta()
        {
            if (Session["UserID"] != null)
            {
                int userID = (int)Session["UserID"];

                // Obtener información del usuario
                CuentaViewModel usuario = ObtenerInformacionUsuarioDesdeBD(userID);

                if (usuario != null)
                {
                    // Obtener subastas publicadas por el usuario
                    List<SubastaViewModel> subastasPublicadas = ObtenerSubastasPorUsuarioDesdeBD(userID);

                    // Asignar las subastas al modelo de vista del usuario
                    usuario.SubastasPublicadas = subastasPublicadas;

                    return View(usuario);
                }
            }
            return RedirectToAction("Login");
        }


        private CuentaViewModel ObtenerInformacionUsuarioDesdeBD(int userID)
        {
            CuentaViewModel usuario = null;

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string procedimientoAlmacenado = "ObtenerInformacionUsuarioPorID";

                using (SqlCommand command = new SqlCommand(procedimientoAlmacenado, connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@UserID", userID);

                    connection.Open();
                    SqlDataReader reader = command.ExecuteReader();

                    if (reader.Read())
                    {
                        usuario = new CuentaViewModel
                        {
                            Nombre = reader["Nombre"].ToString(),
                            Apellido = reader["Apellido"].ToString(),
                            CorreoElectronico = reader["CorreoElectronico"].ToString(),
                        };
                    }

                    reader.Close();
                }
            }

            return usuario;
        }

        private List<SubastaViewModel> ObtenerSubastasPorUsuarioDesdeBD(int userID)
        {
            List<SubastaViewModel> subastasPublicadas = new List<SubastaViewModel>();

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string procedimientoAlmacenado = "ObtenerSubastasPorUsuarioID";

                using (SqlCommand command = new SqlCommand(procedimientoAlmacenado, connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@UserID", userID);

                    connection.Open();
                    SqlDataReader reader = command.ExecuteReader();

                    while (reader.Read())
                    {
                        SubastaViewModel subasta = new SubastaViewModel
                        {
                            SubastaID = Convert.ToInt32(reader["SubastaID"]),
                            Titulo = reader["Titulo"].ToString(),
                            Descripcion = reader["Descripcion"].ToString(),
                            PrecioActual = Convert.ToDecimal(reader["PrecioActual"]),
                            FechaInicio = Convert.ToDateTime(reader["FechaInicio"]),
                            FechaFin = Convert.ToDateTime(reader["FechaFin"]),
                            Estado = reader["Estado"].ToString(),
                            ImagenProducto = reader["ImagenProducto"].ToString()
                        };

                        subastasPublicadas.Add(subasta);
                    }

                    reader.Close();
                }
            }

            return subastasPublicadas;
        }

        public ActionResult Perfil()
        {
            return View();
        }

        public ActionResult CambiarPerfil(PerfilViewModel perfil)
        {
            if (Session["UserID"] != null)
            {
                int userID = (int)Session["UserID"];

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    string procedimientoAlmacenado = "cambiarPerfil";

                    using (SqlCommand command = new SqlCommand(procedimientoAlmacenado, connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.AddWithValue("@UserID", userID);
                        command.Parameters.AddWithValue("@Nombre", perfil.Nombre);
                        command.Parameters.AddWithValue("@Apellido", perfil.Apellido);
                        command.Parameters.AddWithValue("@CorreoElectronico", perfil.CorreoElectronico);
                        command.Parameters.AddWithValue("@Contraseña", perfil.Contraseña);
                        command.Parameters.AddWithValue("@FotoPerfil", perfil.FotoPerfil);

                        connection.Open();
                        command.ExecuteNonQuery();
                    }
                }

                return RedirectToAction("Index", "Home");
            }
            else
            {
                return RedirectToAction("Login", "Home");
            }
        }

        public ActionResult Publicar()
        {
            return View();
        }

        [HttpPost]
        public ActionResult Publicar(PublicarViewModel publicacion)
        {
            if (Session["UserID"] != null)
            {
                int userID = (int)Session["UserID"];

                DateTime fechaInicioActual = DateTime.Now;
                if (fechaInicioActual < SqlDateTime.MinValue.Value || fechaInicioActual > SqlDateTime.MaxValue.Value)
                {
                    ModelState.AddModelError("", "La fecha de inicio está fuera del rango permitido.");
                    return View(publicacion); 
                }

                publicacion.FechaInicio = fechaInicioActual;

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    string procedimientoAlmacenado = "InsertarSubasta";

                    using (SqlCommand command = new SqlCommand(procedimientoAlmacenado, connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.AddWithValue("@Titulo", publicacion.Titulo);
                        command.Parameters.AddWithValue("@Descripcion", publicacion.Descripcion);
                        command.Parameters.AddWithValue("@PrecioActual", publicacion.PrecioActual);
                        command.Parameters.AddWithValue("@FechaInicio", publicacion.FechaInicio);
                        command.Parameters.AddWithValue("@FechaFin", publicacion.FechaFin);
                        command.Parameters.AddWithValue("@UserID", userID);
                        command.Parameters.AddWithValue("@ImagenProducto", publicacion.ImagenProducto);

                        connection.Open();
                        command.ExecuteNonQuery();
                    }
                }

                return RedirectToAction("Cuenta", "Home");
            }
            else
            {
                return RedirectToAction("Login", "Home");
            }
        }

        public ActionResult Ganador()
        {
            if (Session["UserID"] != null)
            {
                int userID = (int)Session["UserID"];
                List<GanarViewModel> subastasGanadas = new List<GanarViewModel>();

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    string procedimientoAlmacenado = "ObtenerSubastasGanadasPorUsuario";

                    using (SqlCommand command = new SqlCommand(procedimientoAlmacenado, connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.AddWithValue("@UsuarioID", userID);

                        connection.Open();
                        SqlDataReader reader = command.ExecuteReader();

                        while (reader.Read())
                        {
                            GanarViewModel ganador = new GanarViewModel
                            {
                                GanadorSubastaID = Convert.ToInt32(reader["GanadorSubastaID"]),
                                SubastaID = Convert.ToInt32(reader["SubastaID"]),
                                Titulo = reader["Titulo"].ToString(),
                                Precio = Convert.ToDecimal(reader["PrecioActual"]),
                                FechaGanado = Convert.ToDateTime(reader["FechaHoraGanador"]),
                                Usuario = reader["UsuarioNombre"].ToString(),
                                ImagenProducto = reader["ImagenProducto"].ToString()
                            };

                            subastasGanadas.Add(ganador);
                        }

                        reader.Close();
                    }
                }

                return View(subastasGanadas);
            }
            else
            {
                return RedirectToAction("Login", "Home");
            }
        }


        // Método para obtener el nombre del usuario
        private string ObtenerNombreUsuario(int userID)
        {
            string nombreUsuario = string.Empty;

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string query = "SELECT Nombre, Apellido FROM Usuarios WHERE UserID = @UserID";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@UserID", userID);
                    connection.Open();
                    SqlDataReader reader = command.ExecuteReader();

                    if (reader.Read())
                    {
                        nombreUsuario = $"{reader["Nombre"]} {reader["Apellido"]}";
                    }

                    reader.Close();
                }
            }

            return nombreUsuario;
        }

        public ActionResult Paypal()
        {
            // Verifica si hay un usuario en sesión
            if (Session["UserID"] != null)
            {
                int userID = (int)Session["UserID"];

                // Obtiene la información del usuario usando el procedimiento almacenado
                UserInfoViewModel userInfo = GetUserInfo(userID);

                decimal totalPagar = GetTotalPagar(userID);

                // Pasa el modelo UserInfoViewModel y el total a pagar a la vista
                var model = new UserInfoViewModel
                {
                    UserInfo = userInfo,
                    TotalPagar = totalPagar
                };

                // Retorna la vista con el modelo
                return View(model);
            }
            else
            {
                // Si no hay un usuario en sesión, redirige al inicio de sesión
                return RedirectToAction("Login", "Home");
            }
        }

        public ActionResult Tarjeta()
        {
            // Verifica si hay un usuario en sesión
            if (Session["UserID"] != null)
            {
                int userID = (int)Session["UserID"];

                // Obtiene la información del usuario usando el procedimiento almacenado
                UserInfoViewModel userInfo = GetUserInfo(userID);

                decimal totalPagar = GetTotalPagar(userID);

                // Pasa el modelo UserInfoViewModel y el total a pagar a la vista
                var model = new UserInfoViewModel
                {
                    UserInfo = userInfo,
                    TotalPagar = totalPagar
                };

                // Retorna la vista con el modelo
                return View(model);
            }
            else
            {
                // Si no hay un usuario en sesión, redirige al inicio de sesión
                return RedirectToAction("Login", "Home");
            }
        }

        public ActionResult Transferencia()
        {
            // Verifica si hay un usuario en sesión
            if (Session["UserID"] != null)
            {
                int userID = (int)Session["UserID"];

                // Obtiene la información del usuario usando el procedimiento almacenado
                UserInfoViewModel userInfo = GetUserInfo(userID);

                decimal totalPagar = GetTotalPagar(userID);

                // Pasa el modelo UserInfoViewModel y el total a pagar a la vista
                var model = new UserInfoViewModel
                {
                    UserInfo = userInfo,
                    TotalPagar = totalPagar
                };

                // Retorna la vista con el modelo
                return View(model);
            }
            else
            {
                // Si no hay un usuario en sesión, redirige al inicio de sesión
                return RedirectToAction("Login", "Home");
            }
        }





        // Método para obtener la información del usuario desde la base de datos
        private UserInfoViewModel GetUserInfo(int userID)
        {
            UserInfoViewModel userInfo = new UserInfoViewModel();

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string storedProcedure = "ObtenerInformacionUsuarioPorID";
                using (SqlCommand command = new SqlCommand(storedProcedure, connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@UserID", userID);

                    connection.Open();
                    SqlDataReader reader = command.ExecuteReader();
                    if (reader.Read())
                    {
                        userInfo.Nombre = reader["Nombre"].ToString();
                        userInfo.Apellido = reader["Apellido"].ToString();
                        userInfo.CorreoElectronico = reader["CorreoElectronico"].ToString();
                    }
                }
            }

            return userInfo;
        }

        // Método para obtener el total a pagar del usuario desde la base de datos
        private decimal GetTotalPagar(int userID)
        {
            decimal totalPagar = 0;

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string storedProcedure = "ObtenerTotalPagarPorUsuarioID";
                using (SqlCommand command = new SqlCommand(storedProcedure, connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@UsuarioID", userID);

                    connection.Open();
                    object result = command.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                    {
                        totalPagar = Convert.ToDecimal(result);
                    }
                }
            }

            return totalPagar;
        }

    }
}
