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

        private string _nuevaPasswordPendiente = "";
        private string _passwordDesencriptadaActual = "";

        // Timer para renovar lock automáticamente
        private System.Windows.Threading.DispatcherTimer _lockRenewTimer;

        // --- RUTAS DE ARCHIVOS ---
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

            // Inicializar timer para renovar lock
            _lockRenewTimer = new System.Windows.Threading.DispatcherTimer();
            _lockRenewTimer.Interval = TimeSpan.FromMinutes(4); // Renovar cada 4 minutos
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

                // Leer con FileShare.None evita chocar con OneDrive si está sincronizando en ese instante
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
                        var nS = new TreeViewItem { Header = $"{sXml.Attribute("nombre")?.Value} ({sXml.Attribute("ip")?.Value})" };
                        _nodosServidores.Add(nS, sXml);
                        nodoG.Items.Add(nS);
                    }
                    nodoG.IsExpanded = true;
                    ArbolServidores.Items.Add(nodoG);
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
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
            var u = s.Element("Usuario");
            LblVerUsuario.Text = u?.Attribute("nombre")?.Value ?? "-";
            var p = u?.Elements("Password").FirstOrDefault(x => x.Attribute("activa")?.Value == "true");
            _passwordDesencriptadaActual = Desencriptar(p?.Value ?? "");
            TxtVerPasswordReal.Text = _passwordDesencriptadaActual;
            TxtVerPasswordOculta.Text = string.IsNullOrEmpty(_passwordDesencriptadaActual) ? "-" : "••••••••";
            TxtVerLlave.Text = s.Element("LlaveSSH")?.Value ?? "";
            PanelVerLlave.Visibility = string.IsNullOrEmpty(TxtVerLlave.Text) ? Visibility.Collapsed : Visibility.Visible;
            LblVerComentarios.Text = s.Element("Comentario")?.Value ?? "-";
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
        // SISTEMA DE BLOQUEO (LOCK) CORREGIDO
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

                        // Si el lock es del mismo usuario, permitir acceso y renovar lock
                        if (usuarioLock == Environment.UserName)
                        {
                            // Mismo usuario: renovamos el lock (actualizamos timestamp)
                            File.WriteAllLines(lockPath, new string[] {
                                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                                Environment.UserName
                            });
                            return true; // Permitir edición
                        }

                        // Diferente usuario: verificar si el lock expiró
                        if (diff.TotalMinutes < 5)
                        {
                            MessageBox.Show($"Registro ocupado.\n\nEste registro está siendo editado actualmente por {usuarioLock}.\nEl bloqueo fue iniciado a las {timestamp:HH:mm:ss}.\n\nPor favor, inténtalo de nuevo en unos minutos.",
                                "Bloqueo Activo", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return false; // Bloqueo vigente de otro usuario
                        }
                    }
                }

                // Si no existe, expiró, o es de otro usuario pero expiró, tomamos control
                File.WriteAllLines(lockPath, new string[] {
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    Environment.UserName
                });
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("No se pudo verificar el acceso concurrente: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private void LiberarLock()
        {
            string lockPath = GetLockPath();
            try
            {
                if (File.Exists(lockPath))
                {
                    // Solo liberar el lock si es del usuario actual
                    string[] lines = File.ReadAllLines(lockPath);
                    if (lines.Length >= 2 && lines[1] == Environment.UserName)
                    {
                        File.Delete(lockPath);
                    }
                }
            }
            catch { /* Ignorar si no se puede borrar */ }
        }

        private bool ValidarLockParaGuardar()
        {
            string lockPath = GetLockPath();
            if (!File.Exists(lockPath))
            {
                MessageBox.Show("El archivo de bloqueo ha desaparecido. No se puede guardar por seguridad.",
                    "Error de Seguridad", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            try
            {
                string[] lines = File.ReadAllLines(lockPath);
                if (lines.Length >= 2 && DateTime.TryParse(lines[0], out DateTime timestamp))
                {
                    string usuarioLock = lines[1];
                    TimeSpan diff = DateTime.Now - timestamp;

                    // Si es el mismo usuario, permitir guardar (aunque haya pasado tiempo)
                    if (usuarioLock == Environment.UserName)
                    {
                        // Renovamos el lock al guardar
                        File.WriteAllLines(lockPath, new string[] {
                            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                            Environment.UserName
                        });
                        return true;
                    }

                    // Diferente usuario: verificar expiración
                    if (diff.TotalMinutes > 5)
                    {
                        MessageBox.Show("Lock Timeout: El tiempo de edición ha expirado (5 minutos).\n\nTus cambios no se guardaron por seguridad para evitar conflictos con otros usuarios.",
                            "Tiempo Excedido", MessageBoxButton.OK, MessageBoxImage.Error);
                        return false;
                    }

                    MessageBox.Show($"El archivo de bloqueo pertenece a {usuarioLock}. No se pueden guardar los cambios.",
                        "Error de Seguridad", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private void LockRenewTimer_Tick(object sender, EventArgs e)
        {
            string lockPath = GetLockPath();
            if (File.Exists(lockPath))
            {
                try
                {
                    // Verificar que el lock sigue siendo nuestro antes de renovar
                    string[] lines = File.ReadAllLines(lockPath);
                    if (lines.Length >= 2 && lines[1] == Environment.UserName)
                    {
                        // Renovar el lock
                        File.WriteAllLines(lockPath, new string[] {
                            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                            Environment.UserName
                        });
                    }
                }
                catch { }
            }
        }

        // ==========================================

        private void BtnAddServidorDesdeGrupo_Click(object sender, RoutedEventArgs e)
        {
            // Verificamos el Lock antes de dejarlo entrar a editar
            if (!VerificarYTomarLock()) return;

            _grupoSeleccionadoParaNuevo = (XElement)((Button)sender).Tag;
            _servidorEditando = null;
            _nuevaPasswordPendiente = "";
            LimpiarForm();
            TxtTituloDerecho.Text = "Nuevo Servidor";
            PanelPassEdicion.Visibility = Visibility.Collapsed;
            PanelPassNuevo.Visibility = Visibility.Visible;
            PanelConfirmarNuevo.Visibility = Visibility.Visible;
            ScrollModoVer.Visibility = Visibility.Collapsed;
            ScrollModoEdicion.Visibility = Visibility.Visible;
            TxtMensajeVacio.Visibility = Visibility.Collapsed;

            // Iniciar timer para renovar lock automáticamente
            _lockRenewTimer.Start();
        }

        private void BtnActivarEdicion_Click(object sender, RoutedEventArgs e)
        {
            if (_servidorEditando == null) return;

            // Verificamos el Lock antes de dejarlo entrar a editar
            if (!VerificarYTomarLock()) return;

            TxtTituloDerecho.Text = "Editar Registro";
            _nuevaPasswordPendiente = "";
            CargarForm(_servidorEditando);
            PanelPassEdicion.Visibility = Visibility.Visible;
            PanelPassNuevo.Visibility = Visibility.Collapsed;
            PanelConfirmarNuevo.Visibility = Visibility.Collapsed;
            ScrollModoVer.Visibility = Visibility.Collapsed;
            ScrollModoEdicion.Visibility = Visibility.Visible;

            // Iniciar timer para renovar lock automáticamente
            _lockRenewTimer.Start();
        }

        private void BtnGuardarServidor_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtNombre.Text) || _xmlData == null) return;

            if (!ValidarLockParaGuardar())
            {
                LiberarLock();
                _lockRenewTimer.Stop();
                ScrollModoEdicion.Visibility = Visibility.Collapsed;
                TxtMensajeVacio.Visibility = Visibility.Visible;
                return;
            }

            bool esN = _servidorEditando == null;
            string uAct = Environment.UserName;
            string fH = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            if (esN)
            {
                // Validación básica (solo para el primer usuario)
                if (TxtNuevoPass.Password != TxtNuevoConfirm.Password) { MessageBox.Show("Las contraseñas del primer usuario no coinciden."); return; }
                if (string.IsNullOrEmpty(TxtNuevoPass.Password)) { MessageBox.Show("Ingrese la contraseña del primer usuario."); return; }

                _nuevaPasswordPendiente = TxtNuevoPass.Password;
                _servidorEditando = new XElement("Servidor");
                var h = new XElement("Historial");
                _servidorEditando.Add(h);
                RegistrarEventoHistorial(h, "Creación de Servidor", uAct, fH);
                _grupoSeleccionadoParaNuevo?.Add(_servidorEditando);
            }
            else
            {
                var h = _servidorEditando.Element("Historial");
                if (h == null) { h = new XElement("Historial"); _servidorEditando.AddFirst(h); }
                RegistrarEventoHistorial(h, "Modificación de Servidor", uAct, fH);

                // Eliminamos los nodos antiguos de usuarios para reemplazarlos por los nuevos (menos el historial)
                _servidorEditando.Elements().Where(x => x.Name != "Historial" && x.Name != "Usuario").Remove();
                // Nota: Si tus usuarios dinámicos antes no existían, esto limpia la estructura vieja
            }

            // Atributos básicos
            _servidorEditando.SetAttributeValue("tipo", (ComboTipo.SelectedItem as ComboBoxItem)?.Content.ToString());
            _servidorEditando.SetAttributeValue("nombre", TxtNombre.Text);
            _servidorEditando.SetAttributeValue("ip", TxtIP.Text);
            _servidorEditando.SetAttributeValue("url", TxtURL.Text);
            _servidorEditando.SetAttributeValue("dominio", TxtDominio.Text);
            _servidorEditando.SetAttributeValue("hw_tipo", TxtHardwareTipo.Text);

            // Comentarios y llaves
            _servidorEditando.Add(new XElement("Comentario", TxtComentario.Text), new XElement("LlaveSSH", TxtLlave.Text));

            // --- AQUÍ CONSTRUIMOS TODOS LOS USUARIOS ---

            // 1. Primero, el usuario principal (fijo)
            string pPrincipal = !string.IsNullOrEmpty(_nuevaPasswordPendiente) ? _nuevaPasswordPendiente : _passwordDesencriptadaActual;
            _servidorEditando.Add(new XElement("Usuario",
                new XAttribute("nombre", TxtUsuario.Text),
                new XElement("Password", Encriptar(pPrincipal), new XAttribute("activa", "true"))));

            // 2. Luego, recorremos el ContenedorListaUsuarios para los adicionales
            foreach (var fila in ContenedorListaUsuarios.Children)
            {
                if (fila is Grid gridFila)
                {
                    TextBox txtUser = gridFila.Children.OfType<TextBox>().FirstOrDefault();
                    var gridPassContainer = gridFila.Children.OfType<Grid>().FirstOrDefault(g => Grid.GetColumn(g) == 1);
                    PasswordBox pbx = gridPassContainer?.Children.OfType<PasswordBox>().FirstOrDefault();

                    if (txtUser != null && pbx != null && !string.IsNullOrEmpty(txtUser.Text))
                    {
                        // Agregamos como elemento Usuario adicional
                        _servidorEditando.Add(new XElement("Usuario",
                            new XAttribute("nombre", txtUser.Text),
                            new XElement("Password", Encriptar(pbx.Password), new XAttribute("activa", "true"))));
                    }
                }
            }

            GuardarXML();
            LiberarLock();
            _lockRenewTimer.Stop();

            CargarDatosXML();
            ScrollModoEdicion.Visibility = Visibility.Collapsed;
            TxtMensajeVacio.Visibility = Visibility.Visible;
        }

        private void BtnCerrarForm_Click(object sender, RoutedEventArgs e)
        {
            LiberarLock(); // Si el usuario cancela, liberamos inmediatamente el recurso
            _lockRenewTimer.Stop();
            ScrollModoEdicion.Visibility = Visibility.Collapsed;
            TxtMensajeVacio.Visibility = Visibility.Visible;
        }

        private void RegistrarEventoHistorial(XElement h, string ac, string au, string f, string uAnt = null, string pAnt = null)
        {
            var ev = new XElement("Evento", new XAttribute("fecha", f), new XAttribute("accion", ac), new XAttribute("autor", au));
            if (uAnt != null) { ev.SetAttributeValue("usuarioAnterior", uAnt); ev.SetAttributeValue("passwordAnterior", pAnt); }
            h.AddFirst(ev);
        }

        private void BtnCambiarPassword_Click(object sender, RoutedEventArgs e) { TxtPopupNueva.Password = TxtPopupConfirm.Password = ""; PanelPopupPassword.Visibility = Visibility.Visible; }
        private void BtnPopupAceptar_Click(object sender, RoutedEventArgs e)
        {
            if (TxtPopupNueva.Password != TxtPopupConfirm.Password) { MessageBox.Show("No coinciden."); return; }
            _nuevaPasswordPendiente = TxtPopupNueva.Password; PanelPopupPassword.Visibility = Visibility.Collapsed;
        }
        private void BtnPopupCancelar_Click(object sender, RoutedEventArgs e) => PanelPopupPassword.Visibility = Visibility.Collapsed;

        private void NuevoOjo1_Down(object sender, MouseButtonEventArgs e) { TxtNuevoPassVisible.Text = TxtNuevoPass.Password; TxtNuevoPass.Visibility = Visibility.Collapsed; TxtNuevoPassVisible.Visibility = Visibility.Visible; }
        private void NuevoOjo1_Up(object sender, EventArgs e) { TxtNuevoPassVisible.Visibility = Visibility.Collapsed; TxtNuevoPass.Visibility = Visibility.Visible; }
        private void NuevoOjo2_Down(object sender, MouseButtonEventArgs e) { TxtNuevoConfirmVisible.Text = TxtNuevoConfirm.Password; TxtNuevoConfirm.Visibility = Visibility.Collapsed; TxtNuevoConfirmVisible.Visibility = Visibility.Visible; }
        private void NuevoOjo2_Up(object sender, EventArgs e) { TxtNuevoConfirmVisible.Visibility = Visibility.Collapsed; TxtNuevoConfirm.Visibility = Visibility.Visible; }

        private void VerPassLectura_Down(object sender, MouseButtonEventArgs e) { if (!string.IsNullOrEmpty(_passwordDesencriptadaActual)) { TxtVerPasswordOculta.Visibility = Visibility.Collapsed; TxtVerPasswordReal.Visibility = Visibility.Visible; } }
        private void VerPassLectura_Up(object sender, EventArgs e) { TxtVerPasswordOculta.Visibility = Visibility.Visible; TxtVerPasswordReal.Visibility = Visibility.Collapsed; }
        private void CopiarPassLectura_Click(object sender, RoutedEventArgs e) { if (!string.IsNullOrEmpty(_passwordDesencriptadaActual)) Clipboard.SetText(_passwordDesencriptadaActual); }
        private void PopupOjo1_Down(object sender, MouseButtonEventArgs e) { TxtPopupNuevaVisible.Text = TxtPopupNueva.Password; TxtPopupNueva.Visibility = Visibility.Collapsed; TxtPopupNuevaVisible.Visibility = Visibility.Visible; }
        private void PopupOjo1_Up(object sender, EventArgs e) { TxtPopupNuevaVisible.Visibility = Visibility.Collapsed; TxtPopupNueva.Visibility = Visibility.Visible; }
        private void PopupOjo2_Down(object sender, MouseButtonEventArgs e) { TxtPopupConfirmVisible.Text = TxtPopupConfirm.Password; TxtPopupConfirm.Visibility = Visibility.Collapsed; TxtPopupConfirmVisible.Visibility = Visibility.Visible; }
        private void PopupOjo2_Up(object sender, EventArgs e) { TxtPopupConfirmVisible.Visibility = Visibility.Collapsed; TxtPopupConfirm.Visibility = Visibility.Visible; }

        private void LimpiarForm() { TxtNombre.Text = TxtIP.Text = TxtURL.Text = TxtUsuario.Text = TxtComentario.Text = TxtDominio.Text = TxtHardwareTipo.Text = TxtLlave.Text = ""; TxtNuevoPass.Password = TxtNuevoConfirm.Password = ""; ComboTipo.SelectedIndex = 0; }

        private void CargarForm(XElement s)
        {
            string t = s.Attribute("tipo")?.Value;
            foreach (ComboBoxItem i in ComboTipo.Items)
                if (i.Content.ToString() == t)
                    ComboTipo.SelectedItem = i;
            TxtNombre.Text = s.Attribute("nombre")?.Value;
            TxtIP.Text = s.Attribute("ip")?.Value;
            TxtURL.Text = s.Attribute("url")?.Value;
            TxtUsuario.Text = s.Element("Usuario")?.Attribute("nombre")?.Value;
            TxtComentario.Text = s.Element("Comentario")?.Value;
            TxtDominio.Text = s.Attribute("dominio")?.Value;
            TxtHardwareTipo.Text = s.Attribute("hw_tipo")?.Value;
            TxtLlave.Text = s.Element("LlaveSSH")?.Value;
            var p = s.Element("Usuario")?.Elements("Password").FirstOrDefault(x => x.Attribute("activa")?.Value == "true");
            _passwordDesencriptadaActual = Desencriptar(p?.Value ?? "");

            // Debajo de: _passwordDesencriptadaActual = Desencriptar(p?.Value ?? "");
            TxtEdicionPassOculta.Text = string.IsNullOrEmpty(_passwordDesencriptadaActual) ? "-" : "••••••••";
        }

        private void GuardarXML()
        {
            try
            {
                string ruta = GetXmlPath();
                if (!string.IsNullOrEmpty(ruta) && _xmlData != null)
                {
                    // Guardado atómico: Guardamos en un archivo temporal primero para evitar que OneDrive corrompa el archivo a la mitad
                    string tempFile = ruta + ".tmp";
                    using (FileStream fs = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        _xmlData.Save(fs);
                    }
                    if (File.Exists(ruta)) File.Delete(ruta);
                    File.Move(tempFile, ruta);
                }
            }
            catch (Exception ex) { MessageBox.Show("Error al guardar el XML: " + ex.Message); }
        }

        private void BtnAddGrupo_Click(object sender, RoutedEventArgs e)
        {
            string n = Microsoft.VisualBasic.Interaction.InputBox("Nombre Grupo:", "Nuevo Grupo");
            if (!string.IsNullOrWhiteSpace(n))
            {
                var g = new XElement("Grupo", new XAttribute("nombre", n));
                var h = new XElement("Historial");
                RegistrarEventoHistorial(h, "Creación de Grupo", Environment.UserName, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                g.Add(h);
                _xmlData?.Root?.Add(g);
                GuardarXML();
                CargarDatosXML();
            }
        }

        private string Encriptar(string t)
        {
            using Aes aes = Aes.Create();
            byte[] k = new byte[32];
            Array.Copy(Encoding.UTF8.GetBytes(MasterKey), k, Math.Min(MasterKey.Length, 32));
            aes.Key = k;
            aes.IV = new byte[16];
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
                aes.Key = k;
                aes.IV = new byte[16];
                using var dec = aes.CreateDecryptor();
                byte[] b = Convert.FromBase64String(b64);
                return Encoding.UTF8.GetString(dec.TransformFinalBlock(b, 0, b.Length));
            }
            catch { return ""; }
        }

        private void EdicionOjo_Down(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Usamos la variable que ya tienes cargada en el formulario
            if (!string.IsNullOrEmpty(_passwordDesencriptadaActual))
            {
                TxtEdicionPassOculta.Visibility = Visibility.Collapsed;
                TxtEdicionPassReal.Text = _passwordDesencriptadaActual;
                TxtEdicionPassReal.Visibility = Visibility.Visible;
            }
        }

        private void EdicionOjo_Up(object sender, EventArgs e)
        {
            TxtEdicionPassOculta.Visibility = Visibility.Visible;
            TxtEdicionPassReal.Visibility = Visibility.Collapsed;
        }

        private void BtnAddUsuario_Click(object sender, RoutedEventArgs e)
        {
            AgregarFilaUsuario();
        }

        // 1. Llama a esto cuando inicies tu formulario (en el constructor o donde cargues los datos)
        // AgregarFilaUsuario(); 

        private void AgregarFilaUsuario()
        {
            Grid nuevaFila = new Grid { Margin = new Thickness(0, 5, 0, 10) };

            // Definimos las columnas exactamente como las quieres
            nuevaFila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
            nuevaFila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            nuevaFila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // --- Usuario ---
            TextBox txtUser = new TextBox { Height = 32, Margin = new Thickness(0, 0, 5, 0) };
            Grid.SetColumn(txtUser, 0);

            // --- Contraseña (Con el Ojo igual al original) ---
            Grid gridPass = new Grid { Margin = new Thickness(5, 0, 5, 0) };
            PasswordBox pbx = new PasswordBox { Height = 32 };
            Button btnOjo = new Button { Content = "👁", Width = 30, HorizontalAlignment = HorizontalAlignment.Right, Background = System.Windows.Media.Brushes.Transparent, BorderThickness = new Thickness(0) };
            gridPass.Children.Add(pbx);
            gridPass.Children.Add(btnOjo);
            Grid.SetColumn(gridPass, 1);

            // --- Confirmar ---
            Grid gridConfirm = new Grid { Margin = new Thickness(5, 0, 0, 0) };
            PasswordBox pbxConfirm = new PasswordBox { Height = 32 };
            Button btnOjo2 = new Button { Content = "👁", Width = 30, HorizontalAlignment = HorizontalAlignment.Right, Background = System.Windows.Media.Brushes.Transparent, BorderThickness = new Thickness(0) };
            gridConfirm.Children.Add(pbxConfirm);
            gridConfirm.Children.Add(btnOjo2);
            Grid.SetColumn(gridConfirm, 2);

            nuevaFila.Children.Add(txtUser);
            nuevaFila.Children.Add(gridPass);
            nuevaFila.Children.Add(gridConfirm);

            ContenedorListaUsuarios.Children.Add(nuevaFila);
        }
    }
}