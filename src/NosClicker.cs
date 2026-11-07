using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]
namespace NosClicker {
static class Program {
 [STAThread] static int Main(string[] args) {
  if(args.Length>0 && args[0]=="--self-test") return Tests.Run();
  Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Application.Run(new MainForm()); return 0;
 }
}
static class Native {
 [DllImport("user32.dll",SetLastError=true)] internal static extern bool RegisterHotKey(IntPtr h,int id,uint mod,uint key);
 [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr h,int id);
 [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll",SetLastError=true)] internal static extern uint SendInput(uint count, INPUT[] inputs,int size);
 [StructLayout(LayoutKind.Sequential)] internal struct INPUT { public uint type; public UNION data; }
 [StructLayout(LayoutKind.Explicit)] internal struct UNION { [FieldOffset(0)] public MOUSE mouse; }
 [StructLayout(LayoutKind.Sequential)] internal struct MOUSE { public int x,y; public uint data,flags,time; public UIntPtr extra; }
 internal static INPUT[] Pair(bool right) { return new INPUT[]{new INPUT{data=new UNION{mouse=new MOUSE{flags=right?8u:2u}}},new INPUT{data=new UNION{mouse=new MOUSE{flags=right?16u:4u}}}}; }
}
sealed class Engine : IDisposable {
 readonly ManualResetEvent cancel=new ManualResetEvent(false); Thread thread;
 internal long Count; internal volatile bool Failed;
 internal static double Interval(int cps) { if(cps<1||cps>1000) throw new ArgumentOutOfRangeException("cps"); return (double)Stopwatch.Frequency/cps; }
 internal void Start(bool right,int cps,bool unlimited,IntPtr own) {
  Stop(); cancel.Reset(); Failed=false; var pair=Native.Pair(right); var size=Marshal.SizeOf(typeof(Native.INPUT)); double interval=Interval(cps);
  thread=new Thread(delegate(){ double next=Stopwatch.GetTimestamp(); while(!cancel.WaitOne(0)) {
   if(Native.GetForegroundWindow()==own) { if(cancel.WaitOne(20)) break; next=Stopwatch.GetTimestamp(); continue; }
   long now=Stopwatch.GetTimestamp(); if(!unlimited && now<next) { double ms=(next-now)*1000/Stopwatch.Frequency; if(ms>2) cancel.WaitOne((int)ms-1); else Thread.SpinWait(60); continue; }
   if(Native.SendInput(2,pair,size)!=2) { Failed=true; break; } Interlocked.Increment(ref Count);
   next=Math.Max(next+interval,Stopwatch.GetTimestamp()); if(unlimited) Thread.Yield();
  }}); thread.IsBackground=true; thread.Start();
 }
 internal void Stop(){cancel.Set(); if(thread!=null){thread.Join();thread=null;}}
 public void Dispose(){Stop();cancel.Dispose();}
}
sealed class MainForm : Form {
 readonly Color mint=Color.FromArgb(191,242,129), panel=Color.FromArgb(29,41,34), muted=Color.FromArgb(157,177,164);
 readonly Engine engine=new Engine(); readonly Button start=new Button(), bind=new Button(); readonly Label status=new Label(), counter=new Label();
 readonly NumericUpDown cps=new NumericUpDown(); readonly CheckBox unlimited=new CheckBox(); readonly RadioButton left=new RadioButton(),right=new RadioButton();
 readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer(); Keys hotkey=Keys.F6; bool active,capturing; readonly string config=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"NosClicker","settings.txt");
 Label TextAt(string text,int x,int y,int w,int size,Color color){var l=new Label{Text=text,Location=new Point(x,y),Size=new Size(w,size+18),ForeColor=color,Font=new Font("Segoe UI",size)}; Controls.Add(l); return l;}
 void ButtonStyle(Button b){b.FlatStyle=FlatStyle.Flat;b.FlatAppearance.BorderSize=0;b.BackColor=panel;b.ForeColor=Color.White;b.Cursor=Cursors.Hand;b.Font=new Font("Segoe UI",11,FontStyle.Bold);Controls.Add(b);}
 internal MainForm(){
  Text="NOS / Clicker"; ClientSize=new Size(460,580); FormBorderStyle=FormBorderStyle.FixedSingle; MaximizeBox=false; StartPosition=FormStartPosition.CenterScreen; BackColor=Color.FromArgb(17,26,21); ForeColor=Color.White; Font=new Font("Segoe UI",11); KeyPreview=true;
  TextAt("NOS",30,24,200,32,mint); TextAt("CLICKER  /  1.0",32,83,300,10,muted);
  TextAt("Меньше действий. Больше кликов.",32,118,400,14,Color.White);
  TextAt("01  /  КНОПКА МЫШИ",32,174,390,10,muted);
  left.Text="ЛКМ";right.Text="ПКМ";left.Checked=true;left.SetBounds(32,208,180,38);right.SetBounds(240,208,180,38);Controls.Add(left);Controls.Add(right);
  TextAt("02  /  СКОРОСТЬ",32,267,350,10,muted);
  cps.SetBounds(32,303,120,35);cps.Minimum=1;cps.Maximum=1000;cps.Value=20;cps.BackColor=panel;cps.ForeColor=mint;cps.Font=new Font("Segoe UI",15);Controls.Add(cps);
  TextAt("кликов / сек",164,310,150,10,muted);
  unlimited.Text="Без лимита";unlimited.SetBounds(300,304,140,34);Controls.Add(unlimited);unlimited.CheckedChanged+=delegate{cps.Enabled=!unlimited.Checked;};
  TextAt("03  /  ГОРЯЧАЯ КЛАВИША",32,365,340,10,muted);
  bind.SetBounds(32,399,120,39);ButtonStyle(bind);bind.Click+=delegate{Stop();capturing=true;bind.Text="Нажмите…";};
  TextAt("Запуск / пауза · Esc — стоп",168,408,280,10,muted);
  start.SetBounds(32,468,396,54);ButtonStyle(start);start.BackColor=mint;start.ForeColor=BackColor;start.Click+=delegate{Toggle();};
  status.SetBounds(32,539,230,25);status.ForeColor=muted;Controls.Add(status);counter.SetBounds(270,539,158,25);counter.TextAlign=ContentAlignment.TopRight;counter.ForeColor=muted;Controls.Add(counter);
  LoadSettings(); timer.Interval=150;timer.Tick+=delegate{counter.Text=Interlocked.Read(ref engine.Count).ToString("N0")+" кликов";if(active&&engine.Failed){Stop();status.Text="Нет доступа к вводу";}};timer.Start();UpdateState();
 }
 protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);if(!Native.RegisterHotKey(Handle,1,0x4000,(uint)hotkey)){status.Text="Клавиша занята — смените";}Native.RegisterHotKey(Handle,2,0x4000,(uint)Keys.Escape);}
 protected override void OnHandleDestroyed(EventArgs e){Native.UnregisterHotKey(Handle,1);Native.UnregisterHotKey(Handle,2);base.OnHandleDestroyed(e);}
 protected override void WndProc(ref Message m){if(m.Msg==0x312){if(m.WParam.ToInt32()==2){capturing=false;Stop();}else if(!capturing)Toggle();}base.WndProc(ref m);}
 protected override bool ProcessCmdKey(ref Message msg,Keys keyData){if(capturing){Keys k=keyData&Keys.KeyCode;if(k==Keys.Escape){capturing=false;UpdateState();return true;}if((k>=Keys.F1&&k<=Keys.F12)||(k>=Keys.A&&k<=Keys.Z)||(k>=Keys.D0&&k<=Keys.D9)||k==Keys.Space||k==Keys.Insert||k==Keys.Home||k==Keys.End){Native.UnregisterHotKey(Handle,1);if(Native.RegisterHotKey(Handle,1,0x4000,(uint)k)){hotkey=k;capturing=false;UpdateState();}else{Native.RegisterHotKey(Handle,1,0x4000,(uint)hotkey);bind.Text="Занята";}}return true;}return base.ProcessCmdKey(ref msg,keyData);}
 void Toggle(){if(active){Stop();return;}capturing=false;engine.Start(right.Checked,(int)cps.Value,unlimited.Checked,Handle);active=true;UpdateState();}
 void Stop(){engine.Stop();active=false;UpdateState();}
 void UpdateState(){bind.Text=hotkey.ToString();start.Text=active?"■   Остановить":"▶   Начать кликать";status.Text=active?"● Работает · наведите курсор":"○ Готов к работе";left.Enabled=right.Enabled=unlimited.Enabled=!active;cps.Enabled=!active&&!unlimited.Checked;}
 void LoadSettings(){try{var s=File.ReadAllLines(config);Keys k;if(s.Length==4&&Enum.TryParse<Keys>(s[0],out k)&&Enum.IsDefined(typeof(Keys),k)&&k!=Keys.Escape){hotkey=k;cps.Value=Math.Max(1,Math.Min(1000,int.Parse(s[1])));right.Checked=bool.Parse(s[2]);unlimited.Checked=bool.Parse(s[3]);}}catch{} }
 protected override void OnFormClosed(FormClosedEventArgs e){timer.Stop();timer.Dispose();engine.Dispose();try{Directory.CreateDirectory(Path.GetDirectoryName(config));File.WriteAllLines(config,new[]{hotkey.ToString(),cps.Value.ToString(),right.Checked.ToString(),unlimited.Checked.ToString()});}catch{}base.OnFormClosed(e);}
}
static class Tests {
 internal static int Run(){try{if(Engine.Interval(1000)>Engine.Interval(1))throw new Exception("interval");bool rejected=false;try{Engine.Interval(0);}catch(ArgumentOutOfRangeException){rejected=true;}if(!rejected)throw new Exception("range");var l=Native.Pair(false);var r=Native.Pair(true);if(l[0].data.mouse.flags!=2||l[1].data.mouse.flags!=4||r[0].data.mouse.flags!=8||r[1].data.mouse.flags!=16)throw new Exception("flags");int expected=IntPtr.Size==8?40:28;if(Marshal.SizeOf(typeof(Native.INPUT))!=expected)throw new Exception("ABI");using(var engine=new Engine()){engine.Stop();engine.Stop();}return 0;}catch{return 1;}}
}
}