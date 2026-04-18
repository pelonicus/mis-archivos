using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Linq;
using Windows.Security.Credentials.UI;

namespace ValmerPasswordsDB
{
    public partial class MainWindow : Window
    {
        private bool _accesoConcedido = false;
        private XDocument? _xmlData;
        private const string MasterKey = "V4lmerSys_Secur1ty_Key_2026_Auth!";
        private XElement? _grupoSeleccionadoParaNuevo;
        private XElement? _servidorEditando;
        private Dictionary<TreeViewItem, XElement> _nodosServidores = new();

        private string _passwordDesencriptadaActual = "";
        private System.Windows.Threading.DispatcherTimer _lockRenewTimer;

        // Rutas
        public class LlaveTemporal
        {
            public string RutaOrigen { get; set; }
            public string NombreOriginal { get; set; }
            public string Comentario { get; set; }
            public string Fecha { get; set; }
            public string Usuario { get; set; }
            public string NombreFisico { get; set; } // Nombre en la carpeta /keys/
        }
        private LlaveTemporal MostrarPopupNuevaLlave()
        {
            LlaveTemporal resultado = null;

            Window win = new Window
            {
                Title = "Configurar Llave SSH",
                Width = 400,
                Height = 280,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.ToolWindow,
                Background = Brushes.White
            };

            StackPanel sp = new StackPanel { Margin = new Thickness(20) };

            // --- SELECCIÓN DE ARCHIVO ---
            sp.Children.Add(new TextBlock { Text = "Seleccione el archivo de llave:", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 5) });

            Grid gridArchivo = new Grid { Margin = new Thickness(0, 0, 0, 15) };
            gridArchivo.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            gridArchivo.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBox txtRuta = new TextBox { IsReadOnly = true, Height = 30, VerticalContentAlignment = VerticalAlignment.Center, Background = (Brush)new BrushConverter().ConvertFrom("#F3F4F6") };
            Button btnBuscar = new Button { Content = "Buscar...", Width = 75, Margin = new Thickness(5, 0, 0, 0), Cursor = Cursors.Hand };

            btnBuscar.Click += (s, e) => {
                var ofd = new Microsoft.Win32.OpenFileDialog();
                if (ofd.ShowDialog() == true)
                {
                    txtRuta.Text = ofd.FileName;
                }
            };

            Grid.SetColumn(txtRuta, 0);
            Grid.SetColumn(btnBuscar, 1);
            gridArchivo.Children.Add(txtRuta);
            gridArchivo.Children.Add(btnBuscar);
            sp.Children.Add(gridArchivo);

            // --- COMENTARIO ---
            sp.Children.Add(new TextBlock { Text = "Comentario / Descripción:", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 5) });
            TextBox txtComentario = new TextBox { Height = 30, VerticalContentAlignment = VerticalAlignment.Center };
            sp.Children.Add(txtComentario);

            // --- BOTÓN AGREGAR ---
            Button btnFinal = new Button
            {
                Content = "Agregar Llave",
                Height = 40,
                Margin = new Thickness(0, 25, 0, 0),
                Background = (Brush)new BrushConverter().ConvertFrom("#4F46E5"),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                Cursor = Cursors.Hand,
                IsDefault = true
            };

            btnFinal.Click += (s, e) => {
                if (string.IsNullOrWhiteSpace(txtRuta.Text))
                {
                    //MessageBox.Show("Por favor, seleccione un archivo de llave.");
                    MostrarToast("Por favor, seleccione un archivo de llave.");
                    return;
                }
                if (string.IsNullOrWhiteSpace(txtComentario.Text))
                {
                    //MessageBox.Show("El comentario es obligatorio.");
                    MostrarToast("El comentario es obligatorio.");
                    return;
                }

                resultado = new LlaveTemporal
                {
                    RutaOrigen = txtRuta.Text,
                    NombreOriginal = System.IO.Path.GetFileName(txtRuta.Text),
                    Comentario = txtComentario.Text,
                    Fecha = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    Usuario = Environment.UserName
                };
                win.DialogResult = true;
            };

            sp.Children.Add(btnFinal);
            win.Content = sp;

            return (win.ShowDialog() == true) ? resultado : null;
        }

        // Esta lista guardará las llaves mientras editamos
        private List<LlaveTemporal> _llavesTemporales = new List<LlaveTemporal>();
        private string GetXmlPath()
        {
            string? od = Environment.GetEnvironmentVariable("OneDriveCommercial") ?? Environment.GetEnvironmentVariable("OneDrive");
            if (string.IsNullOrEmpty(od)) return "";
            return Path.Combine(od, "General - Servicios", "Softwares", "ValmerSystem", "ValmerPasswordsDB", "passwordsdb.xml");
        }

        private string GetLockPath()
        {
            string xmlPath = GetXmlPath();
            if (string.IsNullOrEmpty(xmlPath)) return "";
            return xmlPath.Replace(".xml", ".lock");
        }

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            Closed += MainWindow_Closed;

            _lockRenewTimer = new System.Windows.Threading.DispatcherTimer();
            _lockRenewTimer.Interval = TimeSpan.FromMinutes(4);
            _lockRenewTimer.Tick += LockRenewTimer_Tick;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (await IniciarSeguridad())
            {
                _accesoConcedido = true;
                PanelBloqueo.Visibility = Visibility.Collapsed;
                PanelPrincipal.Visibility = Visibility.Visible;
                CargarDatosXML();
            }
            else { Close(); }
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            _lockRenewTimer.Stop();
            LiberarLock();
        }

        private async Task<bool> IniciarSeguridad()
        {
            try
            {
                var disp = await UserConsentVerifier.CheckAvailabilityAsync();
                if (disp == UserConsentVerifierAvailability.Available)
                {
                    var res = await UserConsentVerifier.RequestVerificationAsync("Acceso a ValmerPasswordsDB");
                    return res == UserConsentVerificationResult.Verified;
                }
                return true;
            }
            catch { return true; }
        }

        private void CargarDatosXML()
        {
            try
            {
                string ruta = GetXmlPath();
                if (string.IsNullOrEmpty(ruta) || !File.Exists(ruta)) return;

                using (FileStream fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    _xmlData = XDocument.Load(fs);
                }

                ArbolServidores.Items.Clear();
                _nodosServidores.Clear();

                foreach (var gXml in _xmlData.Descendants("Grupo"))
                {
                    var grid = new Grid { Width = 260 };
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    var txt = new TextBlock { Text = gXml.Attribute("nombre")?.Value, VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.Bold };
                    var btn = new Button { Content = "+", Width = 22, Height = 22, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Cursor = Cursors.Hand, Tag = gXml };
                    btn.Click += BtnAddServidorDesdeGrupo_Click;
                    Grid.SetColumn(txt, 0);
                    Grid.SetColumn(btn, 1);
                    grid.Children.Add(txt);
                    grid.Children.Add(btn);
                    var nodoG = new TreeViewItem { Header = grid };
                    foreach (var sXml in gXml.Descendants("Servidor"))
                    {
                        string nombre = sXml.Attribute("nombre")?.Value;
                        string tipo = sXml.Attribute("tipo")?.Value;
                        string detalle = (tipo == "WEB") ? sXml.Attribute("url")?.Value : sXml.Attribute("ip")?.Value;
                        var nS = new TreeViewItem { Header = $"{nombre} ({detalle})" };
                        _nodosServidores.Add(nS, sXml);
                        nodoG.Items.Add(nS);
                    }
                    nodoG.IsExpanded = true;
                    ArbolServidores.Items.Add(nodoG);
                }
            }
            catch (Exception ex) { MostrarToast(ex.Message); }
        }

        private void ComboTipo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TxtNombre == null) return;
            string t = (ComboTipo.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "WEB";
            PanelIP.Visibility = (t == "WEB") ? Visibility.Collapsed : Visibility.Visible;
            PanelURL.Visibility = (t == "WEB" || t == "HARDWARE" || t == "VPN") ? Visibility.Visible : Visibility.Collapsed;
            PanelDominio.Visibility = (t == "RDP" || t == "WINDOWS") ? Visibility.Visible : Visibility.Collapsed;
            PanelLlave.Visibility = (t == "SSH" || t == "SFTP") ? Visibility.Visible : Visibility.Collapsed;
            PanelHardwareTipo.Visibility = (t == "HARDWARE") ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ArbolServidores_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (ArbolServidores.SelectedItem is TreeViewItem n && _nodosServidores.ContainsKey(n))
            {
                _servidorEditando = _nodosServidores[n];
                _grupoSeleccionadoParaNuevo = null;
                MostrarDatosEnModoVer(_servidorEditando);
            }
        }

        private void MostrarDatosEnModoVer(XElement s)
        {
            string ipUrl = string.IsNullOrEmpty(s.Attribute("ip")?.Value) ? s.Attribute("url")?.Value : s.Attribute("ip")?.Value;
            LblVerTitulo.Text = $"{s.Attribute("nombre")?.Value} ({ipUrl})";
            LblVerTipo.Text = s.Attribute("tipo")?.Value;
            LblVerURL.Text = s.Attribute("url")?.Value ?? "-";
            PanelVerURL.Visibility = string.IsNullOrEmpty(s.Attribute("url")?.Value) ? Visibility.Collapsed : Visibility.Visible;
            var dom = s.Attribute("dominio")?.Value;
            var hw = s.Attribute("hw_tipo")?.Value;
            LblVerDominio.Text = !string.IsNullOrEmpty(dom) ? dom : (!string.IsNullOrEmpty(hw) ? hw : "-");
            PanelVerDominio.Visibility = LblVerDominio.Text == "-" ? Visibility.Collapsed : Visibility.Visible;

            // Nombre y password del usuario principal
            var usuarioPrincipal = s.Element("Usuario");
            LblVerUsuario.Text = usuarioPrincipal?.Attribute("nombre")?.Value ?? "-";
            var p = usuarioPrincipal?.Elements("Password").FirstOrDefault(x => x.Attribute("activa")?.Value == "true");
            _passwordDesencriptadaActual = Desencriptar(p?.Value ?? "");
            TxtVerPasswordReal.Text = _passwordDesencriptadaActual;
            TxtVerPasswordOculta.Text = string.IsNullOrEmpty(_passwordDesencriptadaActual) ? "-" : "••••••••";
            // PEGAR ESTO EN SU LUGAR:
            CargarLlavesEnModoVer(s);
            LblVerComentarios.Text = s.Element("Comentario")?.Value ?? "-";

            // --- Lógica para usuarios adicionales ---
            ContenedorUsuariosAdicionalesLectura.Children.Clear();
            var listaUsuarios = s.Elements("Usuario").ToList();

            // Empezamos desde el segundo usuario (índice 1) porque el primero ya se muestra arriba
            if (listaUsuarios.Count > 1)
            {
                for (int i = 1; i < listaUsuarios.Count; i++)
                {
                    var usuarioAd = listaUsuarios[i];
                    string nom = usuarioAd.Attribute("nombre")?.Value ?? "-";
                    string pEnc = usuarioAd.Element("Password")?.Value ?? "";
                    string pDec = Desencriptar(pEnc);

                    AgregarFilaUsuarioAdicional(nom, pDec);
                }
            }

            RenderizarHistorial(s);
            ScrollModoVer.Visibility = Visibility.Visible;
            ScrollModoEdicion.Visibility = Visibility.Collapsed;
            TxtMensajeVacio.Visibility = Visibility.Collapsed;
        }

        private void RenderizarHistorial(XElement s)
        {
            ContenedorHistorial.Children.Clear();
            var h = s.Element("Historial");
            if (h == null || !h.Elements("Evento").Any())
            {
                ContenedorHistorial.Children.Add(new TextBlock { Text = "Sin historial.", FontSize = 11, Foreground = Brushes.Gray });
                return;
            }

            foreach (var ev in h.Elements("Evento"))
            {
                var g = new Grid { Margin = new Thickness(0, 0, 0, 4) };
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(115) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var tF = new TextBlock { Text = ev.Attribute("fecha")?.Value, FontSize = 10, Foreground = Brushes.DimGray, VerticalAlignment = VerticalAlignment.Center };
                var tU = new TextBlock { Text = ev.Attribute("autor")?.Value, FontSize = 10, Foreground = Brushes.Gray, VerticalAlignment = VerticalAlignment.Center };

                var spContent = new StackPanel { Orientation = Orientation.Horizontal };
                var tA = new TextBlock { Text = ev.Attribute("accion")?.Value, FontSize = 11, FontWeight = FontWeights.Medium, VerticalAlignment = VerticalAlignment.Center };
                spContent.Children.Add(tA);

                string pAnt = ev.Attribute("passwordAnterior")?.Value;
                if (!string.IsNullOrEmpty(pAnt))
                {
                    var tPass = new TextBlock { Text = "••••••••", FontSize = 12, FontFamily = new FontFamily("Consolas"), VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.Gray, Margin = new Thickness(20, 0, 5, 0) };
                    var btnO = new Button { Content = "👁", FontSize = 10, Width = 25, Height = 18, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
                    string passReal = Desencriptar(pAnt);
                    btnO.PreviewMouseDown += (sender, e) => { tPass.Text = passReal; tPass.Foreground = Brushes.DarkRed; };
                    btnO.PreviewMouseUp += (sender, e) => { tPass.Text = "••••••••"; tPass.Foreground = Brushes.Gray; };
                    btnO.MouseLeave += (sender, e) => { tPass.Text = "••••••••"; tPass.Foreground = Brushes.Gray; };
                    spContent.Children.Add(tPass);
                    spContent.Children.Add(btnO);
                }

                Grid.SetColumn(tF, 0);
                Grid.SetColumn(tU, 1);
                Grid.SetColumn(spContent, 2);
                g.Children.Add(tF);
                g.Children.Add(tU);
                g.Children.Add(spContent);

                ContenedorHistorial.Children.Add(g);
                ContenedorHistorial.Children.Add(new Separator { Height = 1, Background = new SolidColorBrush(Color.FromRgb(240, 240, 240)), Margin = new Thickness(0, 2, 0, 2) });
            }
        }

        // ==========================================
        // SISTEMA DE BLOQUEO Y LIMPIEZA
        // ==========================================

        private bool VerificarYTomarLock()
        {
            string lockPath = GetLockPath();
            if (string.IsNullOrEmpty(lockPath)) return false;
            try
            {
                if (File.Exists(lockPath))
                {
                    string[] lines = File.ReadAllLines(lockPath);
                    if (lines.Length >= 2 && DateTime.TryParse(lines[0], out DateTime timestamp))
                    {
                        string usuarioLock = lines[1];
                        TimeSpan diff = DateTime.Now - timestamp;
                        if (usuarioLock == Environment.UserName)
                        {
                            File.WriteAllLines(lockPath, new string[] { DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), Environment.UserName });
                            return true;
                        }
                        if (diff.TotalMinutes < 5)
                        {
                            MostrarToast($"Registro ocupado por {usuarioLock}.\nInténtalo más tarde.");
                            return false;
                        }
                    }
                }
                File.WriteAllLines(lockPath, new string[] { DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), Environment.UserName });
                return true;
            }
            catch { return false; }
        }

        private void LiberarLock()
        {
            string lockPath = GetLockPath();
            try
            {
                if (File.Exists(lockPath))
                {
                    string[] lines = File.ReadAllLines(lockPath);
                    if (lines.Length >= 2 && lines[1] == Environment.UserName) File.Delete(lockPath);
                }
            }
            catch { }
        }

        private bool ValidarLockParaGuardar()
        {
            string lockPath = GetLockPath();
            if (!File.Exists(lockPath)) return false;
            try
            {
                string[] lines = File.ReadAllLines(lockPath);
                if (lines.Length >= 2 && DateTime.TryParse(lines[0], out DateTime timestamp))
                {
                    if (lines[1] == Environment.UserName)
                    {
                        File.WriteAllLines(lockPath, new string[] { DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), Environment.UserName });
                        return true;
                    }
                    if ((DateTime.Now - timestamp).TotalMinutes > 5) return false;
                }
                return false;
            }
            catch { return false; }
        }

        private void LockRenewTimer_Tick(object sender, EventArgs e)
        {
            string lockPath = GetLockPath();
            if (File.Exists(lockPath))
            {
                try
                {
                    string[] lines = File.ReadAllLines(lockPath);
                    if (lines.Length >= 2 && lines[1] == Environment.UserName)
                    {
                        File.WriteAllLines(lockPath, new string[] { DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), Environment.UserName });
                    }
                }
                catch { }
            }
        }

        // --- RUTINA 1: LIMPIEZA DE FORMULARIO ---
        private void LimpiarFormularioUsuarios()
        {
            // Borra controles generados dinámicamente dejando solo el Index 0 (GridUsuarioBase)
            while (ContenedorListaUsuarios.Children.Count > 1)
            {
                ContenedorListaUsuarios.Children.RemoveAt(1);
            }

            // Resetea campos base
            TxtNombre.Text = TxtIP.Text = TxtURL.Text = TxtComentario.Text = TxtDominio.Text = TxtHardwareTipo.Text =  "";
            ComboTipo.SelectedIndex = 0;

            TxtUsuario.Text = "";
            TxtNuevoPass.Password = "";
            TxtNuevoConfirm.Password = "";
            TxtNuevoPassVisible.Text = "";
            TxtNuevoConfirmVisible.Text = "";
            _passwordDesencriptadaActual = "";

            // Estado Nuevo por defecto
            PanelPassLecturaBase.Visibility = Visibility.Collapsed;
            PanelPassEdicionBase.Visibility = Visibility.Visible;
            PanelConfirmarBase.Visibility = Visibility.Visible;
            //BtnCancelarCambioBase.Visibility = Visibility.Collapsed;
        }

        // ==========================================
        // EVENTOS PRINCIPALES (BOTONES FORMS)
        // ==========================================

        private void BtnAddServidorDesdeGrupo_Click(object sender, RoutedEventArgs e)
        {
            if (!VerificarYTomarLock()) return;

            LimpiarFormularioUsuarios(); // Limpieza antes de usar

            _grupoSeleccionadoParaNuevo = (XElement)((Button)sender).Tag;
            _servidorEditando = null;

            TxtTituloDerecho.Text = "Nuevo Servidor";
            ScrollModoVer.Visibility = Visibility.Collapsed;
            ScrollModoEdicion.Visibility = Visibility.Visible;
            TxtMensajeVacio.Visibility = Visibility.Collapsed;

            _lockRenewTimer.Start();
        }

        private void BtnActivarEdicion_Click(object sender, RoutedEventArgs e)
        {
            if (_servidorEditando == null) return;
            if (!VerificarYTomarLock()) return;

            LimpiarFormularioUsuarios(); // Limpieza antes de cargar

            TxtTituloDerecho.Text = "Editar Registro";
            CargarForm(_servidorEditando);

            ScrollModoVer.Visibility = Visibility.Collapsed;
            ScrollModoEdicion.Visibility = Visibility.Visible;

            _lockRenewTimer.Start();
        }

        private void BtnGuardarServidor_Click(object sender, RoutedEventArgs e)
        {
            // --- 1. VALIDACIONES INICIALES DE CAMPOS ---
            if (string.IsNullOrWhiteSpace(TxtNombre.Text))
            {
                MostrarToast("El campo 'Nombre' es obligatorio.");
                return;
            }

            if (_xmlData == null) return;

            string tipo = (ComboTipo.SelectedItem as ComboBoxItem)?.Content.ToString();
            if (tipo == "WEB")
            {
                if (string.IsNullOrWhiteSpace(TxtURL.Text))
                {
                    MostrarToast($"Para servidores tipo '{tipo}', la URL es obligatoria.");
                    return;
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(TxtIP.Text))
                {
                    MostrarToast($"Para el tipo '{tipo}', la IP es obligatoria.");
                    return;
                }
            }

            // --- 2. VALIDACIÓN DE LOCK ---
            if (!ValidarLockParaGuardar())
            {
                LiberarLock();
                _lockRenewTimer.Stop();
                ScrollModoEdicion.Visibility = Visibility.Collapsed;
                TxtMensajeVacio.Visibility = Visibility.Visible;
                return;
            }

            // --- 3. PREPARACIÓN DE DATOS E HISTORIAL ---
            bool esN = _servidorEditando == null;
            string uAct = Environment.UserName;
            string fH = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            if (esN)
            {
                _servidorEditando = new XElement("Servidor");
                var hNew = new XElement("Historial");
                _servidorEditando.Add(hNew);
                RegistrarEventoHistorial(hNew, "Creación de Servidor", uAct, fH);
                _grupoSeleccionadoParaNuevo?.Add(_servidorEditando);
            }

            var h = _servidorEditando.Element("Historial");
            if (h == null) { h = new XElement("Historial"); _servidorEditando.AddFirst(h); }

            // --- LÓGICA DE DETECCIÓN DE CAMBIOS EN ATRIBUTOS ---
            if (!esN)
            {
                bool huboCambioDatos = false;
                if ((_servidorEditando.Attribute("nombre")?.Value ?? "") != TxtNombre.Text) huboCambioDatos = true;
                if ((_servidorEditando.Attribute("ip")?.Value ?? "") != TxtIP.Text) huboCambioDatos = true;
                if ((_servidorEditando.Attribute("url")?.Value ?? "") != TxtURL.Text) huboCambioDatos = true;
                if ((_servidorEditando.Attribute("dominio")?.Value ?? "") != TxtDominio.Text) huboCambioDatos = true;
                if ((_servidorEditando.Attribute("hw_tipo")?.Value ?? "") != TxtHardwareTipo.Text) huboCambioDatos = true;

                // Solo si hubo cambio en los campos de texto, registramos "Modificación"
                if (huboCambioDatos)
                {
                    RegistrarEventoHistorial(h, "Modificación de Servidor", uAct, fH);
                }
            }

            // Limpiar elementos antiguos (excepto historial y usuarios que se recrearán)
            _servidorEditando.Elements().Where(x => x.Name != "Historial" && x.Name != "Usuario").Remove();

            // Seteo de atributos
            _servidorEditando.SetAttributeValue("tipo", tipo);
            _servidorEditando.SetAttributeValue("nombre", TxtNombre.Text);
            _servidorEditando.SetAttributeValue("ip", TxtIP.Text);
            _servidorEditando.SetAttributeValue("url", TxtURL.Text);
            _servidorEditando.SetAttributeValue("dominio", TxtDominio.Text);
            _servidorEditando.SetAttributeValue("hw_tipo", TxtHardwareTipo.Text);

            // --- PROCESAR LLAVES ---
            string rutaBase = System.IO.Path.GetDirectoryName(GetXmlPath());
            string carpetaKeys = System.IO.Path.Combine(rutaBase, "keys");
            if (!System.IO.Directory.Exists(carpetaKeys)) System.IO.Directory.CreateDirectory(carpetaKeys);

            foreach (var ll in _llavesTemporales)
            {
                // Solo procesamos y registramos historial si es una llave NUEVA (viene de archivo externo)
                if (!string.IsNullOrEmpty(ll.RutaOrigen))
                {
                    int totalFiles = System.IO.Directory.GetFiles(carpetaKeys, "*.key").Length + 1;
                    string nombreFisico = totalFiles.ToString("D6") + ".key";
                    string destino = System.IO.Path.Combine(carpetaKeys, nombreFisico);

                    try
                    {
                        System.IO.File.Copy(ll.RutaOrigen, destino, true);
                        ll.NombreFisico = nombreFisico;

                        // REGISTRO DE HISTORIAL ESPECÍFICO PARA LA LLAVE
                        RegistrarEventoHistorial(h, $"Nueva Llave {ll.NombreOriginal}", uAct, fH);
                    }
                    catch (Exception ex)
                    {
                        MostrarToast($"Error al copiar llave {ll.NombreOriginal}");
                    }
                }

                // Agregar siempre al XML (tanto las nuevas como las que ya estaban cargadas)
                _servidorEditando.Add(new XElement("LlaveSSH_File",
                    new XAttribute("fecha", ll.Fecha),
                    new XAttribute("usuario", ll.Usuario),
                    new XAttribute("nombre_original", ll.NombreOriginal),
                    new XAttribute("nombre_fisico", ll.NombreFisico),
                    new XElement("Comentario", ll.Comentario)
                ));
            }

            // --- 4. LECTURA Y VALIDACIÓN DE USUARIOS ---
            string pPrincipal = "";
            if (PanelPassEdicionBase.Visibility == Visibility.Visible)
            {
                if (TxtNuevoPass.Password != TxtNuevoConfirm.Password)
                {
                    MostrarToast("Las contraseñas del usuario principal no coinciden.");
                    return;
                }
                if (string.IsNullOrEmpty(TxtNuevoPass.Password))
                {
                    MostrarToast("La contraseña del usuario principal no puede estar vacía.");
                    return;
                }
                pPrincipal = TxtNuevoPass.Password;

                if (!esN && pPrincipal != _passwordDesencriptadaActual)
                {
                    RegistrarEventoHistorial(h, "Cambio de Contraseña (Principal)", uAct, fH, TxtUsuario.Text, Encriptar(_passwordDesencriptadaActual));
                }
            }
            else
            {
                pPrincipal = _passwordDesencriptadaActual;
            }

            _servidorEditando.Elements("Usuario").Remove();
            _servidorEditando.Add(new XElement("Usuario",
                new XAttribute("nombre", TxtUsuario.Text),
                new XElement("Password", Encriptar(pPrincipal), new XAttribute("activa", "true"))));

            for (int i = 0; i < ContenedorListaUsuarios.Children.Count; i++)
            {
                var gridFila = ContenedorListaUsuarios.Children[i] as Grid;
                if (gridFila == null || gridFila.Tag?.ToString() != "DINAMICO") continue;

                TextBox txtUser = FindChild<TextBox>(gridFila, "User");
                if (txtUser == null || string.IsNullOrWhiteSpace(txtUser.Text)) continue;

                Grid gridEdicion = FindChild<Grid>(gridFila, "PassEdicionGrid");
                string pDinamica = "";

                if (gridEdicion != null && gridEdicion.Visibility == Visibility.Visible)
                {
                    PasswordBox pbxNew = FindChild<PasswordBox>(gridFila, "PassNew");
                    PasswordBox pbxConfirm = FindChild<PasswordBox>(gridFila, "PassConfirm");

                    if (pbxNew.Password != pbxConfirm.Password)
                    {
                        MostrarToast($"Las contraseñas del usuario '{txtUser.Text}' no coinciden.");
                        return;
                    }
                    if (string.IsNullOrEmpty(pbxNew.Password))
                    {
                        MostrarToast($"Ingrese la contraseña para el usuario '{txtUser.Text}'.");
                        return;
                    }

                    pDinamica = pbxNew.Password;

                    string currentEnc = FindChild<TextBlock>(gridFila, "CurrentEncryptedPass")?.Text;
                    if (!esN && !string.IsNullOrEmpty(currentEnc) && currentEnc != "NEW")
                    {
                        RegistrarEventoHistorial(h, $"Cambio de Contraseña ({txtUser.Text})", uAct, fH, txtUser.Text, currentEnc);
                    }
                }
                else
                {
                    pDinamica = Desencriptar(FindChild<TextBlock>(gridFila, "CurrentEncryptedPass")?.Text ?? "");
                }

                _servidorEditando.Add(new XElement("Usuario",
                    new XAttribute("nombre", txtUser.Text),
                    new XElement("Password", Encriptar(pDinamica), new XAttribute("activa", "true"))));
            }

            // --- 5. FINALIZACIÓN ---
            GuardarXML();
            LiberarLock();
            _lockRenewTimer.Stop();

            CargarDatosXML();
            ScrollModoEdicion.Visibility = Visibility.Collapsed;
            TxtMensajeVacio.Visibility = Visibility.Visible;

            MostrarToast("Servidor guardado con éxito");
        }

        private void BtnCerrarForm_Click(object sender, RoutedEventArgs e)
        {
            LiberarLock();
            _lockRenewTimer.Stop();
            LimpiarFormularioUsuarios();
            ScrollModoEdicion.Visibility = Visibility.Collapsed;
            TxtMensajeVacio.Visibility = Visibility.Visible;
        }

        // ==========================================
        // RUTINAS DE UI (IN-PLACE EDIT Y DINÁMICOS)
        // ==========================================

        private void CargarForm(XElement s)
        {
            string t = s.Attribute("tipo")?.Value;
            foreach (ComboBoxItem i in ComboTipo.Items)
                if (i.Content.ToString() == t) ComboTipo.SelectedItem = i;

            TxtNombre.Text = s.Attribute("nombre")?.Value;
            TxtIP.Text = s.Attribute("ip")?.Value;
            TxtURL.Text = s.Attribute("url")?.Value;
            TxtComentario.Text = s.Element("Comentario")?.Value;
            TxtDominio.Text = s.Attribute("dominio")?.Value;
            TxtHardwareTipo.Text = s.Attribute("hw_tipo")?.Value;
            //TxtLlave.Text = s.Element("LlaveSSH")?.Value;

            var usuarios = s.Elements("Usuario").ToList();
            if (usuarios.Count > 0)
            {
                // Cargar Principal
                var u0 = usuarios[0];
                TxtUsuario.Text = u0.Attribute("nombre")?.Value;
                var p0 = u0.Elements("Password").FirstOrDefault(x => x.Attribute("activa")?.Value == "true");
                _passwordDesencriptadaActual = Desencriptar(p0?.Value ?? "");
                TxtEdicionPassOculta.Text = string.IsNullOrEmpty(_passwordDesencriptadaActual) ? "-" : "••••••••";

                PanelPassLecturaBase.Visibility = Visibility.Visible;
                PanelPassEdicionBase.Visibility = Visibility.Collapsed;
                PanelConfirmarBase.Visibility = Visibility.Collapsed;
                //BtnCancelarCambioBase.Visibility = Visibility.Visible;
            }

            // Cargar Dinámicos
            for (int i = 1; i < usuarios.Count; i++)
            {
                var ui = usuarios[i];
                string nom = ui.Attribute("nombre")?.Value;
                string passEnc = ui.Elements("Password").FirstOrDefault(x => x.Attribute("activa")?.Value == "true")?.Value;
                CrearFilaUsuarioDinamico(nom, passEnc);
            }
        }

        // Eventos UX Base
        private void VerPassLectura_Down(object sender, MouseButtonEventArgs e) { if (!string.IsNullOrEmpty(_passwordDesencriptadaActual)) { TxtVerPasswordOculta.Visibility = Visibility.Collapsed; TxtVerPasswordReal.Visibility = Visibility.Visible; } }
        private void VerPassLectura_Up(object sender, EventArgs e) { TxtVerPasswordOculta.Visibility = Visibility.Visible; TxtVerPasswordReal.Visibility = Visibility.Collapsed; }
        private void CopiarPassLectura_Click(object sender, RoutedEventArgs e) { if (!string.IsNullOrEmpty(_passwordDesencriptadaActual)) Clipboard.SetText(_passwordDesencriptadaActual); }

        private void EdicionOjoBase_Down(object sender, MouseButtonEventArgs e) { if (!string.IsNullOrEmpty(_passwordDesencriptadaActual)) { TxtEdicionPassOculta.Visibility = Visibility.Collapsed; TxtEdicionPassReal.Text = _passwordDesencriptadaActual; TxtEdicionPassReal.Visibility = Visibility.Visible; } }
        private void EdicionOjoBase_Up(object sender, EventArgs e) { TxtEdicionPassOculta.Visibility = Visibility.Visible; TxtEdicionPassReal.Visibility = Visibility.Collapsed; }

        private void NuevoOjo1Base_Down(object sender, MouseButtonEventArgs e) { TxtNuevoPassVisible.Text = TxtNuevoPass.Password; TxtNuevoPass.Visibility = Visibility.Collapsed; TxtNuevoPassVisible.Visibility = Visibility.Visible; }
        private void NuevoOjo1Base_Up(object sender, EventArgs e) { TxtNuevoPassVisible.Visibility = Visibility.Collapsed; TxtNuevoPass.Visibility = Visibility.Visible; }
        private void NuevoOjo2Base_Down(object sender, MouseButtonEventArgs e) { TxtNuevoConfirmVisible.Text = TxtNuevoConfirm.Password; TxtNuevoConfirm.Visibility = Visibility.Collapsed; TxtNuevoConfirmVisible.Visibility = Visibility.Visible; }
        private void NuevoOjo2Base_Up(object sender, EventArgs e) { TxtNuevoConfirmVisible.Visibility = Visibility.Collapsed; TxtNuevoConfirm.Visibility = Visibility.Visible; }

        private void BtnIniciarCambioPassBase_Click(object sender, RoutedEventArgs e)
        {
            // Ocultamos el modo lectura
            PanelPassLecturaBase.Visibility = Visibility.Collapsed;

            // Mostramos el modo edición (campos de password)
            PanelPassEdicionBase.Visibility = Visibility.Visible;
            PanelConfirmarBase.Visibility = Visibility.Visible;

            // MOSTRAMOS el botón cancelar (X)
            BtnCancelarCambioBase.Visibility = Visibility.Visible;

            // Limpiamos los campos para la nueva contraseña
            TxtNuevoPass.Password = "";
            TxtNuevoConfirm.Password = "";
        }

        private void BtnCancelarCambioPassBase_Click(object sender, RoutedEventArgs e)
        {
            // Volvemos al modo lectura
            PanelPassLecturaBase.Visibility = Visibility.Visible;

            // Ocultamos los campos de edición
            PanelPassEdicionBase.Visibility = Visibility.Collapsed;
            PanelConfirmarBase.Visibility = Visibility.Collapsed;

            // OCULTAMOS el botón cancelar (X)
            BtnCancelarCambioBase.Visibility = Visibility.Collapsed;
        }

        // --- LOGICA DE VALIDACIÓN VISUAL DE CONTRASEÑAS ---
        private void ValidarPasswordBase_Changed(object sender, RoutedEventArgs e)
        {
            // Verificamos que los objetos existan antes de actuar
            if (TxtNuevoPass == null || TxtNuevoConfirm == null || BordeConfirmacionBase == null) return;

            // LLAMADA CORRECTA: Solo 3 argumentos (string, string, Border)
            AplicarColorValidacion(TxtNuevoPass.Password, TxtNuevoConfirm.Password, BordeConfirmacionBase);
        }

        private void AplicarColorValidacion(string pass1, string pass2, Border bordeContenedor)
        {
            if (string.IsNullOrEmpty(pass2))
            {
                bordeContenedor.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ABADB3"));
                bordeContenedor.BorderThickness = new Thickness(1);
                return;
            }

            var colorExito = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981")); // Verde
            var colorError = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444")); // Rojo

            if (pass1 == pass2)
            {
                bordeContenedor.BorderBrush = colorExito;
                bordeContenedor.BorderThickness = new Thickness(2);
            }
            else
            {
                bordeContenedor.BorderBrush = colorError;
                bordeContenedor.BorderThickness = new Thickness(2);
            }
        }

        // Overload to support dynamic rows: adjusts the BorderBrush/BorderThickness of a PasswordBox and its visible TextBox mirror
        private void AplicarColorValidacion(string pass1, string pass2, PasswordBox pbConfirm, TextBox tbConfirmVisible)
        {
            if (pbConfirm == null) return;

            // If confirm is empty, set neutral style
            if (string.IsNullOrEmpty(pass2))
            {
                var neutral = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ABADB3"));
                pbConfirm.BorderBrush = neutral;
                pbConfirm.BorderThickness = new Thickness(1);
                if (tbConfirmVisible != null)
                {
                    tbConfirmVisible.BorderBrush = neutral;
                    tbConfirmVisible.BorderThickness = new Thickness(1);
                }
                return;
            }

            var colorExito = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981")); // Verde
            var colorError = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444")); // Rojo

            if (pass1 == pass2)
            {
                pbConfirm.BorderBrush = colorExito;
                pbConfirm.BorderThickness = new Thickness(2);
                if (tbConfirmVisible != null)
                {
                    tbConfirmVisible.BorderBrush = colorExito;
                    tbConfirmVisible.BorderThickness = new Thickness(2);
                }
            }
            else
            {
                pbConfirm.BorderBrush = colorError;
                pbConfirm.BorderThickness = new Thickness(2);
                if (tbConfirmVisible != null)
                {
                    tbConfirmVisible.BorderBrush = colorError;
                    tbConfirmVisible.BorderThickness = new Thickness(2);
                }
            }
        }

        private void BtnAddUsuario_Click(object sender, RoutedEventArgs e)
        {
            CrearFilaUsuarioDinamico("", ""); // Crea fila vacía en modo Edición
        }

        // Helper para buscar elementos Uid en el árbol visual dinámico
        private static T FindChild<T>(DependencyObject parent, string uid) where T : UIElement
        {
            if (parent == null) return null;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild && typedChild.Uid == uid) return typedChild;
                var result = FindChild<T>(child, uid);
                if (result != null) return result;
            }
            return null;
        }

        // --- RUTINA 3: CLONACIÓN DE UX PARA USUARIOS DINÁMICOS ---
        // [Nota: El resto del archivo se mantiene igual, solo sustituye el método CrearFilaUsuarioDinamico]

        private void CrearFilaUsuarioDinamico(string nombre, string passwordEncriptada)
        {
            bool esNuevo = string.IsNullOrEmpty(passwordEncriptada);
            string passDec = esNuevo ? "" : Desencriptar(passwordEncriptada);

            Grid filaGrid = new Grid { Margin = new Thickness(0, 0, 0, 10), Tag = "DINAMICO" };
            filaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.1, GridUnitType.Star) });
            filaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            filaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            TextBlock hiddenEncPass = new TextBlock { Uid = "CurrentEncryptedPass", Text = esNuevo ? "NEW" : passwordEncriptada, Visibility = Visibility.Collapsed };
            filaGrid.Children.Add(hiddenEncPass);

            // --- COL 0: USUARIO ---
            StackPanel spUsr = new StackPanel { Margin = new Thickness(0, 0, 5, 0) };
            Grid.SetColumn(spUsr, 0);
            StackPanel spHdr = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 5), Height = 22 };
            spHdr.Children.Add(new TextBlock { Text = "Usuario", Foreground = Brushes.Gray, VerticalAlignment = VerticalAlignment.Center });
            //Button btnDel = new Button { Content = "❌", Width = 22, Height = 22, Margin = new Thickness(8, 0, 0, 0), Cursor = Cursors.Hand, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Brushes.DarkRed, ToolTip = "Eliminar Fila" };
            //btnDel.Click += (s, e) => ContenedorListaUsuarios.Children.Remove(filaGrid);
            //spHdr.Children.Add(btnDel);
            TextBox txtU = new TextBox { Uid = "User", Height = 32, Text = nombre };
            spUsr.Children.Add(spHdr); spUsr.Children.Add(txtU);

            // --- COL 1: CONTRASEÑA ---
            StackPanel spPass = new StackPanel { Margin = new Thickness(5, 0, 5, 0), VerticalAlignment = VerticalAlignment.Top };
            Grid.SetColumn(spPass, 1);
            spPass.Children.Add(new TextBlock { Text = "Contraseña", Foreground = Brushes.Gray, Margin = new Thickness(0, 0, 0, 5), Height = 22 });

            Grid gridLectura = new Grid { Uid = "PassLecturaGrid", Visibility = esNuevo ? Visibility.Collapsed : Visibility.Visible };
            gridLectura.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            gridLectura.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            gridLectura.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Border borderL = new Border { BorderThickness = new Thickness(1), BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D1D5DB")), Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F3F4F6")), CornerRadius = new CornerRadius(3), Height = 32 };
            Grid inBorder = new Grid();
            TextBlock txtOcultaL = new TextBlock { Text = "••••••••", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0), Foreground = Brushes.Gray };
            TextBlock txtRealL = new TextBlock { Text = passDec, Visibility = Visibility.Collapsed, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0), Foreground = Brushes.DarkSlateGray, FontWeight = FontWeights.Bold };
            inBorder.Children.Add(txtOcultaL); inBorder.Children.Add(txtRealL); borderL.Child = inBorder;
            Grid.SetColumn(borderL, 0);

            Button btnOjoL = new Button { Content = "👁", Width = 35, Height = 32, Margin = new Thickness(5, 0, 0, 0), Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E7EB")), BorderBrush = borderL.BorderBrush, Cursor = Cursors.Hand };
            Grid.SetColumn(btnOjoL, 1);
            btnOjoL.PreviewMouseDown += (s, e) => { txtOcultaL.Visibility = Visibility.Collapsed; txtRealL.Visibility = Visibility.Visible; };
            btnOjoL.PreviewMouseUp += (s, e) => { txtOcultaL.Visibility = Visibility.Visible; txtRealL.Visibility = Visibility.Collapsed; };
            btnOjoL.MouseLeave += (s, e) => { txtOcultaL.Visibility = Visibility.Visible; txtRealL.Visibility = Visibility.Collapsed; };

            Button btnCambiar = new Button { Content = "Cambiar", Width = 65, Height = 32, Margin = new Thickness(5, 0, 0, 0), Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#374151")), Foreground = Brushes.White, BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
            Grid.SetColumn(btnCambiar, 2);
            gridLectura.Children.Add(borderL); gridLectura.Children.Add(btnOjoL); gridLectura.Children.Add(btnCambiar);

            Grid gridEdicion = new Grid { Uid = "PassEdicionGrid", Visibility = esNuevo ? Visibility.Visible : Visibility.Collapsed };
            PasswordBox pbxN = new PasswordBox { Uid = "PassNew", Height = 32, Padding = new Thickness(5, 0, 35, 0) };
            TextBox tbxNV = new TextBox { Height = 32, Padding = new Thickness(5, 0, 35, 0), Visibility = Visibility.Collapsed };
            Button btnOjoN = new Button { Content = "👁", Width = 30, HorizontalAlignment = HorizontalAlignment.Right, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
            btnOjoN.PreviewMouseDown += (s, e) => { tbxNV.Text = pbxN.Password; pbxN.Visibility = Visibility.Collapsed; tbxNV.Visibility = Visibility.Visible; };
            btnOjoN.PreviewMouseUp += (s, e) => { tbxNV.Visibility = Visibility.Collapsed; pbxN.Visibility = Visibility.Visible; };
            btnOjoN.MouseLeave += (s, e) => { tbxNV.Visibility = Visibility.Collapsed; pbxN.Visibility = Visibility.Visible; };
            gridEdicion.Children.Add(pbxN); gridEdicion.Children.Add(tbxNV); gridEdicion.Children.Add(btnOjoN);
            spPass.Children.Add(gridLectura); spPass.Children.Add(gridEdicion);

            // --- COL 2: CONFIRMAR (CON BORDE PARA VALIDACIÓN) ---
            StackPanel spConf = new StackPanel { Margin = new Thickness(5, 0, 0, 0), VerticalAlignment = VerticalAlignment.Top, Uid = "PassConfirmPanel", Visibility = esNuevo ? Visibility.Visible : Visibility.Collapsed };
            Grid.SetColumn(spConf, 2);
            spConf.Children.Add(new TextBlock { Text = "Confirmar Contraseña", Foreground = Brushes.Gray, Margin = new Thickness(0, 0, 0, 5), Height = 22 });

            Grid gridConfirmFinal = new Grid();
            gridConfirmFinal.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            gridConfirmFinal.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // AQUÍ EL CAMBIO CLAVE: El Border que cambiará de color
            Border borderConfirmDinamico = new Border { BorderThickness = new Thickness(1), BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ABADB3")), CornerRadius = new CornerRadius(2) };
            Grid.SetColumn(borderConfirmDinamico, 0);

            Grid inConf = new Grid();
            PasswordBox pbxC = new PasswordBox { Uid = "PassConfirm", Height = 32, Padding = new Thickness(5, 0, 35, 0), BorderThickness = new Thickness(0), Background = Brushes.Transparent };
            TextBox tbxCV = new TextBox { Height = 32, Padding = new Thickness(5, 0, 35, 0), Visibility = Visibility.Collapsed, BorderThickness = new Thickness(0), Background = Brushes.Transparent };
            Button btnOjoC = new Button { Content = "👁", Width = 30, HorizontalAlignment = HorizontalAlignment.Right, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
            btnOjoC.PreviewMouseDown += (s, e) => { tbxCV.Text = pbxC.Password; pbxC.Visibility = Visibility.Collapsed; tbxCV.Visibility = Visibility.Visible; };
            btnOjoC.PreviewMouseUp += (s, e) => { tbxCV.Visibility = Visibility.Collapsed; pbxC.Visibility = Visibility.Visible; };
            btnOjoC.MouseLeave += (s, e) => { tbxCV.Visibility = Visibility.Collapsed; pbxC.Visibility = Visibility.Visible; };
            inConf.Children.Add(pbxC); inConf.Children.Add(tbxCV); inConf.Children.Add(btnOjoC);
            borderConfirmDinamico.Child = inConf;

            Button btnCancelE = new Button { Content = "❌", Width = 32, Height = 32, Margin = new Thickness(5, 0, 0, 0), Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEE2E2")), Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#991B1B")), BorderThickness = new Thickness(0), Cursor = Cursors.Hand, Visibility = esNuevo ? Visibility.Collapsed : Visibility.Visible };
            Grid.SetColumn(btnCancelE, 1);

            gridConfirmFinal.Children.Add(borderConfirmDinamico); gridConfirmFinal.Children.Add(btnCancelE);
            spConf.Children.Add(gridConfirmFinal);

            // --- EVENTOS DE VALIDACIÓN (AQUÍ ESTÁ LA MAGIA) ---
            // Usamos expresiones lambda para pasar el Border correcto de esta fila específica
            pbxN.PasswordChanged += (s, e) => AplicarColorValidacion(pbxN.Password, pbxC.Password, borderConfirmDinamico);
            pbxC.PasswordChanged += (s, e) => AplicarColorValidacion(pbxN.Password, pbxC.Password, borderConfirmDinamico);

            // --- LÓGICA DE BOTONES ---
            btnCambiar.Click += (s, e) => {
                gridLectura.Visibility = Visibility.Collapsed;
                gridEdicion.Visibility = Visibility.Visible;
                spConf.Visibility = Visibility.Visible;
                pbxN.Password = ""; pbxC.Password = "";
                // Reiniciamos color del borde al abrir edición
                borderConfirmDinamico.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ABADB3"));
                borderConfirmDinamico.BorderThickness = new Thickness(1);
            };

            btnCancelE.Click += (s, e) => {
                gridLectura.Visibility = Visibility.Visible;
                gridEdicion.Visibility = Visibility.Collapsed;
                spConf.Visibility = Visibility.Collapsed;
            };

            filaGrid.Children.Add(spUsr); filaGrid.Children.Add(spPass); filaGrid.Children.Add(spConf);
            ContenedorListaUsuarios.Children.Add(filaGrid);
        }

        // ==========================================
        // UTILERÍAS GLOBALES
        // ==========================================

        private void GuardarXML()
        {
            try
            {
                string ruta = GetXmlPath();
                if (!string.IsNullOrEmpty(ruta) && _xmlData != null)
                {
                    string tempFile = ruta + ".tmp";
                    using (FileStream fs = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        _xmlData.Save(fs);
                    }
                    if (File.Exists(ruta)) File.Delete(ruta);
                    File.Move(tempFile, ruta);
                }
            }
            catch (Exception ex) { MostrarToast("Error al guardar el XML: " + ex.Message); }
        }

        private void BtnAddGrupo_Click(object sender, RoutedEventArgs e)
        {
            string n = Microsoft.VisualBasic.Interaction.InputBox("Nombre Grupo:", "Nuevo Grupo");
            if (!string.IsNullOrWhiteSpace(n))
            {
                var g = new XElement("Grupo", new XAttribute("nombre", n));
                var h = new XElement("Historial");
                RegistrarEventoHistorial(h, "Creación de Grupo", Environment.UserName, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                g.Add(h); _xmlData?.Root?.Add(g);
                GuardarXML(); CargarDatosXML();
            }
        }

        private void RegistrarEventoHistorial(XElement h, string ac, string au, string f, string uAnt = null, string pAnt = null)
        {
            var ev = new XElement("Evento", new XAttribute("fecha", f), new XAttribute("accion", ac), new XAttribute("autor", au));
            if (uAnt != null) { ev.SetAttributeValue("usuarioAnterior", uAnt); ev.SetAttributeValue("passwordAnterior", pAnt); }
            h.AddFirst(ev);
        }

        private string Encriptar(string t)
        {
            using Aes aes = Aes.Create();
            byte[] k = new byte[32];
            Array.Copy(Encoding.UTF8.GetBytes(MasterKey), k, Math.Min(MasterKey.Length, 32));
            aes.Key = k; aes.IV = new byte[16];
            using var enc = aes.CreateEncryptor();
            byte[] b = Encoding.UTF8.GetBytes(t);
            return Convert.ToBase64String(enc.TransformFinalBlock(b, 0, b.Length));
        }

        private string Desencriptar(string b64)
        {
            try
            {
                using Aes aes = Aes.Create();
                byte[] k = new byte[32];
                Array.Copy(Encoding.UTF8.GetBytes(MasterKey), k, Math.Min(MasterKey.Length, 32));
                aes.Key = k; aes.IV = new byte[16];
                using var dec = aes.CreateDecryptor();
                byte[] b = Convert.FromBase64String(b64);
                return Encoding.UTF8.GetString(dec.TransformFinalBlock(b, 0, b.Length));
            }
            catch { return ""; }
        }

        private void AgregarFilaUsuarioAdicional(string nombre, string password)
        {
            Grid gridFila = new Grid { Margin = new Thickness(0, 0, 0, 5) };
            gridFila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            gridFila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Columna Usuario
            StackPanel spUsr = new StackPanel { Margin = new Thickness(0, 0, 5, 15) };
            spUsr.Children.Add(new TextBlock { Text = "Usuario Adicional", Foreground = Brushes.Gray, FontSize = 11 });
            spUsr.Children.Add(new TextBlock { Text = nombre, FontSize = 15, FontWeight = FontWeights.Medium });
            Grid.SetColumn(spUsr, 0);

            // Columna Password
            StackPanel spPass = new StackPanel { Margin = new Thickness(5, 0, 0, 15) };
            spPass.Children.Add(new TextBlock { Text = "Contraseña", Foreground = Brushes.Gray, FontSize = 11, Margin = new Thickness(0, 0, 0, 2) });

            Border borderPass = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F9FAFB")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E7EB")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Height = 35
            };

            Grid innerGrid = new Grid();
            TextBlock txtOculta = new TextBlock { Text = "••••••••••••", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 80, 0), FontFamily = new FontFamily("Consolas"), FontSize = 14 };
            TextBlock txtReal = new TextBlock { Text = password, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 80, 0), FontFamily = new FontFamily("Consolas"), FontSize = 14, Visibility = Visibility.Collapsed };

            StackPanel spBotones = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            Button btnOjo = new Button { Content = "👁", Width = 35, BorderThickness = new Thickness(0), Background = Brushes.Transparent, Cursor = Cursors.Hand };
            Button btnCopiar = new Button { Content = "📋", Width = 35, BorderThickness = new Thickness(0), Background = Brushes.Transparent, Cursor = Cursors.Hand };

            // Funcionalidad local
            btnOjo.PreviewMouseDown += (s, e) => { txtOculta.Visibility = Visibility.Collapsed; txtReal.Visibility = Visibility.Visible; };
            btnOjo.PreviewMouseUp += (s, e) => { txtOculta.Visibility = Visibility.Visible; txtReal.Visibility = Visibility.Collapsed; };
            btnOjo.MouseLeave += (s, e) => { txtOculta.Visibility = Visibility.Visible; txtReal.Visibility = Visibility.Collapsed; };
            btnCopiar.Click += (s, e) => { Clipboard.SetText(password); };

            spBotones.Children.Add(btnOjo);
            spBotones.Children.Add(btnCopiar);
            innerGrid.Children.Add(txtOculta);
            innerGrid.Children.Add(txtReal);
            innerGrid.Children.Add(spBotones);
            borderPass.Child = innerGrid;
            spPass.Children.Add(borderPass);
            Grid.SetColumn(spPass, 1);

            gridFila.Children.Add(spUsr);
            gridFila.Children.Add(spPass);
            ContenedorUsuariosAdicionalesLectura.Children.Add(gridFila);
        }

        private void BtnAgregarLlave_Click(object sender, RoutedEventArgs e)
        {
            var nuevaLlave = MostrarPopupNuevaLlave();

            if (nuevaLlave != null)
            {
                _llavesTemporales.Insert(0, nuevaLlave); // Agrega al inicio de la lista
                ActualizarListaLlavesUI();
            }
        }

        private string PedirComentarioSimple()
        {
            // Creamos la ventana con un estilo más de "Popup"
            Window win = new Window
            {
                Title = "Comentario Obligatorio",
                Width = 350,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.ToolWindow, // Quita botones de min/max
                Background = Brushes.White
            };

            StackPanel sp = new StackPanel { Margin = new Thickness(20) };

            sp.Children.Add(new TextBlock
            {
                Text = "Identifique esta llave con un comentario:",
                Margin = new Thickness(0, 0, 0, 10),
                FontWeight = FontWeights.Bold,
                Foreground = (Brush)new BrushConverter().ConvertFrom("#374151")
            });

            TextBox txt = new TextBox
            {
                Height = 35,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(5, 0, 5, 0)
            };
            sp.Children.Add(txt);

            // BOTÓN DE GUARDAR
            Button btn = new Button
            {
                Content = "Guardar y Agregar Llave",
                Margin = new Thickness(0, 20, 0, 0),
                Height = 40,
                Background = (Brush)new BrushConverter().ConvertFrom("#4F46E5"),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                Cursor = Cursors.Hand,
                IsDefault = true // Esto hace que funcione al presionar ENTER
            };

            // Estilo para el botón (bordes redondeados)
            Style style = new Style(typeof(Border));
            style.Setters.Add(new Setter(Border.CornerRadiusProperty, new CornerRadius(5)));
            btn.Resources.Add(typeof(Border), style);

            btn.Click += (s, ev) =>
            {
                if (string.IsNullOrWhiteSpace(txt.Text))
                {
                    MessageBox.Show("Debe ingresar un comentario para continuar.", "Aviso");
                    return;
                }
                win.DialogResult = true; // Esto cierra la ventana y devuelve 'true' al ShowDialog
            };

            sp.Children.Add(btn);
            win.Content = sp;

            // Forzamos el foco en el texto para escribir de inmediato
            txt.Focus();

            // Si el usuario cierra la ventana o da Guardar
            if (win.ShowDialog() == true)
            {
                return txt.Text;
            }

            return null; // Si cancela o cierra la X
        }

        private void ActualizarListaLlavesUI()
        {
            ContenedorLlaves.Children.Clear();
            foreach (var ll in _llavesTemporales)
            {
                var border = new Border { Background = Brushes.White, BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(5) };
                var txt = new TextBlock
                {
                    Text = $"{ll.Fecha} | {ll.Usuario} | {ll.NombreOriginal}\n💬 {ll.Comentario}",
                    FontSize = 10,
                    Foreground = Brushes.DarkSlateGray
                };
                border.Child = txt;
                ContenedorLlaves.Children.Add(border);
            }
        }

        private void CargarLlavesEnModoVer(XElement servidor)
        {
            // 1. Limpiamos visualmente la lista anterior
            ContenedorLlavesModoVer.Children.Clear();

            // 2. Buscamos si este servidor tiene llaves guardadas en el XML
            var nodosLlaves = servidor.Elements("LlaveSSH_File").ToList();

            // Si no hay llaves, ocultamos la sección y terminamos
            if (nodosLlaves.Count == 0)
            {
                PanelVerLlave.Visibility = Visibility.Collapsed;
                return;
            }

            // Si hay llaves, mostramos la sección
            PanelVerLlave.Visibility = Visibility.Visible;

            // 3. Convertimos los datos del XML a nuestra lista y los ordenamos por fecha (más nueva arriba)
            var listaLlaves = nodosLlaves.Select(x => new LlaveTemporal
            {
                NombreOriginal = x.Attribute("nombre_original")?.Value,
                NombreFisico = x.Attribute("nombre_fisico")?.Value,
                Comentario = x.Element("Comentario")?.Value,
                Fecha = x.Attribute("fecha")?.Value,
                Usuario = x.Attribute("usuario")?.Value
            }).OrderByDescending(x => x.Fecha).ToList();

            // 4. Dibujamos cada llave en pantalla
            foreach (var ll in listaLlaves)
            {
                // Contenedor principal de la fila
                var border = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F9FAFB")),
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E7EB")),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(5),
                    Margin = new Thickness(0, 0, 0, 5),
                    Padding = new Thickness(10, 5, 10, 5)
                };

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Texto
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Botones

                // Información de la llave (Nombre y Comentario)
                var txtInfo = new TextBlock
                {
                    Text = $"📄 {ll.NombreOriginal}\n💬 {ll.Comentario}\n📅 {ll.Fecha} | 👤 {ll.Usuario}",
                    FontSize = 11,
                    Foreground = Brushes.DarkSlateGray
                };
                Grid.SetColumn(txtInfo, 0);
                grid.Children.Add(txtInfo);

                // Panel para los botones
                var spBotones = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

                // BOTÓN COPIAR AL PORTAPAPELES
                Button btnCopy = new Button { Content = "📋", ToolTip = "Copiar contenido al portapapeles", Width = 30, Height = 25, Cursor = Cursors.Hand, Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
                btnCopy.Click += (s, e) => {
                    try
                    {
                        string rutaFisica = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(GetXmlPath()), "keys", ll.NombreFisico);
                        if (System.IO.File.Exists(rutaFisica))
                        {
                            Clipboard.SetText(System.IO.File.ReadAllText(rutaFisica));
                            //MessageBox.Show("Contenido de la llave copiado al portapapeles.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            MessageBox.Show("El archivo físico de la llave no se encuentra en la carpeta '/keys/'.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                    catch (Exception ex) { MostrarToast("Error al leer el archivo: " + ex.Message); }
                };

                // BOTÓN DESCARGAR ARCHIVO
                Button btnDown = new Button { Content = "💾", ToolTip = "Descargar archivo", Width = 30, Height = 25, Margin = new Thickness(5, 0, 0, 0), Cursor = Cursors.Hand, Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
                btnDown.Click += (s, e) => {
                    try
                    {
                        string rutaOrigen = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(GetXmlPath()), "keys", ll.NombreFisico);
                        if (!System.IO.File.Exists(rutaOrigen))
                        {
                            MostrarToast("El archivo físico de la llave no se encuentra en el servidor.");
                            return;
                        }

                        var saveDialog = new Microsoft.Win32.SaveFileDialog
                        {
                            FileName = ll.NombreOriginal, // Sugiere el nombre real del archivo
                            Title = "Guardar llave SSH"
                        };

                        if (saveDialog.ShowDialog() == true)
                        {
                            System.IO.File.Copy(rutaOrigen, saveDialog.FileName, true);
                            //MessageBox.Show("Archivo descargado correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                    catch (Exception ex) { MostrarToast("Error al descargar: " + ex.Message); }
                };

                spBotones.Children.Add(btnCopy);
                spBotones.Children.Add(btnDown);

                Grid.SetColumn(spBotones, 1);
                grid.Children.Add(spBotones);

                border.Child = grid;
                ContenedorLlavesModoVer.Children.Add(border);
            }

        }

        private void MostrarToast(string mensaje)
        {
            // Creamos el diseño del Toast por código
            var border = new Border
            {
                Background = (Brush)new BrushConverter().ConvertFrom("#374151"),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(15, 10, 15, 10),
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 10, Opacity = 0.3 }
            };

            var textBlock = new TextBlock
            {
                Text = mensaje,
                Foreground = Brushes.White,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center
            };
            border.Child = textBlock;

            // Usamos un Popup de WPF (no necesita archivo XAML extra)
            System.Windows.Controls.Primitives.Popup toast = new System.Windows.Controls.Primitives.Popup
            {
                Child = border,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Center,
                PlacementTarget = this, // Se centra respecto a la ventana principal
                AllowsTransparency = true,
                PopupAnimation = System.Windows.Controls.Primitives.PopupAnimation.Fade,
                IsOpen = true
            };

            // Temporizador para cerrarlo
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            timer.Tick += (s, e) => {
                toast.IsOpen = false;
                timer.Stop();
            };
            timer.Start();
        }

    }
}