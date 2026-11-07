using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyVersion("1.2.0.0")]
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
 [DllImport("winmm.dll")] internal static extern uint timeBeginPeriod(uint period);
 [DllImport("winmm.dll")] internal static extern uint timeEndPeriod(uint period);
 [DllImport("user32.dll",SetLastError=true)] internal static extern uint SendInput(uint count, INPUT[] inputs,int size);
 [StructLayout(LayoutKind.Sequential)] internal struct INPUT { public uint type; public UNION data; }
 [StructLayout(LayoutKind.Explicit)] internal struct UNION { [FieldOffset(0)] public MOUSE mouse; }
 [StructLayout(LayoutKind.Sequential)] internal struct MOUSE { public int x,y; public uint data,flags,time; public UIntPtr extra; }
 internal static INPUT[] Pair(bool right) { return new INPUT[]{new INPUT{data=new UNION{mouse=new MOUSE{flags=right?8u:2u}}},new INPUT{data=new UNION{mouse=new MOUSE{flags=right?16u:4u}}}}; }
}
sealed class Engine : IDisposable {
 readonly ManualResetEvent cancel=new ManualResetEvent(false); Thread thread;
 readonly Func<uint,Native.INPUT[],int,uint> send;
 readonly Func<IntPtr> foreground;
 internal Engine():this(Native.SendInput,Native.GetForegroundWindow){}
 internal Engine(Func<uint,Native.INPUT[],int,uint> sender,Func<IntPtr> foregroundWindow){send=sender;foreground=foregroundWindow;}
 internal bool Running { get { return thread!=null&&thread.IsAlive; } }
 internal long Count; internal volatile bool Failed;
 internal static double Interval(int cps) { if(cps<1||cps>1000) throw new ArgumentOutOfRangeException("cps"); return (double)Stopwatch.Frequency/cps; }
 internal void Start(bool right,int cps,bool unlimited,bool hold,IntPtr own) {
  if(Running) return; cancel.Reset(); Failed=false; var pair=Native.Pair(right); var down=new[]{pair[0]};var up=new[]{pair[1]};var size=Marshal.SizeOf(typeof(Native.INPUT)); double interval=Interval(cps);
  thread=new Thread(delegate(){ bool held=false; bool precise=Native.timeBeginPeriod(1)==0;
   try { double next=Stopwatch.GetTimestamp(); int batch=0;
    while(!cancel.WaitOne(0)) {
     if(!hold&&foreground()==own) {
      if(held){if(send(1,up,size)!=1){Failed=true;break;}held=false;}
      if(cancel.WaitOne(10))break;next=Stopwatch.GetTimestamp();continue;
     }
     if(hold){if(!held){held=true;if(send(1,down,size)!=1){Failed=true;break;}}cancel.WaitOne(10);continue;}
     long now=Stopwatch.GetTimestamp();if(!unlimited&&now<next){cancel.WaitOne(Math.Max(1,(int)Math.Ceiling((next-now)*1000/Stopwatch.Frequency)));continue;}
     if(send(2,pair,size)!=2){send(1,up,size);Failed=true;break;}Interlocked.Increment(ref Count);
     next=Math.Max(next+interval,Stopwatch.GetTimestamp());
     // Give the window message loop time even in unrestricted mode.
     if(unlimited&&++batch>=32){batch=0;cancel.WaitOne(1);}
    }
   }catch{Failed=true;}finally{if(held){try{if(send(1,up,size)!=1)Failed=true;}catch{Failed=true;}}if(precise)Native.timeEndPeriod(1);}
  });thread.IsBackground=true;thread.Start();
 }
 internal void Stop(){cancel.Set();}
 public void Dispose(){Stop();if(thread!=null)thread.Join();cancel.Dispose();}
}
sealed class MainForm : Form {
 readonly Color mint=Color.FromArgb(191,242,129), panel=Color.FromArgb(29,41,34), muted=Color.FromArgb(157,177,164);
 readonly Engine engine=new Engine(); readonly Button start=new Button(), bind=new Button(); readonly Label status=new Label(), counter=new Label();
 readonly NumericUpDown cps=new NumericUpDown(); readonly CheckBox unlimited=new CheckBox(); readonly ComboBox mode=new ComboBox(); readonly RadioButton left=new RadioButton(),right=new RadioButton();
 readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer(); Keys hotkey=Keys.F6; bool active,capturing,stopping; readonly string config=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"NosClicker","settings.txt");
 Label TextAt(string text,int x,int y,int w,int size,Color color){var l=new Label{Text=text,Location=new Point(x,y),Size=new Size(w,size+18),ForeColor=color,Font=new Font("Segoe UI",size)}; Controls.Add(l); return l;}
 void ButtonStyle(Button b){b.FlatStyle=FlatStyle.Flat;b.FlatAppearance.BorderSize=0;b.BackColor=panel;b.ForeColor=Color.White;b.Cursor=Cursors.Hand;b.Font=new Font("Segoe UI",11,FontStyle.Bold);Controls.Add(b);}
 internal MainForm(){
  Text="NOS / Clicker"; ClientSize=new Size(460,646); DoubleBuffered=true; FormBorderStyle=FormBorderStyle.FixedSingle; MaximizeBox=false; StartPosition=FormStartPosition.CenterScreen; BackColor=Color.FromArgb(17,26,21); ForeColor=Color.White; Font=new Font("Segoe UI",11); KeyPreview=true;
  TextAt("NOS",30,24,200,32,mint); TextAt("CLICKER  /  1.2",32,83,300,10,muted);
  TextAt("Меньше действий. Больше кликов.",32,118,400,14,Color.White);
  TextAt("01  /  КНОПКА МЫШИ",32,174,390,10,muted);
  left.Text="ЛКМ";right.Text="ПКМ";left.Checked=true;left.SetBounds(32,208,180,38);right.SetBounds(240,208,180,38);Controls.Add(left);Controls.Add(right);
  TextAt("02  /  СКОРОСТЬ",32,267,350,10,muted);
  cps.SetBounds(32,303,120,35);cps.Minimum=1;cps.Maximum=1000;cps.Value=20;cps.BackColor=panel;cps.ForeColor=mint;cps.Font=new Font("Segoe UI",15);Controls.Add(cps);
  TextAt("кликов / сек",164,310,150,10,muted);
  unlimited.Text="Без лимита";unlimited.SetBounds(300,304,140,34);Controls.Add(unlimited);unlimited.CheckedChanged+=delegate{UpdateState();};
  TextAt("03  /  ГОРЯЧАЯ КЛАВИША",32,365,340,10,muted);
  bind.SetBounds(32,399,120,39);ButtonStyle(bind);bind.Click+=delegate{Stop();capturing=true;bind.Text="Нажмите…";};
  TextAt("Запуск / пауза · Esc — стоп",168,408,280,10,muted);
  start.SetBounds(32,468,396,54);ButtonStyle(start);start.BackColor=mint;start.ForeColor=BackColor;start.Click+=delegate{Toggle();};
  status.SetBounds(32,539,230,25);status.ForeColor=muted;Controls.Add(status);counter.SetBounds(270,539,158,25);counter.TextAlign=ContentAlignment.TopRight;counter.ForeColor=muted;Controls.Add(counter);
  foreach(Control control in Controls) if(control.Top>=267) control.Top+=66;
  mode.DropDownStyle=ComboBoxStyle.DropDownList;mode.Items.AddRange(new object[]{"Повторные клики","Удерживать кнопку"});mode.SelectedIndex=1;mode.SetBounds(32,263,396,32);mode.BackColor=panel;mode.ForeColor=mint;Controls.Add(mode);mode.SelectedIndexChanged+=delegate{UpdateState();};
  LoadSettings(); timer.Interval=100;timer.Tick+=delegate{counter.Text=Interlocked.Read(ref engine.Count).ToString("N0")+" кликов";if((active||stopping)&&!engine.Running){active=false;stopping=false;UpdateState();if(engine.Failed)status.Text="Ошибка ввода / прав доступа";}};timer.Start();UpdateState();
 }
 protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);if(!Native.RegisterHotKey(Handle,1,0x4000,(uint)hotkey)){status.Text="Клавиша занята — смените";}Native.RegisterHotKey(Handle,2,0x4000,(uint)Keys.Escape);}
 protected override void OnHandleDestroyed(EventArgs e){Native.UnregisterHotKey(Handle,1);Native.UnregisterHotKey(Handle,2);base.OnHandleDestroyed(e);}
 protected override void WndProc(ref Message m){if(m.Msg==0x312){if(m.WParam.ToInt32()==2){capturing=false;Stop();}else if(!capturing)Toggle();}base.WndProc(ref m);}
 protected override bool ProcessCmdKey(ref Message msg,Keys keyData){if(capturing){Keys k=keyData&Keys.KeyCode;if(k==Keys.Escape){capturing=false;UpdateState();return true;}if((k>=Keys.F1&&k<=Keys.F12)||(k>=Keys.A&&k<=Keys.Z)||(k>=Keys.D0&&k<=Keys.D9)||k==Keys.Space||k==Keys.Insert||k==Keys.Home||k==Keys.End){Native.UnregisterHotKey(Handle,1);if(Native.RegisterHotKey(Handle,1,0x4000,(uint)k)){hotkey=k;capturing=false;UpdateState();}else{Native.RegisterHotKey(Handle,1,0x4000,(uint)hotkey);bind.Text="Занята";}}return true;}return base.ProcessCmdKey(ref msg,keyData);}
 void Toggle(){if(stopping)return;if(active){Stop();return;}capturing=false;engine.Start(right.Checked,(int)cps.Value,unlimited.Checked,mode.SelectedIndex==1,Handle);active=true;UpdateState();}
 void Stop(){engine.Stop();active=false;stopping=engine.Running;UpdateState();}
 void UpdateState(){bool idle=!active&&!stopping;bool hold=mode.SelectedIndex==1;bind.Text=capturing?"Нажмите…":hotkey.ToString();start.Text=stopping?"Останавливаем…":active?"■   Остановить":hold?"▶   Начать удержание":"▶   Начать кликать";start.Enabled=!stopping;status.Text=stopping?"Отпускаем кнопку…":active?hold?"● Удержание активно":"● Клики активны":"○ Готов к работе";left.Enabled=right.Enabled=mode.Enabled=idle;unlimited.Enabled=idle&&!hold;cps.Enabled=idle&&!hold&&!unlimited.Checked;}
 void LoadSettings(){try{var s=File.ReadAllLines(config);Keys k;if(s.Length>=4&&Enum.TryParse<Keys>(s[0],out k)&&Enum.IsDefined(typeof(Keys),k)&&k!=Keys.Escape){hotkey=k;cps.Value=Math.Max(1,Math.Min(1000,int.Parse(s[1])));right.Checked=bool.Parse(s[2]);unlimited.Checked=bool.Parse(s[3]);if(s.Length>=6&&s[5]=="v1.2")mode.SelectedIndex=s[4]=="hold"?1:0;}}catch{} }
 protected override void OnFormClosed(FormClosedEventArgs e){timer.Stop();timer.Dispose();engine.Dispose();try{Directory.CreateDirectory(Path.GetDirectoryName(config));File.WriteAllLines(config,new[]{hotkey.ToString(),cps.Value.ToString(),right.Checked.ToString(),unlimited.Checked.ToString(),mode.SelectedIndex==1?"hold":"click","v1.2"});}catch{}base.OnFormClosed(e);}
}
static class Tests {
 static void HoldTest(bool right,bool fail){
  int downs=0,ups=0;var pair=Native.Pair(right);var pressed=new ManualResetEvent(false);
  using(var engine=new Engine(delegate(uint count,Native.INPUT[] input,int size){uint f=input[0].data.mouse.flags;if(f==pair[0].data.mouse.flags){Interlocked.Increment(ref downs);pressed.Set();return fail?0u:count;}if(f==pair[1].data.mouse.flags)Interlocked.Increment(ref ups);return count;},delegate{return new IntPtr(123);})){
   engine.Start(right,20,false,true,new IntPtr(123));if(!pressed.WaitOne(2000))throw new Exception("hold did not start in own foreground window");if(!fail){Thread.Sleep(100);if(downs!=1||ups!=0||Interlocked.Read(ref engine.Count)!=0)throw new Exception("hold emitted repeat or premature release");}engine.Stop();
  }
  pressed.Dispose();if(downs!=1||ups!=1)throw new Exception("hold must press once and release once");
 }
 internal static int Run(){try{HoldTest(false,false);HoldTest(true,false);HoldTest(false,true);if(Engine.Interval(1000)>Engine.Interval(1))throw new Exception("interval");bool rejected=false;try{Engine.Interval(0);}catch(ArgumentOutOfRangeException){rejected=true;}if(!rejected)throw new Exception("range");var l=Native.Pair(false);var r=Native.Pair(true);if(l[0].data.mouse.flags!=2||l[1].data.mouse.flags!=4||r[0].data.mouse.flags!=8||r[1].data.mouse.flags!=16)throw new Exception("flags");int expected=IntPtr.Size==8?40:28;if(Marshal.SizeOf(typeof(Native.INPUT))!=expected)throw new Exception("ABI");using(var engine=new Engine()){engine.Stop();engine.Stop();}return 0;}catch{return 1;}}
}
}