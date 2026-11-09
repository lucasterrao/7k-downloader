using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Drawing;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Drawing.Drawing2D;

[assembly: AssemblyTitle("7K Downloader")]
[assembly: AssemblyDescription("Aplicativo nativo Windows para downloads de vídeo e áudio")]
[assembly: AssemblyProduct("7K Downloader")]
[assembly: AssemblyVersion("3.3.0.0")]
[assembly: AssemblyFileVersion("3.3.0.0")]

public class DownloadOptions {
    public string mode = "video", format = "mp4", quality = "1080", bitrate = "192", destination = "";
    public bool playlist, subtitles;
    public string[] urls;
}
public class Job {
    public string id, url, title, status, message, destination;
    public double progress, speed, eta;
    public bool requiresAuth;
    public string[] files = new string[0], logs = new string[0];
    public DownloadOptions options = new DownloadOptions();
}
public class AppStatus { public bool ready; public string destination; public int concurrency, apiVersion; public Job[] jobs = new Job[0]; }
public class Preview { public string title, uploader, site; public int[] heights; }
public class AuthSettings { public string cookiesFile = "", proxyUrl = "", subLanguages = "pt.*,en.*"; public int concurrency = 2, retries = 3, timeout = 25, speedLimitKB = 0, fragmentThreads = 1; public bool resume = true; }
public class Settings {
    public bool smart = true, playlist, subtitles;
    public string mode = "video", videoFormat = "mp4", audioFormat = "mp3", quality = "1080", bitrate = "192";
    public string theme = "dark", lastFolder = "";
    public bool preventSleep, background, notify, sound;
}

static class Design {
    public static bool Dark = true;
    public static readonly Color Green = Color.FromArgb(90, 174, 16);
    public static Color Background { get { return Dark ? Color.FromArgb(40,40,40) : Color.White; } }
    public static Color Text { get { return Dark ? Color.FromArgb(235,235,235) : Color.FromArgb(51,56,59); } }
    public static Color Muted { get { return Dark ? Color.FromArgb(151,151,151) : Color.FromArgb(126,132,137); } }
    public static Color Line { get { return Dark ? Color.FromArgb(65,65,65) : Color.FromArgb(226,228,230); } }
    public static Color Pale { get { return Dark ? Color.FromArgb(55,55,55) : Color.FromArgb(246,247,248); } }
    public static Font Font(float size, bool bold = false) { return new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular); }
    public static Button Button(string text, bool primary = false) {
        var b = new Button { Text = text, AccessibleDescription = primary ? "primary" : "", FlatStyle = FlatStyle.Flat, BackColor = primary ? Green : Background, ForeColor = primary ? Color.White : Text, Font = Font(9), Cursor = Cursors.Hand, Height = 34, UseVisualStyleBackColor = false };
        b.FlatAppearance.BorderColor = primary ? Green : Line; b.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(77,155,13) : Pale; return b;
    }
    public static ComboBox Combo(string[] values) { var c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Pale, ForeColor = Text, Font = Font(9), FlatStyle = FlatStyle.Flat, Width = 128 }; c.Items.AddRange(values); c.SelectedIndex = 0; return c; }
    public static Label Label(string text, float size = 9, bool bold = false) { return new Label { Text = text, Font = Font(size, bold), ForeColor = Text, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft }; }
    public static Icon Icon(string root) { string f = Path.Combine(root, "assets", "app.ico"); return File.Exists(f) ? new Icon(f) : SystemIcons.Application; }
    public static void Theme(Control control) {
        if(control.GetType().FullName=="Microsoft.Web.WebView2.WinForms.WebView2")return;
        control.ForeColor = Text;
        if (control is TextBox || control is ComboBox || control is NumericUpDown) control.BackColor = Pale;
        else if (control is Button) { var b = (Button)control; bool primary = b.AccessibleDescription == "primary"; b.BackColor = primary ? Green : Background; b.ForeColor = primary ? Color.White : Text; b.FlatAppearance.BorderColor = primary ? Green : Line; b.FlatAppearance.MouseOverBackColor = Pale; }
        else control.BackColor = control is MenuStrip || control is FlowLayoutPanel || control is Form ? Background : control is Label ? control.Parent == null ? Background : control.Parent.BackColor : Background;
        if (control is ToolStrip) { var strip = (ToolStrip)control; strip.Renderer = new ToolStripProfessionalRenderer(new DarkMenuColors()); ThemeItems(strip.Items); }
        foreach (Control child in control.Controls) Theme(child);
        var form = control as Form; if (form != null) form.HandleCreated += delegate { DarkTitle(form); };
        if (form != null && form.IsHandleCreated) DarkTitle(form);
        control.Invalidate();
    }
    public static void Menu(ContextMenuStrip menu) { menu.BackColor = Background; menu.ForeColor = Text; menu.Renderer = new ToolStripProfessionalRenderer(new DarkMenuColors()); ThemeItems(menu.Items); }
    static void ThemeItems(ToolStripItemCollection items) { foreach (ToolStripItem item in items) { item.ForeColor = Text; item.BackColor = Background; var d = item as ToolStripDropDownItem; if (d != null) { d.DropDown.BackColor = Background; d.DropDown.Renderer = new ToolStripProfessionalRenderer(new DarkMenuColors()); ThemeItems(d.DropDownItems); } } }
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
    public static void DarkTitle(Form form) { try { int v = Dark ? 1 : 0; DwmSetWindowAttribute(form.Handle,20,ref v,4); } catch {} }
    sealed class DarkMenuColors : ProfessionalColorTable {
        public override Color ToolStripDropDownBackground { get { return Background; } }
        public override Color ImageMarginGradientBegin { get { return Background; } }
        public override Color ImageMarginGradientMiddle { get { return Background; } }
        public override Color ImageMarginGradientEnd { get { return Background; } }
        public override Color MenuItemSelected { get { return Pale; } }
        public override Color MenuItemBorder { get { return Line; } }
        public override Color MenuBorder { get { return Line; } }
        public override Color MenuItemPressedGradientBegin { get { return Pale; } }
        public override Color MenuItemPressedGradientMiddle { get { return Pale; } }
        public override Color MenuItemPressedGradientEnd { get { return Pale; } }
    }
}

sealed class Backend : IDisposable {
    readonly string root;
    readonly JavaScriptSerializer json = new JavaScriptSerializer { MaxJsonLength = 20000000 };
    readonly object logLock = new object();
    Process process; Uri address; string token;
    public Backend(string folder) { root = folder; }
    public async Task Start() {
        Directory.CreateDirectory(Path.Combine(root, "data"));
        if (await TrySession()) return;
        string node = Path.Combine(root, "bin", "node.exe"), server = Path.Combine(root, "server.mjs");
        if (!File.Exists(node) || !File.Exists(server)) throw new Exception("Mantenha o executável junto das pastas bin, lib e public e do arquivo server.mjs.");
        File.WriteAllText(Path.Combine(root, "data", "server.log"), "");
        process = new Process { StartInfo = new ProcessStartInfo(node, "\"" + server + "\"") { WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 } };
        process.OutputDataReceived += Log; process.ErrorDataReceived += Log; process.Start(); process.BeginOutputReadLine(); process.BeginErrorReadLine();
        for (int i = 0; i < 80; i++) { if (process.HasExited) throw new Exception("Não foi possível iniciar. Consulte data\\server.log."); if (await TrySession()) return; await Task.Delay(150); }
        throw new Exception("O motor de downloads não respondeu. Consulte data\\server.log.");
    }
    void Log(object sender, DataReceivedEventArgs e) { if (e.Data == null) return; lock (logLock) { try { File.AppendAllText(Path.Combine(root, "data", "server.log"), e.Data + Environment.NewLine, Encoding.UTF8); } catch {} } }
    async Task<bool> TrySession() {
        try {
            var session = json.Deserialize<Dictionary<string, object>>(File.ReadAllText(Path.Combine(root, "data", "session.json"), Encoding.UTF8));
            var url = new Uri((string)session["url"]); if (url.Host != "127.0.0.1" || url.Scheme != "http" || url.Fragment.Length != 65) return false;
            address = new Uri(url.GetLeftPart(UriPartial.Authority) + "/"); token = url.Fragment.Substring(1); var state=await Get<AppStatus>("status");
            if(state.apiVersion<6){if(state.jobs.Any(j=>DownloadRow.Active(j.status)))throw new OldBackendException("Há downloads em andamento na versão anterior. Aguarde ou encerre a versão anterior antes de abrir esta atualização.");await Post("shutdown",new{});await Task.Delay(500);address=null;token=null;return false;}return true;
        } catch(OldBackendException){throw;
        } catch { address = null; token = null; return false; }
    }
    public async Task<T> Get<T>(string route) { return json.Deserialize<T>(await Send(route, null)); }
    public async Task<T> Post<T>(string route, object payload) { return json.Deserialize<T>(await Send(route, payload)); }
    public async Task Post(string route, object payload) { await Send(route, payload); }
    async Task<string> Send(string route, object payload) {
        if (address == null) throw new Exception("Aguarde o aplicativo iniciar.");
        using (var client = new LocalClient()) {
            client.Encoding = Encoding.UTF8; client.Headers["X-7K-Token"] = token; if (payload != null) client.Headers[HttpRequestHeader.ContentType] = "application/json";
            try { var uri = new Uri(address, "api/" + route); return payload == null ? await client.DownloadStringTaskAsync(uri) : await client.UploadStringTaskAsync(uri, "POST", json.Serialize(payload)); }
            catch (WebException e) {
                if (e.Response != null) using (var r = new StreamReader(e.Response.GetResponseStream(), Encoding.UTF8)) {
                    string message = null; try { var error = json.Deserialize<Dictionary<string, object>>(r.ReadToEnd()); message = Convert.ToString(error["error"]); } catch {} if (message != null) throw new Exception(message);
                }
                throw new Exception("Não foi possível comunicar com o motor de downloads. " + e.Message);
            }
        }
    }
    public async Task Stop() {
        if (address != null) try { await Post("shutdown", new {}); } catch {}
        if (process != null) {
            bool ended = await Task.Run(() => { try { return process.HasExited || process.WaitForExit(4000); } catch { return true; } });
            if (!ended) try { var kill = Process.Start(new ProcessStartInfo("taskkill.exe", "/PID " + process.Id + " /T /F") { UseShellExecute = false, CreateNoWindow = true }); if (kill != null) await Task.Run(() => kill.WaitForExit(3000)); } catch {}
        }
        address = null;
    }
    public void Dispose() { if (process != null) process.Dispose(); }
    sealed class LocalClient : WebClient { protected override WebRequest GetWebRequest(Uri uri) { var r = base.GetWebRequest(uri); r.Timeout = 70000; return r; } }
    sealed class OldBackendException : Exception{public OldBackendException(string message):base(message){}}
}

sealed class DownloadRow : Panel {
    public Job Job;
    readonly ServiceTile tile = new ServiceTile { Caption = false };
    readonly Label title = Design.Label("", 10, true), meta = Design.Label("", 8), state = Design.Label("", 8);
    readonly Button action = Design.Button(""), more = Design.Button("•••");
    readonly ContextMenuStrip menu = new ContextMenuStrip();
    Job menuJob;
    public ContextMenuStrip ActionsMenu { get { return menu; } }
    public event Action<Job, string> Command;
    public DownloadRow(Job job) {
        DoubleBuffered = true; Height = 100; Margin = new Padding(0); BackColor = Design.Background;
        tile.BackColor = Design.Pale; tile.Click += delegate { if(Command != null && Job.status == "completed") Command(Job,"open"); };
        title.AutoEllipsis = true; meta.AutoEllipsis = true; state.AutoEllipsis = true; meta.ForeColor = Design.Muted;
        action.FlatAppearance.BorderSize = 0; action.Font = Design.Font(8); more.FlatAppearance.BorderSize = 0; more.Font = Design.Font(10, true);
        Controls.AddRange(new Control[] { tile, title, meta, state, action, more });
        action.Click += delegate { if (Command != null) Command(Job, Active(Job.status) ? "cancel" : Job.status == "completed" ? "open" : Job.requiresAuth ? "auth" : "retry"); };
        AddMenu("Abrir arquivo", "open"); AddMenu("Local do arquivo", "reveal"); AddMenu("Tentar novamente", "retry"); menu.Items.Add(new ToolStripSeparator()); AddMenu("Detalhes do download", "details"); AddMenu("Copiar link", "copy"); menu.Items.Add(new ToolStripSeparator()); AddMenu("Remover do histórico", "remove");
        menu.Opening += delegate { menuJob = Job; foreach(ToolStripItem item in menu.Items) { string command = item.Tag as string; if(command == "open" || command == "reveal") item.Enabled = Job.files != null && Job.files.Length > 0; if(command == "retry" || command == "remove") item.Enabled = !Active(Job.status); } Design.Menu(menu); };
        more.Click += delegate { if (!IsDisposed) menu.Show(more, new Point(0, more.Height)); }; UpdateJob(job);
    }
    void AddMenu(string text, string command) { var item = menu.Items.Add(text); item.Tag = command; item.Click += delegate { var selected = menuJob ?? Job; if(Command != null && !IsDisposed) Command(selected, command); }; }
    protected override void Dispose(bool disposing) { if(disposing) menu.Dispose(); base.Dispose(disposing); }
    public static bool Active(string s) { return s == "queued" || s == "running" || s == "processing"; }
    public void UpdateJob(Job job) {
        Job = job; tile.Service = ServiceTile.Identify(job.url); tile.Invalidate(); title.Text = String.IsNullOrEmpty(job.title) ? "Consultando título…" : job.title;
        string q = job.options.mode == "audio" ? job.options.bitrate + " kbps" : job.options.quality == "best" ? "Melhor qualidade" : job.options.quality + "p";
        string host = ""; Uri u; if (Uri.TryCreate(job.url, UriKind.Absolute, out u)) host = u.Host;
        meta.Text = q + "   ·   " + job.options.format.ToUpperInvariant() + "   ·   " + host;
        var labels = new Dictionary<string, string> { { "queued", "Na fila" }, { "running", "Baixando" }, { "processing", "Finalizando" }, { "completed", "Concluído" }, { "canceled", "Cancelado" }, { "interrupted", "Interrompido" }, { "failed", "Falhou" } };
        string label; if (!labels.TryGetValue(job.status, out label)) label = job.status;
        state.ForeColor = job.status == "failed" ? Color.FromArgb(193, 69, 61) : job.status == "completed" ? Design.Green : Design.Muted;
        state.Text = job.status == "running" ? String.Format("{0:0.0}%   ·   {1:0.0} MB/s{2}", job.progress, job.speed / 1048576, job.eta > 0 ? "   ·   " + Math.Ceiling(job.eta) + "s restantes" : "") : job.status == "processing" ? job.message : label + (job.status == "failed" ? "  —  " + job.message : "");
        action.Text = Active(job.status) ? "Cancelar" : job.status == "completed" ? "Abrir arquivo" : job.requiresAuth ? "Configurar sessão" : "Tentar novamente"; PerformLayout(); Invalidate();
    }
    protected override void OnLayout(LayoutEventArgs e) {
        base.OnLayout(e); tile.SetBounds(8, 18, 96, 58); int x = 122, w = Math.Max(50, ClientSize.Width - 275);
        title.SetBounds(x, 14, w, 24); meta.SetBounds(x, 39, w, 20); state.SetBounds(x, 60, w, 22); action.SetBounds(ClientSize.Width - 138, 26, 105, 30); more.SetBounds(ClientSize.Width - 33, 26, 32, 30);
    }
    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e); using (var p = new Pen(Design.Line)) e.Graphics.DrawLine(p, 0, Height - 1, Width, Height - 1);
        if (Job != null && (Job.status == "running" || Job.status == "processing")) { int w = Math.Max(50, Width - 275); using (var b = new SolidBrush(Color.FromArgb(233, 238, 233))) e.Graphics.FillRectangle(b, 122, 86, w, 3); using (var b = new SolidBrush(Design.Green)) e.Graphics.FillRectangle(b, 122, 86, (int)(w * Math.Min(100, Math.Max(0, Job.progress)) / 100), 3); }
    }
}

sealed class PasteDialog : Form {
    readonly TextBox links = new TextBox(); readonly ComboBox mode, format, quality, bitrate;
    readonly CheckBox playlist = new CheckBox(), subtitles = new CheckBox();
    readonly Label preview = Design.Label("", 9), count = Design.Label("0 links", 8);
    public DownloadOptions Result;
    public PasteDialog(Backend backend, Settings defaults, string initial) {
        Text = "Adicionar downloads"; Font = Design.Font(9); BackColor = Color.White; ClientSize = new Size(540, 510); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; StartPosition = FormStartPosition.CenterParent; AutoScaleMode = AutoScaleMode.Dpi; AutoScaleDimensions = new SizeF(96, 96);
        var heading = Design.Label("Adicionar vídeo ou áudio", 16, true); heading.SetBounds(24, 15, 480, 40); Controls.Add(heading);
        var description = Design.Label("Cole um ou mais links, um por linha (até 30).", 9); description.ForeColor = Design.Muted; description.SetBounds(24, 61, 490, 25); Controls.Add(description);
        links.Multiline = true; links.ScrollBars = ScrollBars.Vertical; links.Font = Design.Font(10); links.SetBounds(24, 95, 492, 115); links.Text = initial ?? ""; Controls.Add(links);
        count.SetBounds(24, 215, 200, 24); Controls.Add(count); var inspect = Design.Button("Consultar link"); inspect.SetBounds(383, 215, 133, 28); Controls.Add(inspect);
        preview.Text="MP4: prioriza H.264/AAC direto do site, até a qualidade escolhida. Pode usar resolução menor para evitar conversão.";preview.SetBounds(24, 246, 492, 48); preview.ForeColor = Design.Muted; preview.AutoEllipsis = true; Controls.Add(preview);
        mode = Design.Combo(new[] { "Vídeo", "Áudio" }); format = Design.Combo(new[] { "MP4", "MKV" }); quality = Design.Combo(new[] { "Melhor disponível", "4K · 2160p", "2K · 1440p", "1080p", "720p", "480p" }); bitrate = Design.Combo(new[] { "320 kbps", "256 kbps", "192 kbps", "128 kbps" });
        Field("Conteúdo", mode, 24, 304, 130); Field("Formato", format, 170, 304, 130); Field("Qualidade", quality, 316, 304, 200); bitrate.SetBounds(316, 330, 200, 26); Controls.Add(bitrate);
        mode.SelectedIndexChanged += delegate { SetMode(defaults); }; mode.SelectedIndex = defaults.mode == "audio" ? 1 : 0; SetMode(defaults);
        quality.SelectedIndex = Math.Max(0, Array.IndexOf(new[] { "best", "2160", "1440", "1080", "720", "480" }, defaults.quality)); bitrate.SelectedIndex = Math.Max(0, Array.IndexOf(new[] { "320", "256", "192", "128" }, defaults.bitrate));
        playlist.Text = "Baixar playlist (até 100 itens)"; playlist.SetBounds(24, 380, 245, 25); playlist.Checked = defaults.playlist; subtitles.Text = "Legendas em português e inglês"; subtitles.SetBounds(270, 380, 255, 25); subtitles.Checked = defaults.subtitles; Controls.AddRange(new Control[] { playlist, subtitles });
        var download = Design.Button("Baixar", true); download.SetBounds(383, 445, 133, 38); Controls.Add(download); var cancel = Design.Button("Cancelar"); cancel.SetBounds(240, 445, 133, 38); cancel.DialogResult = DialogResult.Cancel; Controls.Add(cancel); CancelButton = cancel;
        download.Click += delegate {
            if (Links().Length == 0) { MessageBox.Show(this, "Cole um link primeiro.", Text); return; }
            Result = new DownloadOptions { urls = Links(), mode = mode.SelectedIndex == 0 ? "video" : "audio", format = Convert.ToString(format.SelectedItem).ToLowerInvariant(), quality = new[] { "best", "2160", "1440", "1080", "720", "480" }[quality.SelectedIndex], bitrate = new[] { "320", "256", "192", "128" }[bitrate.SelectedIndex], playlist = playlist.Checked, subtitles = subtitles.Checked }; DialogResult = DialogResult.OK; Close();
        };
        inspect.Click += async delegate {
            if (Links().Length == 0) return; inspect.Enabled = false; preview.Text = "Consultando o site…";
            try { var info = await backend.Post<Preview>("preview", new { urls = new[] { Links()[0] } }); if (!IsDisposed) preview.Text = info.title + "\r\n" + info.site + "   " + info.uploader + (info.heights != null && info.heights.Length > 0 ? "   ·   " + String.Join(" / ", info.heights.Select(h => h + "p")) : ""); } catch (Exception e) { if (!IsDisposed) preview.Text = e.Message; } finally { if (!IsDisposed) inspect.Enabled = true; }
        };
        links.TextChanged += delegate { count.Text = Links().Length + " link(s)"; }; count.Text = Links().Length + " link(s)"; Design.Theme(this); Shown += delegate { links.Focus(); links.Select(links.TextLength, 0); };
    }
    void Field(string text, ComboBox c, int x, int y, int width) { var l = Design.Label(text, 8); l.ForeColor = Design.Muted; l.SetBounds(x, y, width, 22); c.SetBounds(x, y + 26, width, 26); Controls.AddRange(new Control[] { l, c }); }
    void SetMode(Settings defaults) { format.Items.Clear(); format.Items.AddRange(mode.SelectedIndex == 0 ? new[] { "MP4", "MKV" } : new[] { "MP3", "M4A", "Opus" }); format.SelectedIndex = mode.SelectedIndex == 0 ? (defaults.videoFormat == "mkv" ? 1 : 0) : defaults.audioFormat == "m4a" ? 1 : defaults.audioFormat == "opus" ? 2 : 0; quality.Visible = mode.SelectedIndex == 0; bitrate.Visible = mode.SelectedIndex != 0; }
    string[] Links() { return links.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => s.Length > 0).ToArray(); }
}

sealed class MainWindow : Form {
    readonly string root = AppDomain.CurrentDomain.BaseDirectory; readonly Backend backend; readonly JavaScriptSerializer json = new JavaScriptSerializer();
    readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer(); readonly Dictionary<string, DownloadRow> rows = new Dictionary<string, DownloadRow>();
    Settings settings = new Settings(); AppStatus status;
    readonly FlowLayoutPanel list = new FlowLayoutPanel(); readonly Panel listHost = new Panel(), empty = new Panel();
    readonly Label footer = Design.Label("Iniciando…", 8), summary = Design.Label("", 8), notice = Design.Label("", 9);
    readonly Button paste = Design.Button("▣  Colar link", true); readonly CheckBox smart = new ToggleSwitch();
    readonly ComboBox mode, format, quality, bitrate; readonly TextBox search = new TextBox(); readonly List<Button> filters = new List<Button>();
    readonly TableLayoutPanel layout; readonly Panel services = new Panel();
    readonly NotifyIcon tray = new NotifyIcon(); readonly ContextMenuStrip trayMenu = new ContextMenuStrip();
    readonly Dictionary<string,string> knownStatus = new Dictionary<string,string>();
    string filter = "all"; bool polling, closing, started, changing, adding, explicitExit;
    [DllImport("kernel32.dll")] static extern uint SetThreadExecutionState(uint flags);
    [STAThread]
    static void Main() {
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); bool created;
        using (var mutex = new Mutex(true, "Local\\7KDownloaderNative", out created)) {
            if (!created) { MessageBox.Show("O 7K Downloader já está aberto. Confira a barra de tarefas.", "7K Downloader"); return; }
            try { Application.Run(new MainWindow()); } catch (Exception e) { MessageBox.Show(e.Message, "7K Downloader", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
    }
    public MainWindow() {
        backend = new Backend(root); LoadSettings(); Design.Dark = settings.theme != "light"; Text = "7K Video Downloader"; Icon = Design.Icon(root); StartPosition = FormStartPosition.CenterScreen; Font = Design.Font(9); BackColor = Design.Background; ForeColor = Design.Text;
        MinimumSize = new Size(1000, 610); ClientSize = new Size(1120, 740); AutoScaleMode = AutoScaleMode.Dpi; AutoScaleDimensions = new SizeF(96, 96); KeyPreview = true;
        layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Margin = new Padding(0), Padding = new Padding(0) };
        foreach (var height in new[] { 29, 90, 0, 54 }) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); Controls.Add(layout);
        var menu = new MenuStrip { BackColor = Color.White, Dock = DockStyle.Fill, Font = Design.Font(9), Margin = new Padding(0) }; var file = new ToolStripMenuItem("Arquivo");
        file.DropDownItems.Add("Adicionar links", null, delegate { Paste(false); }); file.DropDownItems.Add("Abrir pasta de downloads", null, delegate { OpenFolder(); }); file.DropDownItems.Add(new ToolStripSeparator()); file.DropDownItems.Add("Sair", null, delegate { explicitExit=true; Close(); });
        var edit=new ToolStripMenuItem("Editar");edit.DropDownItems.Add("Colar link",null,delegate{Paste(true);});edit.DropDownItems.Add("Limpar histórico",null,async delegate{await ClearHistory();});
        var viewMenu=new ToolStripMenuItem("Ver");viewMenu.DropDownItems.Add("Todos os downloads",null,delegate{filter="all";Render();});viewMenu.DropDownItems.Add("Em andamento",null,delegate{filter="active";Render();});viewMenu.DropDownItems.Add("Navegador de sites",null,delegate{ShowBrowser("https://www.youtube.com/");});
        var tools = new ToolStripMenuItem("Ferramentas"); tools.DropDownItems.Add("Preferências", null, delegate { Preferences(); }); tools.DropDownItems.Add("Limpar histórico", null, async delegate { await ClearHistory(); });
        var help = new ToolStripMenuItem("Ajuda"); help.DropDownItems.Add("Sobre o 7K Downloader", null, delegate { MessageBox.Show(this, "7K Downloader 3.3\r\n\r\nAplicativo independente com interface nativa Windows.\r\nMotor de downloads: yt-dlp e FFmpeg.\r\nNavegador integrado: Microsoft WebView2.\r\n\r\nMP4 / MKV · MP3 / M4A / Opus", "Sobre", MessageBoxButtons.OK, MessageBoxIcon.Information); }); menu.Items.AddRange(new ToolStripItem[] { file, edit, viewMenu, tools, help }); MainMenuStrip = menu; layout.Controls.Add(menu, 0, 0);
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = Design.Pale, Padding = new Padding(18, 14, 10, 8), WrapContents = false, Margin = new Padding(0) };
        paste.Size = new Size(175, 44); paste.Font = Design.Font(10); paste.Margin = new Padding(0, 10, 15, 0); paste.Enabled = false; paste.Click += delegate { Paste(true); }; toolbar.Controls.Add(paste);
        smart.Text = ""; smart.AccessibleName="Modo inteligente"; smart.Size = new Size(50, 44); smart.Margin = new Padding(0, 10, 14, 0); smart.Checked = settings.smart; smart.CheckedChanged += delegate { settings.smart = smart.Checked; SaveSettings(); }; toolbar.Controls.Add(smart);
        mode = Design.Combo(new[] { "Vídeo", "Áudio" }); format = Design.Combo(new[] { "MP4", "MKV" }); quality = Design.Combo(new[] { "Melhor disponível", "4K · 2160p", "2K · 1440p", "1080p", "720p", "480p" }); bitrate = Design.Combo(new[] { "320 kbps", "256 kbps", "192 kbps", "128 kbps" });
        toolbar.Controls.Add(Field("Transferir", mode, 130)); var qualityBox = Field("Qualidade", quality, 155); bitrate.SetBounds(0, 25, 155, 26); qualityBox.Controls.Add(bitrate); toolbar.Controls.Add(qualityBox); toolbar.Controls.Add(Field("Formato", format, 125));
        var folder = Design.Button("▣  Downloads"); folder.Size = new Size(135, 44); folder.Margin = new Padding(4, 10, 0, 0); folder.FlatAppearance.BorderSize = 0; folder.BackColor = Design.Pale; folder.Click += delegate { OpenFolder(); }; toolbar.Controls.Add(folder);
        var preferences = Design.Button("⚙"); preferences.Size = new Size(42, 44); preferences.Margin = new Padding(0, 10, 0, 0); preferences.BackColor = Design.Pale; preferences.FlatAppearance.BorderSize = 0; preferences.Font = Design.Font(16); preferences.Click += delegate { Preferences(); }; toolbar.Controls.Add(preferences); layout.Controls.Add(toolbar, 0, 1);
        var infoBar = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0), BackColor = Color.FromArgb(240, 247, 240) }; notice.Dock = DockStyle.Fill; notice.Padding = new Padding(20, 0, 0, 0); notice.ForeColor = Color.FromArgb(75, 119, 82); infoBar.Controls.Add(notice); layout.Controls.Add(infoBar, 0, 2);
        var tabs = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0), BackColor = Color.White }; string[] titles = { "Todos", "Vídeos", "Áudios", "Em andamento", "Concluídos" }, values = { "all", "video", "audio", "active", "completed" }; int left = 18;
        for (int i = 0; i < titles.Length; i++) { var b = Design.Button(titles[i]); b.SetBounds(left, 9, i == 3 ? 127 : 95, 36); b.FlatAppearance.BorderSize = 0; b.Tag = values[i]; left += b.Width + 2; b.Click += delegate(object sender, EventArgs e) { filter = (string)((Button)sender).Tag; Render(); }; filters.Add(b); tabs.Controls.Add(b); }
        var searchBox = new Panel { Dock = DockStyle.Right, Width = 370 }; var clear=Design.Button("Limpar");clear.SetBounds(297,10,65,32);clear.FlatAppearance.BorderSize=0;clear.Click+=async delegate{await ClearHistory();};searchBox.Controls.Add(clear);search.SetBounds(64,16,228,24); search.AccessibleName = "Pesquisar downloads"; search.TextChanged += delegate { Render(); }; searchBox.Controls.Add(search); var searchLabel = Design.Label("Pesquisar", 8); searchLabel.SetBounds(0, 16, 62, 24); searchLabel.ForeColor = Design.Muted; searchBox.Controls.Add(searchLabel); tabs.Controls.Add(searchBox); layout.Controls.Add(tabs, 0, 3);
        listHost.Dock = DockStyle.Fill; listHost.Margin = new Padding(0); list.Dock = DockStyle.Fill; list.AutoScroll = true; list.FlowDirection = FlowDirection.TopDown; list.WrapContents = false; list.Padding = new Padding(16, 0, 16, 8); list.BackColor = Color.White; listHost.Controls.Add(list);
        empty.Dock = DockStyle.Fill; empty.BackColor = Design.Background; empty.Paint += PaintEmpty; CreateServices(); listHost.Controls.Add(empty); list.SizeChanged += delegate { ResizeRows(); }; layout.Controls.Add(listHost, 0, 4);
        var statusBar = new TableLayoutPanel { Dock = DockStyle.Fill, Margin = new Padding(0), BackColor = Design.Pale, ColumnCount = 2, Padding = new Padding(18, 0, 18, 0) }; statusBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); statusBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300)); footer.Dock = DockStyle.Fill; footer.AutoEllipsis = true; footer.ForeColor = Design.Muted; summary.Dock = DockStyle.Fill; summary.TextAlign = ContentAlignment.MiddleRight; summary.ForeColor = Design.Muted; statusBar.Controls.Add(footer, 0, 0); statusBar.Controls.Add(summary, 1, 0); layout.Controls.Add(statusBar, 0, 5);
        SetControls(); mode.SelectedIndexChanged += delegate { if (!changing) { settings.mode = mode.SelectedIndex == 0 ? "video" : "audio"; SetControls(); SaveSettings(); } };
        format.SelectedIndexChanged += delegate { if (!changing && format.SelectedIndex >= 0) { if (settings.mode == "video") settings.videoFormat = Convert.ToString(format.SelectedItem).ToLowerInvariant(); else settings.audioFormat = Convert.ToString(format.SelectedItem).ToLowerInvariant(); SaveSettings(); } };
        quality.SelectedIndexChanged += delegate { if (!changing && quality.SelectedIndex >= 0) { settings.quality = new[] { "best", "2160", "1440", "1080", "720", "480" }[quality.SelectedIndex]; SaveSettings(); } };
        bitrate.SelectedIndexChanged += delegate { if (!changing && bitrate.SelectedIndex >= 0) { settings.bitrate = new[] { "320", "256", "192", "128" }[bitrate.SelectedIndex]; SaveSettings(); } };
        timer.Interval = 1500; timer.Tick += async delegate { await Poll(); };
        Shown += async delegate { try { await backend.Start(); if (closing) { await backend.Stop(); return; } started = true; await Poll(); timer.Start(); } catch (Exception e) { if (!closing) { footer.Text = e.Message; MessageBox.Show(this, e.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); } } };
        KeyDown += delegate(object sender, KeyEventArgs e) { if (e.Control && e.KeyCode == Keys.V && !search.Focused) { e.SuppressKeyPress = true; Paste(true); } if (e.Control && e.KeyCode == Keys.O) { e.SuppressKeyPress = true; OpenFolder(); } if (e.Control && e.KeyCode == Keys.F) { e.SuppressKeyPress = true; search.Focus(); } };
        tray.Icon=Icon;tray.Text="7K Downloader";trayMenu.Items.Add("Abrir 7K Downloader",null,delegate{Show();WindowState=FormWindowState.Normal;Activate();});trayMenu.Items.Add("Encerrar",null,delegate{explicitExit=true;Close();});tray.ContextMenuStrip=trayMenu;tray.DoubleClick+=delegate{Show();WindowState=FormWindowState.Normal;Activate();};tray.Visible=true;
        FormClosing += OnClosing;FormClosed+=delegate{SetThreadExecutionState(0x80000000);tray.Visible=false;tray.Dispose();trayMenu.Dispose();timer.Dispose();}; AllowDrop = true; DragEnter += delegate(object sender, DragEventArgs e) { if (e.Data.GetDataPresent(DataFormats.UnicodeText)) e.Effect = DragDropEffects.Copy; }; DragDrop += delegate(object sender, DragEventArgs e) { if (started) ShowPaste(Convert.ToString(e.Data.GetData(DataFormats.UnicodeText))); };
        ApplyTheme();
    }
    Panel Field(string text, ComboBox c, int width) { var p = new Panel { Size = new Size(width, 65), Margin = new Padding(0, 0, 18, 0) }; var l = Design.Label(text, 8); l.ForeColor = Design.Muted; l.SetBounds(0, 0, width, 22); c.SetBounds(0, 25, width, 26); p.Controls.AddRange(new Control[] { l, c }); return p; }
    void ApplyTheme(){Design.Dark=settings.theme!="light";Design.Theme(this);Design.Menu(trayMenu);services.BackColor=Design.Pale;list.BackColor=Design.Background;empty.BackColor=Design.Background;footer.ForeColor=Design.Muted;summary.ForeColor=Design.Muted;foreach(var row in rows.Values){Design.Theme(row);row.UpdateJob(row.Job);}Render();}
    void CreateServices(){
        services.Dock=DockStyle.Bottom;services.Height=300;services.Padding=new Padding(20,14,20,14);services.BackColor=Design.Pale;empty.Controls.Add(services);
        var bar=new TableLayoutPanel{Dock=DockStyle.Top,Height=46,ColumnCount=3};bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,100));bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,130));var label=Design.Label("⌕  Pesquisar",9);label.Dock=DockStyle.Fill;var query=new TextBox{Dock=DockStyle.Fill,Font=Design.Font(10),AccessibleName="Link ou pesquisa no YouTube"};var go=Design.Button("Pesquisar");go.Dock=DockStyle.Fill;bar.Controls.Add(label,0,0);bar.Controls.Add(query,1,0);bar.Controls.Add(go,2,0);services.Controls.Add(bar);
        Action browse=delegate{Uri uri;ShowBrowser(Uri.TryCreate(query.Text.Trim(),UriKind.Absolute,out uri)&&(uri.Scheme=="http"||uri.Scheme=="https")?uri.AbsoluteUri:"https://www.youtube.com/results?search_query="+Uri.EscapeDataString(query.Text.Trim()));};go.Click+=delegate{browse();};query.KeyDown+=delegate(object sender,KeyEventArgs e){if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;browse();}};
        var grid=new TableLayoutPanel{Size=new Size(600,204),ColumnCount=5,RowCount=2};for(int i=0;i<5;i++)grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,20));for(int i=0;i<2;i++)grid.RowStyles.Add(new RowStyle(SizeType.Percent,50));
        string[] names={"YouTube","SoundCloud","Facebook","Vimeo","TikTok","Naver TV","Twitch","Dailymotion","BiliBili","Rumble"};string[] links={"https://www.youtube.com/","https://soundcloud.com/","https://www.facebook.com/","https://vimeo.com/","https://www.tiktok.com/","https://tv.naver.com/","https://www.twitch.tv/","https://www.dailymotion.com/","https://www.bilibili.com/","https://rumble.com/"};
        for(int i=0;i<names.Length;i++){string link=links[i];var icon=new ServiceTile{Service=names[i],AccessibleName=names[i],Dock=DockStyle.Fill,Margin=new Padding(4)};icon.Click+=delegate{ShowBrowser(link);};grid.Controls.Add(icon,i%5,i/5);}services.Controls.Add(grid);grid.BringToFront();services.Resize+=delegate{grid.Location=new Point(Math.Max(0,(services.ClientSize.Width-grid.Width)/2),68);};grid.Location=new Point(200,68);
    }
    void ShowBrowser(string address){if(closing)return;try{string picked=null;using(var browser=new SitesBrowser(root,address,link=>picked=link))browser.ShowDialog(this);if(picked!=null)ShowPaste(picked);}catch(Exception e){if(!closing)MessageBox.Show(this,"Não foi possível abrir os sites integrados.\r\n"+e.Message,Text,MessageBoxButtons.OK,MessageBoxIcon.Warning);}}
    void SetControls() {
        changing = true; mode.SelectedIndex = settings.mode == "audio" ? 1 : 0; format.Items.Clear(); format.Items.AddRange(settings.mode == "video" ? new[] { "MP4", "MKV" } : new[] { "MP3", "M4A", "Opus" }); format.SelectedIndex = settings.mode == "video" ? (settings.videoFormat == "mkv" ? 1 : 0) : settings.audioFormat == "m4a" ? 1 : settings.audioFormat == "opus" ? 2 : 0;
        quality.SelectedIndex = Math.Max(0, Array.IndexOf(new[] { "best", "2160", "1440", "1080", "720", "480" }, settings.quality)); bitrate.SelectedIndex = Math.Max(0, Array.IndexOf(new[] { "320", "256", "192", "128" }, settings.bitrate)); quality.Visible = settings.mode == "video"; bitrate.Visible = settings.mode == "audio"; changing = false; UpdateNotice();
    }
    void UpdateNotice() { notice.Text = "Modo inteligente " + (settings.smart ? "ativado: o link é baixado com as opções acima." : "desativado: escolha as opções ao adicionar um link.") + (settings.playlist ? "   ·   Playlists" : "") + (settings.subtitles ? "   ·   Legendas PT / EN" : ""); }
    void LoadSettings() { try { settings = json.Deserialize<Settings>(File.ReadAllText(Path.Combine(root, "data", "ui-settings.json"), Encoding.UTF8)) ?? new Settings(); } catch {} }
    void SaveSettings() { UpdateNotice(); try { Directory.CreateDirectory(Path.Combine(root, "data")); File.WriteAllText(Path.Combine(root, "data", "ui-settings.json"), json.Serialize(settings), Encoding.UTF8); } catch {} }
    DownloadOptions Current(string[] urls) { return new DownloadOptions { urls = urls, mode = settings.mode, format = settings.mode == "audio" ? settings.audioFormat : settings.videoFormat, quality = settings.quality, bitrate = settings.bitrate, playlist = settings.playlist, subtitles = settings.subtitles }; }
    async void Paste(bool clipboard) {
        if (!started || status == null || !status.ready || closing) return; string text = ""; if (clipboard) try { if (Clipboard.ContainsText()) text = Clipboard.GetText(); } catch {}
        var links = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x.Length > 0).ToArray(); bool valid = links.Length > 0 && links.All(s => { Uri u; return Uri.TryCreate(s, UriKind.Absolute, out u) && (u.Scheme == "http" || u.Scheme == "https"); });
        if (clipboard && smart.Checked && valid) await Add(Current(links)); else ShowPaste(valid ? text : "");
    }
    async void ShowPaste(string initial) { if (closing || !started || status == null || !status.ready) return; using (var d = new PasteDialog(backend, settings, initial)) { d.Icon = Icon; if (d.ShowDialog(this) == DialogResult.OK) await Add(d.Result); } }
    string ChooseDestination(string initial) {
        using(var chooser=new FolderBrowserDialog{Description="Onde deseja salvar os arquivos? Eles serão salvos diretamente nesta pasta.",SelectedPath=Directory.Exists(initial??"")?initial:Directory.Exists(settings.lastFolder)?settings.lastFolder:Path.Combine(root,"downloads"),ShowNewFolderButton=true}) {
            if(chooser.ShowDialog(this)!=DialogResult.OK)return null;settings.lastFolder=chooser.SelectedPath;SaveSettings();return chooser.SelectedPath;
        }
    }
    async Task Add(DownloadOptions options) {
        if(adding||closing)return;adding=true;paste.Enabled=false;
        try { var folder=ChooseDestination(settings.lastFolder);if(folder==null)return;options.destination=folder;await backend.Post("jobs", options); filter = "all"; search.Clear(); await Poll(); }
        catch (Exception e) { if (!closing) MessageBox.Show(this, e.Message, "Não foi possível adicionar", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        finally { adding=false;if (!closing && !IsDisposed) paste.Enabled = status != null && status.ready; }
    }
    async Task Poll() {
        if (polling || closing || !started) return; polling = true;
        try { var result = await backend.Get<AppStatus>("status"); if (closing || IsDisposed) return;
            foreach(var job in result.jobs){string previous;if(knownStatus.TryGetValue(job.id,out previous)&&previous!="completed"&&job.status=="completed"){if(settings.notify){tray.BalloonTipTitle="Download concluído";tray.BalloonTipText=job.title??"Seu arquivo está pronto.";tray.ShowBalloonTip(4000);}if(settings.sound)System.Media.SystemSounds.Asterisk.Play();}knownStatus[job.id]=job.status;}
            status = result; paste.Enabled = status.ready&&!adding; footer.Text = status.ready ? "Salvar em: " + (String.IsNullOrEmpty(settings.lastFolder)?status.destination:settings.lastFolder) : "Componentes ausentes. Execute Instalar componentes.cmd e reabra.";
            SetThreadExecutionState(settings.preventSleep&&status.jobs.Any(j=>DownloadRow.Active(j.status))?0x80000001:0x80000000);Render(); }
        catch (Exception e) { if (!closing && !IsDisposed) { footer.Text = e.Message; paste.Enabled = false; } } finally { polling = false; }
    }
    void Render() {
        foreach (var b in filters) { bool selected = (string)b.Tag == filter; b.ForeColor = selected ? Design.Green : Design.Muted; b.BackColor = selected ? Design.Pale : Design.Background; }
        if (status == null) return; summary.Text = status.jobs.Count(j => DownloadRow.Active(j.status)) + " em andamento   ·   " + status.jobs.Count(j => j.status == "completed") + " concluídos";
        var shown = status.jobs.Where(j => filter == "all" || (filter == "active" ? DownloadRow.Active(j.status) : filter == "completed" ? j.status == "completed" : j.options.mode == filter)).Where(j => ((j.title ?? "") + " " + j.url).IndexOf(search.Text.Trim(), StringComparison.OrdinalIgnoreCase) >= 0).Reverse().ToArray();
        var ids = new HashSet<string>(shown.Select(j => j.id)); list.SuspendLayout(); foreach (var id in rows.Keys.Where(id => !ids.Contains(id)).ToArray()) { list.Controls.Remove(rows[id]); rows[id].Dispose(); rows.Remove(id); }
        for (int i = 0; i < shown.Length; i++) { DownloadRow row; if (!rows.TryGetValue(shown[i].id, out row)) { row = new DownloadRow(shown[i]); Design.Theme(row);row.UpdateJob(shown[i]);row.Command += Command; rows.Add(shown[i].id, row); list.Controls.Add(row); } else row.UpdateJob(shown[i]); list.Controls.SetChildIndex(row, i); }
        ResizeRows(); list.ResumeLayout(); layout.RowStyles[3].Height=status.jobs.Length>0?54:0;empty.Visible = shown.Length == 0; empty.Invalidate(); if (empty.Visible) empty.BringToFront();
    }
    void ResizeRows() { int width = Math.Max(200, list.ClientSize.Width - list.Padding.Horizontal - (list.VerticalScroll.Visible ? SystemInformation.VerticalScrollBarWidth : 0) - 2); foreach (var row in rows.Values) row.Width = width; }
    void PaintEmpty(object sender, PaintEventArgs e) {
        var g = e.Graphics; int y=Math.Max(10,(empty.Height-services.Height)/2-58);var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        using (var f=Design.Font(10))using(var b=new SolidBrush(Design.Muted))g.DrawString("Encontre vídeos para transferir na aplicação",f,b,new RectangleF(20,y,empty.Width-40,30),center);
        using (var f=Design.Font(20))using(var b=new SolidBrush(Design.Text))g.DrawString(filter=="all"?"Clique no ícone do serviço abaixo e comece a\r\npesquisar":"Nenhum download nesta categoria",f,b,new RectangleF(30,y+34,empty.Width-60,95),center);
    }
    async void Command(Job job, string command) {
        try {
            if (command == "auth") Preferences();
            else if (command == "cancel") { await backend.Post("jobs/" + job.id + "/cancel", new {}); await Poll(); }
            else if (command == "retry") {var folder=ChooseDestination(job.destination??settings.lastFolder);if(folder!=null){await backend.Post("jobs/"+job.id+"/retry",new{destination=folder});await Poll();}}
            else if (command == "remove") {await backend.Post("jobs/"+job.id+"/remove",new{});await Poll();}
            else if (command == "copy") Clipboard.SetText(job.url);
            else if (command == "details") using (var d = new Form { Text = "Detalhes do download", Size = new Size(780, 480), StartPosition = FormStartPosition.CenterParent, Icon = Icon }) { d.Controls.Add(new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Font = new Font("Consolas", 9), Text = job.url + "\r\n\r\n" + job.message + "\r\n\r\nArquivos:\r\n"+String.Join("\r\n",job.files??new string[0])+"\r\n\r\n" + String.Join("\r\n", job.logs ?? new string[0]) });Design.Theme(d); d.ShowDialog(this); }
            else if(command=="open"||command=="reveal") {var file=(job.files??new string[0]).FirstOrDefault(File.Exists);if(file==null)throw new FileNotFoundException("O arquivo não foi encontrado. Ele pode ter sido movido ou excluído. Consulte Detalhes para ver o caminho registrado.");if(command=="reveal")FileActions.Reveal(file);else FileActions.Open(file);}
        } catch (Exception e) { if (!closing) MessageBox.Show(this, e.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }
    void OpenFolder() { try { string folder = Directory.Exists(settings.lastFolder)?settings.lastFolder:status == null ? Path.Combine(root, "downloads") : status.destination; Directory.CreateDirectory(folder); Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true }); } catch (Exception e) { MessageBox.Show(this, e.Message, Text); } }
    async Task ClearHistory() { if (started && !closing) try { await backend.Post("clear", new {}); await Poll(); } catch (Exception e) { MessageBox.Show(this, e.Message, Text); } }
    async void Preferences() {
        if(!started||closing)return;
        try {var auth=await backend.Get<AuthSettings>("settings");if(closing||IsDisposed)return;using(var dialog=new PreferencesDialog(settings,auth,updated=>backend.Post("settings",updated))){dialog.Icon=Icon;if(dialog.ShowDialog(this)==DialogResult.OK){settings=dialog.Result;SaveSettings();ApplyTheme();await Poll();}}}
        catch(Exception e){if(!closing)MessageBox.Show(this,e.Message,"Preferências",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
    }
    async void OnClosing(object sender, FormClosingEventArgs e) {
        if (closing) return;
        if(settings.background&&!explicitExit&&e.CloseReason==CloseReason.UserClosing){e.Cancel=true;Hide();return;}
        if (status != null && status.jobs.Any(j => DownloadRow.Active(j.status)) && MessageBox.Show(this, "Há downloads em andamento. Encerrar e interrompê-los? Você poderá tentar novamente ao reabrir.", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) { e.Cancel = true; return; }
        e.Cancel = true; closing = true; timer.Stop(); paste.Enabled = false; Enabled = false; footer.Text = "Encerrando…"; await backend.Stop(); backend.Dispose(); SaveSettings(); Close();
    }
}
