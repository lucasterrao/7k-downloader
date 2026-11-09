using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Web.WebView2.Core;

static class FileActions {
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)]static extern int SHParseDisplayName(string name,IntPtr binding,out IntPtr pidl,uint mask,out uint attributes);
    [DllImport("shell32.dll")]static extern int SHOpenFolderAndSelectItems(IntPtr folder,uint count,[MarshalAs(UnmanagedType.LPArray,SizeParamIndex=1)]IntPtr[] items,uint flags);
    [DllImport("shell32.dll")]static extern IntPtr ILFindLastID(IntPtr pidl);
    public static void Open(string file){string full=Path.GetFullPath(file);if(!System.Text.RegularExpressions.Regex.IsMatch(full,@"\.(mp4|mkv|mp3|m4a|opus|webm|mov|avi|flv|ogg|wav|aac)$",System.Text.RegularExpressions.RegexOptions.IgnoreCase))throw new InvalidOperationException("Abertura bloqueada: somente arquivos de áudio e vídeo são permitidos.");if(!File.Exists(full))throw new FileNotFoundException("Arquivo não encontrado.",full);Process.Start(new ProcessStartInfo(full){UseShellExecute=true,WorkingDirectory=Path.GetDirectoryName(full)});}
    public static void Reveal(string file){string full=Path.GetFullPath(file);if(!File.Exists(full))throw new FileNotFoundException("Arquivo não encontrado.",full);IntPtr folder=IntPtr.Zero,item=IntPtr.Zero;uint attributes;try{Marshal.ThrowExceptionForHR(SHParseDisplayName(Path.GetDirectoryName(full),IntPtr.Zero,out folder,0,out attributes));Marshal.ThrowExceptionForHR(SHParseDisplayName(full,IntPtr.Zero,out item,0,out attributes));Marshal.ThrowExceptionForHR(SHOpenFolderAndSelectItems(folder,1,new[]{ILFindLastID(item)},0));}finally{if(folder!=IntPtr.Zero)Marshal.FreeCoTaskMem(folder);if(item!=IntPtr.Zero)Marshal.FreeCoTaskMem(item);}}
}

sealed class ToggleSwitch : CheckBox {
    public ToggleSwitch() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); Cursor = Cursors.Hand; Height = 42; }
    protected override void OnPaint(PaintEventArgs e) {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent == null ? Design.Background : Parent.BackColor);
        int x = String.IsNullOrEmpty(Text) ? (Width - 42) / 2 : Width - 46, y = (Height - 24) / 2;
        using(var path = new GraphicsPath()) { path.AddArc(x,y,24,24,90,180); path.AddArc(x+18,y,24,24,270,180); path.CloseFigure(); using(var b = new SolidBrush(Checked ? Design.Green : Color.FromArgb(85,85,85))) g.FillPath(b,path); }
        using(var b = new SolidBrush(Color.White)) g.FillEllipse(b,x+(Checked ? 22 : 3),y+3,18,18);
        if(!String.IsNullOrEmpty(Text)) TextRenderer.DrawText(g,Text,Font,new Rectangle(0,0,Math.Max(1,x-8),Height),Design.Text,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.WordBreak);
        if(Focused) ControlPaint.DrawFocusRectangle(g,ClientRectangle);
    }
}

sealed class ServiceTile : Button {
    public string Service = "YouTube";
    public bool Caption = true;
    bool hover;
    public ServiceTile() { FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; Cursor = Cursors.Hand; Size = new Size(100,90); Font = Design.Font(8); MouseEnter += delegate { hover=true; Invalidate(); }; MouseLeave += delegate { hover=false; Invalidate(); }; }
    public static string Identify(string link) { Uri u; if(!Uri.TryCreate(link,UriKind.Absolute,out u)) return "Vídeo"; string h=u.Host.ToLowerInvariant(); if(h.Contains("youtube")||h=="youtu.be")return "YouTube"; if(h.Contains("soundcloud"))return "SoundCloud"; if(h.Contains("facebook")||h=="fb.watch")return "Facebook"; if(h.Contains("vimeo"))return "Vimeo"; if(h.Contains("tiktok"))return "TikTok"; if(h.Contains("twitch"))return "Twitch"; if(h.Contains("dailymotion")||h=="dai.ly")return "Dailymotion"; if(h.Contains("bilibili")||h=="b23.tv")return "BiliBili"; if(h.Contains("rumble"))return "Rumble"; if(h.Contains("naver"))return "Naver TV"; return "Vídeo"; }
    protected override void OnPaint(PaintEventArgs e) {
        var g=e.Graphics; g.SmoothingMode=SmoothingMode.AntiAlias; g.Clear(hover?Color.FromArgb(67,67,67):BackColor);
        int x=(Width-44)/2,y=Caption?15:(Height-40)/2;
        switch(Service) {
            case "YouTube": using(var p=new GraphicsPath()){p.AddArc(x,y+5,12,12,180,90);p.AddArc(x+32,y+5,12,12,270,90);p.AddArc(x+32,y+25,12,12,0,90);p.AddArc(x,y+25,12,12,90,90);p.CloseFigure();using(var b=new SolidBrush(Color.Red))g.FillPath(b,p);} g.FillPolygon(Brushes.White,new[]{new Point(x+18,y+13),new Point(x+18,y+29),new Point(x+31,y+21)});break;
            case "SoundCloud": using(var b=new SolidBrush(Color.FromArgb(255,119,0))){g.FillEllipse(b,x+12,y+8,23,23);g.FillEllipse(b,x+26,y+20,18,14);g.FillRectangle(b,x+15,y+20,24,14);for(int i=0;i<5;i++)g.FillRectangle(b,x+i*3,y+20-i*2,2,14+i*2);} break;
            case "Facebook": using(var b=new SolidBrush(Color.FromArgb(24,119,242)))g.FillEllipse(b,x+2,y,40,40); using(var f=new Font("Segoe UI",30,FontStyle.Bold))g.DrawString("f",f,Brushes.White,x+9,y-5);break;
            case "Vimeo": Letter(g,"v",Color.FromArgb(26,183,234),x,y,30,FontStyle.Bold|FontStyle.Italic);break;
            case "TikTok": Letter(g,"♪",Color.FromArgb(255,0,80),x+3,y+1,31,FontStyle.Bold);Letter(g,"♪",Color.FromArgb(0,242,234),x-2,y-1,31,FontStyle.Bold);Letter(g,"♪",Design.Text,x,y,31,FontStyle.Bold);break;
            case "Twitch": using(var p=new Pen(Color.FromArgb(145,70,255),4)){g.DrawPolygon(p,new[]{new Point(x+5,y+3),new Point(x+40,y+3),new Point(x+40,y+29),new Point(x+24,y+29),new Point(x+14,y+39),new Point(x+14,y+29),new Point(x+5,y+29)});g.DrawLine(p,x+21,y+12,x+21,y+22);g.DrawLine(p,x+30,y+12,x+30,y+22);}break;
            case "Dailymotion": Letter(g,"d",Color.FromArgb(0,102,220),x,y,29,FontStyle.Bold);break;
            case "BiliBili": using(var p=new Pen(Color.FromArgb(0,174,236),3)){g.DrawRectangle(p,x+2,y+9,39,27);g.DrawLine(p,x+11,y,x+18,y+9);g.DrawLine(p,x+33,y,x+27,y+9);g.DrawLine(p,x+13,y+17,x+13,y+26);g.DrawLine(p,x+29,y+17,x+29,y+26);}break;
            case "Naver TV": using(var p=new Pen(Color.FromArgb(0,210,157),9))g.DrawPolygon(p,new[]{new Point(x+8,y+6),new Point(x+8,y+34),new Point(x+35,y+20)});break;
            case "Rumble": using(var p=new Pen(Color.FromArgb(117,201,24),9))g.DrawPolygon(p,new[]{new Point(x+8,y+6),new Point(x+8,y+34),new Point(x+35,y+20)});break;
            default: using(var b=new SolidBrush(Design.Green))g.FillPolygon(b,new[]{new Point(x+10,y+6),new Point(x+10,y+34),new Point(x+35,y+20)});break;
        }
        if(Caption) TextRenderer.DrawText(g,Service,Font,new Rectangle(0,60,Width,26),Design.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
        if(Focused) ControlPaint.DrawFocusRectangle(g,ClientRectangle);
    }
    static void Letter(Graphics g,string text,Color color,int x,int y,float size,FontStyle style){using(var f=new Font("Segoe UI",size,style))using(var b=new SolidBrush(color))g.DrawString(text,f,b,x,y-5);}
}

sealed class SitesBrowser : Form {
    readonly WebView2 view = new WebView2(); readonly TextBox address = new TextBox(); readonly Button transfer = Design.Button("Baixar página atual",true);
    readonly string root,initial; readonly Action<string> download;
    public SitesBrowser(string folder,string first,Action<string> callback) {
        root=folder; initial=first; download=callback; Text="Sites — 7K Downloader"; Size=new Size(1120,790); MinimumSize=new Size(850,560); StartPosition=FormStartPosition.CenterParent; Icon=Design.Icon(root); Font=Design.Font(9);
        var top=new TableLayoutPanel{Dock=DockStyle.Top,Height=48,Padding=new Padding(8),ColumnCount=5}; top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,42));top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,42));top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,82));top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,170));
        Button back=Design.Button("←"),forward=Design.Button("→"),go=Design.Button("Ir");address.Dock=DockStyle.Fill;address.Text=initial;transfer.Dock=DockStyle.Fill;transfer.Enabled=false;top.Controls.Add(back,0,0);top.Controls.Add(forward,1,0);top.Controls.Add(address,2,0);top.Controls.Add(go,3,0);top.Controls.Add(transfer,4,0);
        view.Dock=DockStyle.Fill;view.DefaultBackgroundColor=Design.Background;Controls.Add(view);Controls.Add(top);Design.Theme(this);
        back.Click+=delegate{if(view.CanGoBack)view.GoBack();};forward.Click+=delegate{if(view.CanGoForward)view.GoForward();};go.Click+=delegate{Navigate();};address.KeyDown+=delegate(object s,KeyEventArgs e){if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;Navigate();}};
        transfer.Click+=delegate{if(view.Source!=null){download(view.Source.AbsoluteUri);DialogResult=DialogResult.OK;Close();}};
        Shown+=async delegate{try{var env=await CoreWebView2Environment.CreateAsync(null,Path.Combine(root,"data","browser"));if(IsDisposed)return;await view.EnsureCoreWebView2Async(env);if(IsDisposed)return;view.CoreWebView2.Settings.IsStatusBarEnabled=false;view.CoreWebView2.Settings.AreHostObjectsAllowed=false;view.CoreWebView2.NavigationStarting+=delegate(object s,CoreWebView2NavigationStartingEventArgs e){Uri uri;if(!Uri.TryCreate(e.Uri,UriKind.Absolute,out uri)||(uri.Scheme!="http"&&uri.Scheme!="https"))e.Cancel=true;};view.CoreWebView2.DownloadStarting+=delegate(object s,CoreWebView2DownloadStartingEventArgs e){e.Cancel=true;};view.SourceChanged+=delegate{if(!IsDisposed&&view.Source!=null)address.Text=view.Source.AbsoluteUri;};view.CoreWebView2.NewWindowRequested+=delegate(object s,CoreWebView2NewWindowRequestedEventArgs e){e.Handled=true;view.CoreWebView2.Navigate(e.Uri);};transfer.Enabled=true;view.CoreWebView2.Navigate(initial);}catch(Exception e){if(!IsDisposed)MessageBox.Show(this,"Não foi possível iniciar o navegador integrado. Verifique o Microsoft Edge WebView2 Runtime.\r\n"+e.Message,Text);}};
        FormClosed+=delegate{view.Dispose();};
    }
    void Navigate(){if(view.CoreWebView2==null)return;Uri uri;if(Uri.TryCreate(address.Text.Trim(),UriKind.Absolute,out uri)&&(uri.Scheme=="http"||uri.Scheme=="https"))view.CoreWebView2.Navigate(uri.AbsoluteUri);else view.CoreWebView2.Navigate("https://www.youtube.com/results?search_query="+Uri.EscapeDataString(address.Text.Trim()));}
}

sealed class PreferencesDialog : Form {
    public Settings Result; public AuthSettings Connection;
    readonly Panel content=new Panel(); readonly Panel[] pages=new Panel[5]; readonly Button[] nav=new Button[5];
    readonly ComboBox theme=Design.Combo(new[]{"Escuro","Claro"}),languages=Design.Combo(new[]{"Português e inglês","Português","Inglês","Todos"});
    readonly ToggleSwitch playlists=new ToggleSwitch(),subs=new ToggleSwitch(),sleep=new ToggleSwitch(),background=new ToggleSwitch(),resume=new ToggleSwitch(),notify=new ToggleSwitch(),sound=new ToggleSwitch();
    readonly NumericUpDown concurrent=Number(1,8),fragments=Number(1,8),retries=Number(0,10),timeout=Number(10,120),rate=Number(0,100000);
    readonly TextBox cookies=new TextBox{ReadOnly=true},proxy=new TextBox();
    public PreferencesDialog(Settings current,AuthSettings auth,Func<AuthSettings,System.Threading.Tasks.Task> save) {
        Text="Preferências";ClientSize=new Size(820,590);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;StartPosition=FormStartPosition.CenterParent;Font=Design.Font(9);AutoScaleMode=AutoScaleMode.Dpi;AutoScaleDimensions=new SizeF(96,96);
        var side=new Panel{Dock=DockStyle.Left,Width=190,Padding=new Padding(12,15,12,12)};content.Dock=DockStyle.Fill;content.Padding=new Padding(28,20,28,12);var bottom=new Panel{Dock=DockStyle.Bottom,Height=67};Controls.Add(content);Controls.Add(side);Controls.Add(bottom);
        string[] names={"⚙   Geral","☷   Avançadas","☰   Conexão","♧   Notificações","♙   Autorização"};for(int i=0;i<5;i++){int index=i;nav[i]=Design.Button(names[i]);nav[i].SetBounds(12,15+i*49,165,42);nav[i].TextAlign=ContentAlignment.MiddleLeft;nav[i].FlatAppearance.BorderSize=0;nav[i].Click+=delegate{SelectPage(index);};side.Controls.Add(nav[i]);pages[i]=new Panel{Dock=DockStyle.Fill,AutoScroll=true};content.Controls.Add(pages[i]);}
        Header(0,"Geral");Label(0,"Aparência",60);theme.SetBounds(0,88,260,28);pages[0].Controls.Add(theme);theme.SelectedIndex=current.theme=="light"?1:0;
        Toggle(0,background,"Continuar em segundo plano ao fechar a janela",155,current.background);Label(0,"Reabra o programa pelo ícone na área de notificação.",205,true);Label(0,"Os arquivos são salvos diretamente na pasta escolhida.\r\nA pasta de destino é solicitada antes de cada envio.",275,true);
        Header(1,"Avançadas");Numeric(1,"Transferências simultâneas",concurrent,60,auth.concurrency);Numeric(1,"Fragmentos simultâneos por arquivo",fragments,112,auth.fragmentThreads);Toggle(1,sleep,"Impedir suspensão durante as transferências",172,current.preventSleep);Toggle(1,resume,"Retomar arquivos parciais",224,auth.resume);Toggle(1,playlists,"Baixar playlists (até 100 itens por link)",276,current.playlist);Toggle(1,subs,"Salvar legendas disponíveis",328,current.subtitles);Label(1,"Idiomas das legendas",391);languages.SetBounds(220,391,305,28);pages[1].Controls.Add(languages);languages.SelectedIndex=Array.IndexOf(new[]{"pt.*,en.*","pt.*","en.*","all"},auth.subLanguages);if(languages.SelectedIndex<0)languages.SelectedIndex=0;
        Header(2,"Conexão");Numeric(2,"Tentativas em caso de falha",retries,60,auth.retries);Numeric(2,"Tempo limite da conexão (segundos)",timeout,112,auth.timeout);Numeric(2,"Limite de velocidade (KB/s; 0 = ilimitado)",rate,164,auth.speedLimitKB);Label(2,"Proxy HTTP / HTTPS / SOCKS",238);proxy.SetBounds(0,271,530,28);proxy.Text=auth.proxyUrl??"";pages[2].Controls.Add(proxy);Label(2,"Opcional. Exemplo: http://127.0.0.1:8080\r\nDeixe vazio para usar uma conexão direta.",316,true);
        Header(3,"Notificações");Toggle(3,notify,"Mostrar notificação ao concluir um download",65,current.notify);Toggle(3,sound,"Reproduzir um som ao concluir",125,current.sound);Label(3,"O ícone de notificação também permite reabrir e encerrar\r\no aplicativo quando ele está em segundo plano.",197,true);
        Header(4,"Autorização");Label(4,"Sessão do YouTube e outros sites",65);Label(4,"Selecione cookies.txt exportado por você no formato Netscape.\r\nEste arquivo contém sua sessão: não o compartilhe.",112,true);cookies.SetBounds(0,183,530,28);cookies.Text=auth.cookiesFile??"";pages[4].Controls.Add(cookies);Button browse=Design.Button("Selecionar cookies.txt"),remove=Design.Button("Remover sessão");browse.SetBounds(0,229,210,36);remove.SetBounds(224,229,165,36);pages[4].Controls.AddRange(new Control[]{browse,remove});browse.Click+=delegate{using(var chooser=new OpenFileDialog{Title="Selecione seus cookies Netscape",Filter="Cookies (*.txt)|*.txt|Todos (*.*)|*.*",CheckFileExists=true})if(chooser.ShowDialog(this)==DialogResult.OK)cookies.Text=chooser.FileName;};remove.Click+=delegate{cookies.Clear();};
        Button accept=Design.Button("Salvar",true),cancel=Design.Button("Cancelar");accept.SetBounds(665,16,125,36);cancel.SetBounds(528,16,125,36);cancel.DialogResult=DialogResult.Cancel;bottom.Controls.AddRange(new Control[]{accept,cancel});CancelButton=cancel;AcceptButton=accept;
        accept.Click+=async delegate{accept.Enabled=false;try{var updated=new AuthSettings{cookiesFile=cookies.Text,proxyUrl=proxy.Text.Trim(),subLanguages=new[]{"pt.*,en.*","pt.*","en.*","all"}[languages.SelectedIndex],concurrency=(int)concurrent.Value,fragmentThreads=(int)fragments.Value,retries=(int)retries.Value,timeout=(int)timeout.Value,speedLimitKB=(int)rate.Value,resume=resume.Checked};await save(updated);if(IsDisposed)return;Connection=updated;Result=new Settings{smart=current.smart,mode=current.mode,videoFormat=current.videoFormat,audioFormat=current.audioFormat,quality=current.quality,bitrate=current.bitrate,lastFolder=current.lastFolder,theme=theme.SelectedIndex==1?"light":"dark",playlist=playlists.Checked,subtitles=subs.Checked,preventSleep=sleep.Checked,background=background.Checked,notify=notify.Checked,sound=sound.Checked};DialogResult=DialogResult.OK;Close();}catch(Exception e){if(!IsDisposed)MessageBox.Show(this,e.Message,"Não foi possível salvar",MessageBoxButtons.OK,MessageBoxIcon.Warning);}finally{if(!IsDisposed)accept.Enabled=true;}};
        Design.Theme(this);SelectPage(0);
    }
    static NumericUpDown Number(int min,int max){return new NumericUpDown{Minimum=min,Maximum=max,Width=130};}
    void Header(int i,string text){var h=Design.Label(text,14);h.SetBounds(0,0,520,40);pages[i].Controls.Add(h);}
    void Label(int i,string text,int y,bool muted=false){var l=Design.Label(text,9);l.SetBounds(0,y,530,muted?60:28);if(muted)l.ForeColor=Design.Muted;pages[i].Controls.Add(l);}
    void Toggle(int i,ToggleSwitch c,string text,int y,bool value){c.Text=text;c.Checked=value;c.SetBounds(0,y,530,42);pages[i].Controls.Add(c);}
    void Numeric(int i,string text,NumericUpDown c,int y,int value){Label(i,text,y);c.SetBounds(387,y,140,28);c.Value=Math.Max(c.Minimum,Math.Min(c.Maximum,value));pages[i].Controls.Add(c);}
    void SelectPage(int index){for(int i=0;i<5;i++){pages[i].Visible=i==index;nav[i].BackColor=i==index?Design.Pale:Design.Background;}pages[index].BringToFront();}
}
